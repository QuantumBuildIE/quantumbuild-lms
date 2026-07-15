using System.Net;
using System.Text;
using System.Text.Json;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Minimal HttpMessageHandler that returns a canned Anthropic Messages API response for any
/// request, in the exact shape AnthropicResponseParser.Parse expects. Used to test
/// RequirementIngestionJob's Claude extraction step deterministically, without making a real
/// network call and without needing an API key in the test environment.
/// </summary>
public class FakeAnthropicHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    /// The text Claude "replies" with, e.g. "[]" for zero extracted requirements.
    /// </summary>
    public string ResponseContentText { get; set; } = "[]";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = new
        {
            content = new[] { new { type = "text", text = ResponseContentText } },
            usage = new { input_tokens = 10, output_tokens = 5 },
            model = "claude-test-model"
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
