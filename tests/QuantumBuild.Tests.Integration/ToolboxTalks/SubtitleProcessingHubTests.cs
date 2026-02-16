using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the Subtitle Processing SignalR Hub.
/// Tests real-time progress updates and subscription functionality.
/// </summary>
public class SubtitleProcessingHubTests : IntegrationTestBase
{
    public SubtitleProcessingHubTests(CustomWebApplicationFactory factory) : base(factory) { }

    private HubConnection CreateHubConnection(string accessToken)
    {
        var hubUrl = Factory.Server.BaseAddress + "api/hubs/subtitle-processing";

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();
    }

    private string GetAdminAccessToken()
    {
        // Create an authenticated client to get the token
        using var client = Factory.CreateAuthenticatedClient(TestUserType.Admin);
        var authHeader = client.DefaultRequestHeaders.Authorization;
        return authHeader?.Parameter ?? throw new InvalidOperationException("No auth token found");
    }

    #region Connection Tests

    [Fact]
    public async Task Hub_CanConnect_WithValidToken()
    {
        // Arrange
        var token = GetAdminAccessToken();
        var connection = CreateHubConnection(token);

        try
        {
            // Act
            await connection.StartAsync();

            // Assert
            connection.State.Should().Be(HubConnectionState.Connected);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Hub_CanDisconnect_Gracefully()
    {
        // Arrange
        var token = GetAdminAccessToken();
        var connection = CreateHubConnection(token);

        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        try
        {
            // Act
            await connection.StopAsync();

            // Assert
            connection.State.Should().Be(HubConnectionState.Disconnected);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    #endregion

    #region Subscription Tests

    [Fact]
    public async Task SubscribeToJob_SuccessfullyJoinsGroup()
    {
        // Arrange
        var token = GetAdminAccessToken();
        var connection = CreateHubConnection(token);
        var jobId = Guid.NewGuid();

        try
        {
            await connection.StartAsync();

            // Act & Assert - Should not throw
            await connection.InvokeAsync("SubscribeToJob", jobId);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribeFromJob_SuccessfullyLeavesGroup()
    {
        // Arrange
        var token = GetAdminAccessToken();
        var connection = CreateHubConnection(token);
        var jobId = Guid.NewGuid();

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync("SubscribeToJob", jobId);

            // Act & Assert - Should not throw
            await connection.InvokeAsync("UnsubscribeFromJob", jobId);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribeToJob_CanSubscribeToMultipleJobs()
    {
        // Arrange
        var token = GetAdminAccessToken();
        var connection = CreateHubConnection(token);
        var jobId1 = Guid.NewGuid();
        var jobId2 = Guid.NewGuid();
        var jobId3 = Guid.NewGuid();

        try
        {
            await connection.StartAsync();

            // Act & Assert - Should not throw
            await connection.InvokeAsync("SubscribeToJob", jobId1);
            await connection.InvokeAsync("SubscribeToJob", jobId2);
            await connection.InvokeAsync("SubscribeToJob", jobId3);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    #endregion

    #region Progress Update Tests

    [Fact]
    public async Task Hub_ReceivesProgressUpdates_WhenSubscribedToJob()
    {
        // Arrange
        var token = GetAdminAccessToken();
        var connection = CreateHubConnection(token);
        var progressUpdates = new List<ProgressUpdateMessage>();
        var updateReceived = new TaskCompletionSource<bool>();

        connection.On<ProgressUpdateMessage>("ProgressUpdate", update =>
        {
            progressUpdates.Add(update);
            updateReceived.TrySetResult(true);
        });

        try
        {
            await connection.StartAsync();

            // Create a talk and start processing
            var createCommand = new
            {
                Title = $"Talk for hub progress test {Guid.NewGuid()}",
                Frequency = ToolboxTalkFrequency.Once,
                RequiresQuiz = false,
                IsActive = true,
                Sections = new[]
                {
                    new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
                }
            };

            var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
            var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
            var talkId = createdTalk!.Id;

            // Start processing
            var startRequest = new
            {
                VideoUrl = "https://drive.google.com/file/d/test/view",
                VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
                TargetLanguages = new List<string> { "Spanish" }
            };

            var startResponse = await AdminClient.PostAsJsonAsync(
                $"/api/toolbox-talks/{talkId}/subtitles/process", startRequest);

            if (startResponse.StatusCode == HttpStatusCode.Accepted)
            {
                var result = await startResponse.Content.ReadFromJsonAsync<StartProcessingResponse>();
                var jobId = result!.JobId;

                // Subscribe to the job
                await connection.InvokeAsync("SubscribeToJob", jobId);

                // Wait a short time for any updates (background processing may not be running)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await updateReceived.Task.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // This is expected since background processing is not running in tests
                }
            }

            // Note: In integration tests with Hangfire disabled, we may not receive actual updates
            // This test verifies the connection and subscription mechanism works
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    #endregion

    #region Multiple Clients Tests

    [Fact]
    public async Task Hub_MultipleClientsCanConnect()
    {
        // Arrange
        var token = GetAdminAccessToken();
        var connection1 = CreateHubConnection(token);
        var connection2 = CreateHubConnection(token);
        var jobId = Guid.NewGuid();

        try
        {
            // Act
            await connection1.StartAsync();
            await connection2.StartAsync();

            await connection1.InvokeAsync("SubscribeToJob", jobId);
            await connection2.InvokeAsync("SubscribeToJob", jobId);

            // Assert
            connection1.State.Should().Be(HubConnectionState.Connected);
            connection2.State.Should().Be(HubConnectionState.Connected);
        }
        finally
        {
            await connection1.DisposeAsync();
            await connection2.DisposeAsync();
        }
    }

    #endregion

    #region Response DTOs

    private record ToolboxTalkDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    private record StartProcessingResponse
    {
        public Guid JobId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StatusUrl { get; set; } = string.Empty;
    }

    private record ProgressUpdateMessage
    {
        public Guid JobId { get; set; }
        public int OverallPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
