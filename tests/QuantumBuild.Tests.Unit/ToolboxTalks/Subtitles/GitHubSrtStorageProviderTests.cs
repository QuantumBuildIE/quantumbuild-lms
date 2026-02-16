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
/// Unit tests for GitHubSrtStorageProvider
/// </summary>
public class GitHubSrtStorageProviderTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<GitHubSrtStorageProvider>> _loggerMock;
    private readonly IOptions<SubtitleProcessingSettings> _settings;
    private readonly Guid _testTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public GitHubSrtStorageProviderTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<GitHubSrtStorageProvider>>();

        var settings = new SubtitleProcessingSettings
        {
            SrtStorage = new SrtStorageSettings
            {
                Type = "GitHub",
                GitHub = new GitHubStorageSettings
                {
                    Token = "test-token",
                    Owner = "test-owner",
                    Repo = "test-repo",
                    Branch = "main",
                    Path = "subs"
                }
            }
        };
        _settings = Options.Create(settings);
    }

    #region UploadSrtAsync Tests

    [Fact]
    public async Task UploadSrtAsync_WithSuccessfulUpload_ReturnsUrl()
    {
        // Arrange
        SetupSequentialResponses(
            // First request: Check if file exists (404 = doesn't exist)
            (HttpStatusCode.NotFound, "Not Found"),
            // Second request: Upload file
            (HttpStatusCode.Created, """{"content": {"sha": "abc123"}}""")
        );

        var sut = CreateService();

        // Act
        var result = await sut.UploadSrtAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "test_en.srt", _testTenantId);

        // Assert
        result.Success.Should().BeTrue();
        result.Url.Should().Be($"https://raw.githubusercontent.com/test-owner/test-repo/main/{_testTenantId}/subs/test_en.srt");
    }

    [Fact]
    public async Task UploadSrtAsync_WithExistingFile_UpdatesFile()
    {
        // Arrange
        SetupSequentialResponses(
            // First request: Check if file exists (returns existing SHA)
            (HttpStatusCode.OK, """{"sha": "existing-sha"}"""),
            // Second request: Update file
            (HttpStatusCode.OK, """{"content": {"sha": "new-sha"}}""")
        );

        var sut = CreateService();

        // Act
        var result = await sut.UploadSrtAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "test_en.srt", _testTenantId);

        // Assert
        result.Success.Should().BeTrue();
        result.Url.Should().Contain("test_en.srt");
    }

    [Fact]
    public async Task UploadSrtAsync_WithUploadError_ReturnsFailure()
    {
        // Arrange
        SetupSequentialResponses(
            // First request: Check if file exists
            (HttpStatusCode.NotFound, "Not Found"),
            // Second request: Upload fails
            (HttpStatusCode.InternalServerError, "Server Error")
        );

        var sut = CreateService();

        // Act
        var result = await sut.UploadSrtAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "test_en.srt", _testTenantId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("GitHub upload failed");
    }

    [Fact]
    public async Task UploadSrtAsync_WithUnauthorizedError_ReturnsFailure()
    {
        // Arrange
        SetupSequentialResponses(
            // First request: Unauthorized
            (HttpStatusCode.Unauthorized, "Bad credentials")
        );

        var sut = CreateService();

        // Act
        var result = await sut.UploadSrtAsync("1\n00:00:00,000 --> 00:00:02,000\nHello", "test_en.srt", _testTenantId);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UploadSrtAsync_GeneratesCorrectFilePath()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                callCount++;
                if (callCount == 2) capturedRequest = req;
            })
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Get)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"content": {"sha": "abc123"}}""")
                };
            });

        var sut = CreateService();

        // Act
        await sut.UploadSrtAsync("content", "my_video_en.srt", _testTenantId);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Contain($"{_testTenantId}/subs/my_video_en.srt");
    }

    [Fact]
    public async Task UploadSrtAsync_WithEmptyPath_UsesTenantIdAndFileName()
    {
        // Arrange
        var settings = new SubtitleProcessingSettings
        {
            SrtStorage = new SrtStorageSettings
            {
                GitHub = new GitHubStorageSettings
                {
                    Token = "test-token",
                    Owner = "test-owner",
                    Repo = "test-repo",
                    Branch = "main",
                    Path = "" // Empty path
                }
            }
        };

        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Get)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"content": {"sha": "abc123"}}""")
                };
            });

        var sut = new GitHubSrtStorageProvider(_httpClient, Options.Create(settings), _loggerMock.Object);

        // Act
        var result = await sut.UploadSrtAsync("content", "test.srt", _testTenantId);

        // Assert - with tenant isolation, path is {tenantId}/{fileName}
        result.Url.Should().Be($"https://raw.githubusercontent.com/test-owner/test-repo/main/{_testTenantId}/test.srt");
    }

    [Fact]
    public async Task UploadSrtAsync_SendsCorrectHeaders()
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var sut = CreateService();

        // Act
        await sut.UploadSrtAsync("content", "test.srt", _testTenantId);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Should().Contain(h => h.Key == "Accept");
        capturedRequest.Headers.Should().Contain(h => h.Key == "X-GitHub-Api-Version");
        capturedRequest.Headers.Should().Contain(h => h.Key == "User-Agent");
    }

    [Fact]
    public async Task UploadSrtAsync_EncodesContentAsBase64()
    {
        // Arrange
        string? capturedBody = null;

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                callCount++;
                if (callCount == 2 && req.Content != null)
                {
                    capturedBody = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Get)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"content": {"sha": "abc123"}}""")
                };
            });

        var sut = CreateService();
        var srtContent = "1\n00:00:00,000 --> 00:00:02,000\nHello World";

        // Act
        await sut.UploadSrtAsync(srtContent, "test.srt", _testTenantId);

        // Assert
        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var content = doc.RootElement.GetProperty("content").GetString();
        content.Should().NotBe(srtContent); // Should be base64 encoded
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(content!));
        decoded.Should().Be(srtContent);
    }

    [Fact]
    public async Task UploadSrtAsync_IncludesShaWhenUpdating()
    {
        // Arrange
        string? capturedBody = null;

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                callCount++;
                if (callCount == 2 && req.Content != null)
                {
                    capturedBody = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Get)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"sha": "existing-sha-123"}""")
                    };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"content": {"sha": "new-sha"}}""")
                };
            });

        var sut = CreateService();

        // Act
        await sut.UploadSrtAsync("content", "test.srt", _testTenantId);

        // Assert
        capturedBody.Should().Contain("existing-sha-123");
    }

    [Fact]
    public async Task UploadSrtAsync_WithException_ReturnsFailure()
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
        var result = await sut.UploadSrtAsync("content", "test.srt", _testTenantId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Upload failed");
    }

    #endregion

    #region GetSrtContentAsync Tests

    [Fact]
    public async Task GetSrtContentAsync_WithExistingFile_ReturnsContent()
    {
        // Arrange
        var expectedContent = "1\n00:00:00,000 --> 00:00:02,000\nHello World";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedContent)
            });

        var sut = CreateService();

        // Act
        var result = await sut.GetSrtContentAsync("test_en.srt");

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task GetSrtContentAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var sut = CreateService();

        // Act
        var result = await sut.GetSrtContentAsync("nonexistent.srt");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSrtContentAsync_WithException_ReturnsNull()
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
        var result = await sut.GetSrtContentAsync("test.srt");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSrtContentAsync_UsesCorrectRawUrl()
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
                Content = new StringContent("content")
            });

        var sut = CreateService();

        // Act
        await sut.GetSrtContentAsync("my_video_es.srt");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be(
            "https://raw.githubusercontent.com/test-owner/test-repo/main/subs/my_video_es.srt");
    }

    #endregion

    #region DeleteSrtAsync Tests

    [Fact]
    public async Task DeleteSrtAsync_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        SetupSequentialResponses(
            // First request: Get SHA
            (HttpStatusCode.OK, """{"sha": "file-sha-123"}"""),
            // Second request: Delete
            (HttpStatusCode.OK, """{"commit": {"sha": "commit-sha"}}""")
        );

        var sut = CreateService();

        // Act
        var result = await sut.DeleteSrtAsync("test_en.srt");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSrtAsync_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var sut = CreateService();

        // Act
        var result = await sut.DeleteSrtAsync("nonexistent.srt");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSrtAsync_WithDeleteError_ReturnsFalse()
    {
        // Arrange
        SetupSequentialResponses(
            // First request: Get SHA
            (HttpStatusCode.OK, """{"sha": "file-sha-123"}"""),
            // Second request: Delete fails
            (HttpStatusCode.InternalServerError, "Server Error")
        );

        var sut = CreateService();

        // Act
        var result = await sut.DeleteSrtAsync("test.srt");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSrtAsync_WithException_ReturnsFalse()
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
        var result = await sut.DeleteSrtAsync("test.srt");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSrtAsync_SendsCorrectDeleteRequest()
    {
        // Arrange
        HttpRequestMessage? capturedDeleteRequest = null;
        var callCount = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                callCount++;
                if (callCount == 2) capturedDeleteRequest = req;
            })
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Get)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"sha": "file-sha"}""")
                    };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"commit": {}}""")
                };
            });

        var sut = CreateService();

        // Act
        await sut.DeleteSrtAsync("test.srt");

        // Assert
        capturedDeleteRequest.Should().NotBeNull();
        capturedDeleteRequest!.Method.Should().Be(HttpMethod.Delete);
    }

    #endregion

    private GitHubSrtStorageProvider CreateService()
    {
        return new GitHubSrtStorageProvider(_httpClient, _settings, _loggerMock.Object);
    }

    private void SetupSequentialResponses(params (HttpStatusCode statusCode, string content)[] responses)
    {
        var callIndex = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = responses[Math.Min(callIndex, responses.Length - 1)];
                callIndex++;
                return new HttpResponseMessage(response.statusCode)
                {
                    Content = new StringContent(response.content)
                };
            });
    }
}
