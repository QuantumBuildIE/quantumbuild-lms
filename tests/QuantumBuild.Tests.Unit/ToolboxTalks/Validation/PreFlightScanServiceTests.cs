using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using QuantumBuild.Core.Application.Abstractions.AI;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.PreFlightScan;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class PreFlightScanServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly Mock<IAiUsageLogger> _aiUsageLoggerMock = new();
    private readonly Mock<ILogger<PreFlightScanService>> _loggerMock = new();

    private PreFlightScanService CreateService()
    {
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        var settings = Options.Create(new SubtitleProcessingSettings
        {
            Claude = new ClaudeSettings
            {
                ApiKey = "test-api-key",
                BaseUrl = "https://api.anthropic.com/v1"
            }
        });

        var aiProviders = Options.Create(new AIProviderOptions
        {
            Anthropic = new AnthropicProviderOptions
            {
                Models = new AnthropicModels { Haiku = "claude-haiku-4-5" }
            }
        });

        return new PreFlightScanService(
            httpClient,
            settings,
            _aiUsageLoggerMock.Object,
            _loggerMock.Object,
            aiProviders);
    }

    private void SetupClaudeResponse(string contentText)
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = contentText } },
            usage = new { input_tokens = 10, output_tokens = 20 },
            model = "claude-haiku-4-5"
        });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });
    }

    private const string CleanJson = """
        {
          "highRiskTerms": [
            {"term": "isolate", "risk": "can be mistranslated as 'insulate'", "suggestedTranslation": "aislar"}
          ],
          "properNouns": ["QuantumBuild"],
          "roleConstructs": [
            {"term": "Site Safety Officer", "suggestedTranslation": "Oficial de Seguridad del Sitio"}
          ],
          "slashConstructs": [
            {"term": "lock/tag", "risk": "the slash may be dropped, losing the dual requirement"}
          ]
        }
        """;

    [Fact]
    public async Task ScanAsync_WithCleanJson_ParsesAllFindings()
    {
        SetupClaudeResponse(CleanJson);
        var sut = CreateService();

        var result = await sut.ScanAsync(["Isolate the machine before servicing."], "Spanish", "manufacturing");

        result.HasFindings.Should().BeTrue();
        result.HighRiskCount.Should().Be(1);
        result.ProperNounCount.Should().Be(1);
        result.RoleConstructCount.Should().Be(1);
        result.Findings.Should().ContainSingle(f => f.Type == PreFlightFindingType.SlashConstruct);
    }

    [Fact]
    public async Task ScanAsync_WithMarkdownFencedJson_ParsesCorrectly()
    {
        var fenced = $"```json\n{CleanJson}\n```";
        SetupClaudeResponse(fenced);
        var sut = CreateService();

        var result = await sut.ScanAsync(["Isolate the machine before servicing."], "Spanish", "manufacturing");

        result.HasFindings.Should().BeTrue();
        result.HighRiskCount.Should().Be(1);
        result.ProperNounCount.Should().Be(1);
        result.RoleConstructCount.Should().Be(1);
    }

    [Fact]
    public async Task ScanAsync_WithPlainFencedJson_ParsesCorrectly()
    {
        var fenced = $"```\n{CleanJson}\n```";
        SetupClaudeResponse(fenced);
        var sut = CreateService();

        var result = await sut.ScanAsync(["Isolate the machine before servicing."], "Spanish", "manufacturing");

        result.HasFindings.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_WithPreamblePrecedingJson_ParsesCorrectly()
    {
        var withPreamble = $"Here is the pre-flight analysis you requested:\n\n{CleanJson}";
        SetupClaudeResponse(withPreamble);
        var sut = CreateService();

        var result = await sut.ScanAsync(["Isolate the machine before servicing."], "Spanish", "manufacturing");

        result.HasFindings.Should().BeTrue();
        result.HighRiskCount.Should().Be(1);
    }

    [Fact]
    public async Task ScanAsync_WithPreambleAndTrailingCommentary_ParsesCorrectly()
    {
        var wrapped = $"Sure, here are the findings:\n\n{CleanJson}\n\nLet me know if you need anything else!";
        SetupClaudeResponse(wrapped);
        var sut = CreateService();

        var result = await sut.ScanAsync(["Isolate the machine before servicing."], "Spanish", "manufacturing");

        result.HasFindings.Should().BeTrue();
        result.HighRiskCount.Should().Be(1);
    }

    [Fact]
    public async Task ScanAsync_WithEmptyFindingCategories_ReturnsNoFindings()
    {
        const string empty = """
            {
              "highRiskTerms": [],
              "properNouns": [],
              "roleConstructs": [],
              "slashConstructs": []
            }
            """;
        SetupClaudeResponse(empty);
        var sut = CreateService();

        var result = await sut.ScanAsync(["Plain benign text."], "Spanish", null);

        result.HasFindings.Should().BeFalse();
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithGenuinelyUnparseableResponse_ReturnsEmptyResultAndDoesNotThrow()
    {
        SetupClaudeResponse("I'm sorry, I cannot analyse that content.");
        var sut = CreateService();

        var act = () => sut.ScanAsync(["Some text."], "Spanish", null);

        (await act.Should().NotThrowAsync()).Which.Should().BeEquivalentTo(new PreFlightScanResult(
            [], HasFindings: false, HighRiskCount: 0, ProperNounCount: 0, RoleConstructCount: 0));
    }

    [Fact]
    public async Task ScanAsync_WithTruncatedMidJsonResponse_ReturnsEmptyResultAndDoesNotThrow()
    {
        // Simulates a response cut short (e.g. max_tokens reached mid-object) — the
        // stray trailing content after the last value is exactly the class of error
        // ('o' is invalid after a value) this fix targets.
        SetupClaudeResponse("""{"highRiskTerms": [{"term": "isolate", "risk": "so""");
        var sut = CreateService();

        var act = () => sut.ScanAsync(["Some text."], "Spanish", null);

        (await act.Should().NotThrowAsync()).Which.HasFindings.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithNonJsonProseOnly_ReturnsEmptyResultAndDoesNotThrow()
    {
        SetupClaudeResponse("There is nothing risky in this text.");
        var sut = CreateService();

        var result = await sut.ScanAsync(["Some text."], "Spanish", null);

        result.HasFindings.Should().BeFalse();
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_LogsErrorWithRawResponse_OnUnparseableJson()
    {
        SetupClaudeResponse("not json at all, no braces here");
        var sut = CreateService();

        await sut.ScanAsync(["Some text."], "Spanish", null);

        var loggedError = _loggerMock.Invocations.Any(i =>
            i.Method.Name == "Log" &&
            i.Arguments.Any(a => a is LogLevel level && level == LogLevel.Error));
        loggedError.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_WithApiError_ReturnsEmptyResultAndDoesNotThrow()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server error")
            });

        var sut = CreateService();

        var act = () => sut.ScanAsync(["Some text."], "Spanish", null);

        (await act.Should().NotThrowAsync()).Which.HasFindings.Should().BeFalse();
    }
}
