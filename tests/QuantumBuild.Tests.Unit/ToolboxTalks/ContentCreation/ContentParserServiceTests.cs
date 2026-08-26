using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using QuantumBuild.Core.Application.Abstractions.AI;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.ContentCreation;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.ContentCreation;

/// <summary>
/// Unit tests for ContentParserService — specifically the PreserveSourceWording default
/// and minimumSections wiring at the root of the "section degradation" regression
/// (see docs/video-parsing-regression-recon.md §A). Verifies the prompt actually sent to
/// Claude, since the AI's own rewrite behaviour cannot be asserted without a live call.
/// </summary>
public class ContentParserServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<ContentParserService>> _loggerMock;
    private readonly Mock<IAiUsageLogger> _aiUsageLoggerMock;
    private readonly IOptions<SubtitleProcessingSettings> _settings;
    private readonly IOptions<AIProviderOptions> _aiProviders;

    public ContentParserServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<ContentParserService>>();
        _aiUsageLoggerMock = new Mock<IAiUsageLogger>();

        _settings = Options.Create(new SubtitleProcessingSettings
        {
            Claude = new ClaudeSettings
            {
                ApiKey = "test-api-key",
                MaxTokens = 8000,
                BaseUrl = "https://api.anthropic.com/v1"
            }
        });

        _aiProviders = Options.Create(new AIProviderOptions
        {
            Anthropic = new AnthropicProviderOptions
            {
                Models = new AnthropicModels { Sonnet = "claude-sonnet-4-5" }
            }
        });
    }

    private ContentParserService CreateService() =>
        new(_httpClient, _settings, _aiProviders, _aiUsageLoggerMock.Object, _loggerMock.Object);

    private const string ThreeSectionAiResponse = """
        [
          {"sortOrder": 1, "title": "Section A", "content": "<p>Rewritten A.</p>", "source": "Video"},
          {"sortOrder": 2, "title": "Section B", "content": "<p>Rewritten B.</p>", "source": "Video"},
          {"sortOrder": 3, "title": "Section C", "content": "<p>Rewritten C.</p>", "source": "Video"}
        ]
        """;

    // preserveSourceWording = false (the new default) must select the rewrite prompt
    // branch — "create clear, concise sections" — not the verbatim branch, and must
    // request at least 7 sections (matching the legacy wizard's floor), not the old
    // hard-coded 2.
    [Fact]
    public async Task ParseContentAsync_PreserveSourceWordingFalse_UsesRewritePromptWithMinimumSectionsSeven()
    {
        string? capturedBody = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    content = new[] { new { type = "text", text = ThreeSectionAiResponse } },
                    usage = new { input_tokens = 100, output_tokens = 50 },
                    model = "claude-sonnet-4-5"
                }))
            });

        var sut = CreateService();

        var result = await sut.ParseContentAsync(
            "Raw rambling transcript with no natural section breaks.",
            InputMode.Video,
            Guid.NewGuid(),
            userId: null,
            preserveSourceWording: false);

        result.Success.Should().BeTrue();
        result.Sections.Should().HaveCount(3, "the rewrite prompt is not bound to the old floor of 2");

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("Create at least 7 sections");
        capturedBody.Should().NotContain("VERBATIM");
        capturedBody.Should().NotContain("NOT to rewrite");
    }

    // preserveSourceWording = true must still select the verbatim prompt branch —
    // confirms verbatim remains available as an explicit, non-default option.
    [Fact]
    public async Task ParseContentAsync_PreserveSourceWordingTrue_StillUsesVerbatimPrompt()
    {
        string? capturedBody = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    content = new[] { new { type = "text", text = ThreeSectionAiResponse } },
                    usage = new { input_tokens = 100, output_tokens = 50 },
                    model = "claude-sonnet-4-5"
                }))
            });

        var sut = CreateService();

        var result = await sut.ParseContentAsync(
            "Customer-approved source text.",
            InputMode.Video,
            Guid.NewGuid(),
            userId: null,
            preserveSourceWording: true);

        result.Success.Should().BeTrue();

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("VERBATIM");
        capturedBody.Should().Contain("NOT to rewrite");
        capturedBody.Should().Contain("Identify between 7 and a reasonable number");
    }
}
