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

    /// <summary>
    /// The Anthropic API's stop_reason for the canned response, e.g. "end_turn" or "max_tokens".
    /// Null omits the field entirely, matching older fixtures that predate stop_reason checks.
    /// </summary>
    public string? StopReason { get; set; }

    /// <summary>
    /// The raw JSON body of the most recent request sent through this handler — lets tests
    /// assert on what was actually sent to Claude (e.g. which candidate requirements were
    /// included in the prompt) without needing a real API call.
    /// </summary>
    public string? CapturedRequestBody { get; private set; }

    /// <summary>
    /// Every request body sent through this handler, in call order — lets tests assert how many
    /// Claude calls were made (e.g. fail-fast segmentation stopping after one failed segment)
    /// and inspect each one (e.g. which principle a segmented prompt targeted).
    /// </summary>
    public List<string> RequestBodies { get; } = new();

    /// <summary>
    /// When set, overrides ResponseContentText/StopReason for every call, computing the response
    /// from the request body instead — lets tests vary the canned response per call (e.g.
    /// RequirementIngestionJob's one-call-per-principle segmentation) rather than returning the
    /// same fixed text for every request the handler ever sees.
    /// </summary>
    public Func<string, (string Text, string? StopReason)>? Responder { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(CapturedRequestBody);
        }

        var (responseText, stopReason) = Responder != null
            ? Responder(CapturedRequestBody ?? string.Empty)
            : (ResponseContentText, StopReason);

        object body = stopReason == null
            ? new
            {
                content = new[] { new { type = "text", text = responseText } },
                usage = new { input_tokens = 10, output_tokens = 5 },
                model = "claude-test-model"
            }
            : new
            {
                content = new[] { new { type = "text", text = responseText } },
                usage = new { input_tokens = 10, output_tokens = 5 },
                model = "claude-test-model",
                stop_reason = stopReason
            };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        return response;
    }
}
