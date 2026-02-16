using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Subtitles;

/// <summary>
/// Unit tests for ClaudeTranslationService
/// </summary>
public class ClaudeTranslationServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<ClaudeTranslationService>> _loggerMock;
    private readonly IOptions<SubtitleProcessingSettings> _settings;

    public ClaudeTranslationServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<ClaudeTranslationService>>();

        var settings = new SubtitleProcessingSettings
        {
            Claude = new ClaudeSettings
            {
                ApiKey = "test-api-key",
                Model = "claude-sonnet-4-20250514",
                MaxTokens = 4000,
                BaseUrl = "https://api.anthropic.com/v1"
            }
        };
        _settings = Options.Create(settings);
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithSuccessfulResponse_ReturnsTranslatedContent()
    {
        // Arrange
        var originalSrt = """
            1
            00:00:00,000 --> 00:00:02,000
            Hello World
            """;

        var translatedSrt = """
            1
            00:00:00,000 --> 00:00:02,000
            Hola Mundo
            """;

        var responseJson = new
        {
            content = new[]
            {
                new { type = "text", text = translatedSrt }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync(originalSrt, "Spanish");

        // Assert
        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Be(translatedSrt);
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_PreservesTimestampsInTranslation()
    {
        // Arrange
        var originalSrt = """
            1
            00:00:00,000 --> 00:00:02,000
            First subtitle

            2
            00:00:02,500 --> 00:00:04,500
            Second subtitle
            """;

        var translatedSrt = """
            1
            00:00:00,000 --> 00:00:02,000
            Premier sous-titre

            2
            00:00:02,500 --> 00:00:04,500
            Deuxième sous-titre
            """;

        var responseJson = new
        {
            content = new[]
            {
                new { type = "text", text = translatedSrt }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync(originalSrt, "French");

        // Assert
        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Contain("00:00:00,000 --> 00:00:02,000");
        result.TranslatedContent.Should().Contain("00:00:02,500 --> 00:00:04,500");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithEmptyTranslation_ReturnsFailure()
    {
        // Arrange
        var originalSrt = """
            1
            00:00:00,000 --> 00:00:02,000
            Hello World
            """;

        var responseJson = new
        {
            content = new[]
            {
                new { type = "text", text = "" }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync(originalSrt, "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty content");
        result.TranslatedContent.Should().BeEmpty();
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithWhitespaceOnlyTranslation_ReturnsFailure()
    {
        // Arrange
        var originalSrt = """
            1
            00:00:00,000 --> 00:00:02,000
            Hello World
            """;

        var responseJson = new
        {
            content = new[]
            {
                new { type = "text", text = "   " }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync(originalSrt, "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty content");
        result.TranslatedContent.Should().BeEmpty();
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.BadRequest, "Invalid request");

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Claude API error");
        result.ErrorMessage.Should().Contain("BadRequest");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithRateLimitError_ReturnsFailure()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.TooManyRequests, "Rate limit exceeded");

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Claude API error");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithUnauthorizedError_ReturnsFailure()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.Unauthorized, "Invalid API key");

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Claude API error");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithServerError_ReturnsFailure()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Server error");

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Claude API error");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithHttpException_ReturnsFailure()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP request failed");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "not valid json {{{");

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("parse");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithMissingContentProperty_ReturnsFailure()
    {
        // Arrange
        var responseJson = new { usage = new { input_tokens = 100, output_tokens = 50 } };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty content");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithMultipleContentBlocks_UsesFirstTextBlock()
    {
        // Arrange
        var responseJson = new
        {
            content = new object[]
            {
                new { type = "text", text = "First translation" },
                new { type = "text", text = "Second translation" }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Be("First translation");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_SendsCorrectHeaders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"content":[{"type":"text","text":"translated"}]}""")
            });

        var sut = CreateService();

        // Act
        await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == "x-api-key");
        capturedRequest.Headers.Should().Contain(h => h.Key == "anthropic-version");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_SendsCorrectRequestBody()
    {
        // Arrange
        string? capturedBody = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"content":[{"type":"text","text":"translated"}]}""")
            });

        var sut = CreateService();

        // Act
        await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Polish");

        // Assert
        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("Polish");
        capturedBody.Should().Contain("claude-sonnet-4-20250514");
        capturedBody.Should().Contain("max_tokens");
    }

    [Theory]
    [InlineData("Spanish")]
    [InlineData("Polish")]
    [InlineData("French")]
    [InlineData("German")]
    [InlineData("Portuguese")]
    public async Task TranslateSrtBatchAsync_SupportsMultipleLanguages(string targetLanguage)
    {
        // Arrange
        var translatedText = $"Translated to {targetLanguage}";
        var responseJson = new
        {
            content = new[]
            {
                new { type = "text", text = translatedText }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", targetLanguage);

        // Assert
        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Contain(targetLanguage);
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_WithCancelledToken_ReturnsFailure()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Operation was cancelled"));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "Spanish");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("failed");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_HandlesLargeBatches()
    {
        // Arrange
        var largeSrt = string.Join("\n\n", Enumerable.Range(1, 100).Select(i =>
            $"{i}\n00:00:{i:D2},000 --> 00:00:{i:D2},500\nSubtitle number {i}"));

        var responseJson = new
        {
            content = new[]
            {
                new { type = "text", text = largeSrt.Replace("Subtitle", "Subtitulo") }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync(largeSrt, "Spanish");

        // Assert
        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Contain("Subtitulo");
    }

    [Fact]
    public async Task TranslateSrtBatchAsync_HandlesSpecialCharactersInSrt()
    {
        // Arrange
        var srtWithSpecialChars = """
            1
            00:00:00,000 --> 00:00:02,000
            Hello "World" & <Company>
            """;

        var translatedSrt = """
            1
            00:00:00,000 --> 00:00:02,000
            Hola "Mundo" & <Empresa>
            """;

        var responseJson = new
        {
            content = new[]
            {
                new { type = "text", text = translatedSrt }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(responseJson));

        var sut = CreateService();

        // Act
        var result = await sut.TranslateSrtBatchAsync(srtWithSpecialChars, "Spanish");

        // Assert
        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Contain("\"Mundo\"");
    }

    private ClaudeTranslationService CreateService()
    {
        return new ClaudeTranslationService(_httpClient, _settings, _loggerMock.Object);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }
}
