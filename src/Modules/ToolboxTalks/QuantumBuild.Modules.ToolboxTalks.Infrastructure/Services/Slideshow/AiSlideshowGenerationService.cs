using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Prompts;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Slideshow;

/// <summary>
/// AI-powered slideshow generation service using Claude (Anthropic) API.
/// Sends a PDF document to Claude and receives a complete, self-contained HTML slideshow.
/// </summary>
public class AiSlideshowGenerationService : IAiSlideshowGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<AiSlideshowGenerationService> _logger;

    private static readonly JsonSerializerOptions NullIgnoringJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const string TruncationRetryPrefix =
        "Generate MINIMAL CSS. No comments. Prioritize slide content data completeness over styling.\n\n";

    public AiSlideshowGenerationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<AiSlideshowGenerationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateSlideshowFromPdfAsync(
        byte[] pdfBytes,
        string documentTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return Result.Fail<string>("Claude API key not configured");
            }

            _logger.LogInformation(
                "Generating AI slideshow for document: {Title}, PDF size: {Size} bytes",
                documentTitle, pdfBytes.Length);

            var pdfBase64 = Convert.ToBase64String(pdfBytes);
            var prompt = SlideshowGenerationPrompts.GetPdfSlideshowPrompt();

            string SerializeBody(string p) => JsonSerializer.Serialize(new
            {
                model = _settings.Claude.Model,
                max_tokens = 32000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "document",
                                source = new
                                {
                                    type = "base64",
                                    media_type = "application/pdf",
                                    data = pdfBase64
                                }
                            },
                            new
                            {
                                type = "text",
                                source = (object?)null,
                                text = p
                            }
                        }
                    }
                }
            }, NullIgnoringJsonOptions);

            _logger.LogInformation(
                "Sending PDF to Claude for slideshow generation (document: {Title})",
                documentTitle);

            // First attempt
            var (html, wasTruncated, error) = await SendAndParseAsync(
                SerializeBody(prompt), documentTitle, cancellationToken);

            if (error != null)
                return Result.Fail<string>(error);

            // Retry once on truncation with efficiency instructions
            if (wasTruncated)
            {
                _logger.LogWarning(
                    "Retrying slideshow generation with efficiency instructions for {Title}",
                    documentTitle);

                var (retryHtml, retryTruncated, retryError) = await SendAndParseAsync(
                    SerializeBody(TruncationRetryPrefix + prompt), documentTitle, cancellationToken);

                if (retryError == null && retryHtml != null)
                {
                    html = retryHtml;
                    if (retryTruncated)
                    {
                        _logger.LogError(
                            "Slideshow generation still truncated after retry for {Title}",
                            documentTitle);
                    }
                }
            }

            // Validate HTML completeness
            var validation = ValidateHtml(html, documentTitle);
            if (!validation.Success) return Result.Fail<string>(validation.Errors.First());

            html = InjectPostMessageBridge(html!);

            _logger.LogInformation(
                "Successfully generated HTML slideshow for {Title}, size: {Size} characters",
                documentTitle, html.Length);

            return Result.Ok(html);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during slideshow generation for {Title}", documentTitle);
            return Result.Fail<string>($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI slideshow for document: {Title}", documentTitle);
            return Result.Fail<string>($"Failed to generate slideshow: {ex.Message}");
        }
    }

    public async Task<Result<string>> GenerateSlideshowFromTranscriptAsync(
        string transcriptText,
        string documentTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return Result.Fail<string>("Claude API key not configured");
            }

            _logger.LogInformation(
                "Generating AI slideshow from transcript for document: {Title}, transcript length: {Length} chars",
                documentTitle, transcriptText.Length);

            var prompt = SlideshowGenerationPrompts.GetTranscriptSlideshowPrompt();

            string SerializeBody(string p) => JsonSerializer.Serialize(new
            {
                model = _settings.Claude.Model,
                max_tokens = 32000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Document title: {documentTitle}\n\nVideo Transcript:\n\n{transcriptText}"
                            },
                            new
                            {
                                type = "text",
                                text = p
                            }
                        }
                    }
                }
            });

            _logger.LogInformation(
                "Sending transcript to Claude for slideshow generation (document: {Title})",
                documentTitle);

            // First attempt
            var (html, wasTruncated, error) = await SendAndParseAsync(
                SerializeBody(prompt), documentTitle, cancellationToken);

            if (error != null)
                return Result.Fail<string>(error);

            // Retry once on truncation with efficiency instructions
            if (wasTruncated)
            {
                _logger.LogWarning(
                    "Retrying transcript slideshow generation with efficiency instructions for {Title}",
                    documentTitle);

                var (retryHtml, retryTruncated, retryError) = await SendAndParseAsync(
                    SerializeBody(TruncationRetryPrefix + prompt), documentTitle, cancellationToken);

                if (retryError == null && retryHtml != null)
                {
                    html = retryHtml;
                    if (retryTruncated)
                    {
                        _logger.LogError(
                            "Transcript slideshow generation still truncated after retry for {Title}",
                            documentTitle);
                    }
                }
            }

            // Validate HTML completeness
            var validation = ValidateHtml(html, documentTitle);
            if (!validation.Success) return Result.Fail<string>(validation.Errors.First());

            html = InjectPostMessageBridge(html!);

            _logger.LogInformation(
                "Successfully generated HTML slideshow from transcript for {Title}, size: {Size} characters",
                documentTitle, html.Length);

            return Result.Ok(html);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during transcript slideshow generation for {Title}", documentTitle);
            return Result.Fail<string>($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI slideshow from transcript for document: {Title}", documentTitle);
            return Result.Fail<string>($"Failed to generate slideshow: {ex.Message}");
        }
    }

    public async Task<Result<string>> GenerateSlideshowFromSectionsAsync(
        IReadOnlyList<(string Title, string Content)> sections,
        string documentTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return Result.Fail<string>("Claude API key not configured");
            }

            if (sections.Count == 0)
                return Result.Fail<string>("No sections provided for slideshow generation");

            // Format sections into structured text
            var sb = new StringBuilder();
            sb.AppendLine($"Document title: {documentTitle}");
            sb.AppendLine();
            sb.AppendLine("=== SECTIONS ===");
            sb.AppendLine();
            for (var i = 0; i < sections.Count; i++)
            {
                var (title, content) = sections[i];
                sb.AppendLine($"--- Section {i + 1}: {title} ---");
                sb.AppendLine(content);
                sb.AppendLine();
            }

            var sectionsText = sb.ToString();

            _logger.LogInformation(
                "Generating AI slideshow from {SectionCount} sections for document: {Title}, content length: {Length} chars",
                sections.Count, documentTitle, sectionsText.Length);

            var prompt = SlideshowGenerationPrompts.GetSectionsSlideshowPrompt();

            string SerializeBody(string p) => JsonSerializer.Serialize(new
            {
                model = _settings.Claude.Model,
                max_tokens = 32000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = sectionsText
                            },
                            new
                            {
                                type = "text",
                                text = p
                            }
                        }
                    }
                }
            });

            _logger.LogInformation(
                "Sending sections to Claude for slideshow generation (document: {Title})",
                documentTitle);

            // First attempt
            var (html, wasTruncated, error) = await SendAndParseAsync(
                SerializeBody(prompt), documentTitle, cancellationToken);

            if (error != null)
                return Result.Fail<string>(error);

            // Retry once on truncation with efficiency instructions
            if (wasTruncated)
            {
                _logger.LogWarning(
                    "Retrying sections slideshow generation with efficiency instructions for {Title}",
                    documentTitle);

                var (retryHtml, retryTruncated, retryError) = await SendAndParseAsync(
                    SerializeBody(TruncationRetryPrefix + prompt), documentTitle, cancellationToken);

                if (retryError == null && retryHtml != null)
                {
                    html = retryHtml;
                    if (retryTruncated)
                    {
                        _logger.LogError(
                            "Sections slideshow generation still truncated after retry for {Title}",
                            documentTitle);
                    }
                }
            }

            // Validate HTML completeness
            var validation = ValidateHtml(html, documentTitle);
            if (!validation.Success) return Result.Fail<string>(validation.Errors.First());

            html = InjectPostMessageBridge(html!);

            _logger.LogInformation(
                "Successfully generated HTML slideshow from sections for {Title}, size: {Size} characters",
                documentTitle, html.Length);

            return Result.Ok(html);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during sections slideshow generation for {Title}", documentTitle);
            return Result.Fail<string>($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI slideshow from sections for document: {Title}", documentTitle);
            return Result.Fail<string>($"Failed to generate slideshow: {ex.Message}");
        }
    }

    private async Task<(string? Html, bool WasTruncated, string? Error)> SendAndParseAsync(
        string jsonContent,
        string documentTitle,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.Claude.BaseUrl}/messages");
        request.Headers.Add("x-api-key", _settings.Claude.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.Add("anthropic-beta", "output-128k-2025-02-19");
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Claude API error for slideshow generation: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            return (null, false, $"Claude API error: {response.StatusCode}");
        }

        var (html, stopReason) = ParseApiResponse(responseBody);
        LogTokenUsage(responseBody);

        var wasTruncated = stopReason == "max_tokens";
        if (wasTruncated)
        {
            _logger.LogWarning(
                "Slideshow generation was truncated — output exceeded max_tokens limit for {Title}",
                documentTitle);
        }

        return (html, wasTruncated, null);
    }

    private (string? Html, string? StopReason) ParseApiResponse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        var stopReason = jsonDoc.RootElement.TryGetProperty("stop_reason", out var stopEl)
            ? stopEl.GetString()
            : null;

        string? html = null;
        if (jsonDoc.RootElement.TryGetProperty("content", out var contentArray))
        {
            foreach (var item in contentArray.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var textEl))
                {
                    html = textEl.GetString();
                    break;
                }
            }
        }

        return (html, stopReason);
    }

    private Result ValidateHtml(string? html, string documentTitle)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            _logger.LogWarning("Claude returned empty response for slideshow generation");
            return Result.Fail("AI returned empty response");
        }

        if (!html.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) &&
            !html.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Claude response doesn't appear to be valid HTML: {Preview}",
                html[..Math.Min(200, html.Length)]);
            return Result.Fail("AI response is not valid HTML");
        }

        if (!html.TrimEnd().EndsWith("</html>", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "HTML output is incomplete — missing closing </html> tag for {Title}",
                documentTitle);
            return Result.Fail("AI response is incomplete — HTML output was truncated");
        }

        return Result.Ok();
    }

    /// <summary>
    /// Injects a postMessage bridge script into the generated HTML so the parent React
    /// component can control navigation (goToSlide, nextSlide, prevSlide, getSlideCount).
    /// The bridge tries the AI-generated global functions first, then falls back to
    /// finding and clicking navigation buttons in the DOM.
    /// </summary>
    private static string InjectPostMessageBridge(string html)
    {
        // Insert just before </body> (or </html> as fallback)
        var insertIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (insertIndex < 0)
            insertIndex = html.LastIndexOf("</html>", StringComparison.OrdinalIgnoreCase);
        if (insertIndex < 0)
            return html; // Can't inject — return as-is

        return string.Concat(html.AsSpan(0, insertIndex), PostMessageBridgeScript, html.AsSpan(insertIndex));
    }

    private const string PostMessageBridgeScript = """

    <script>
    /* postMessage bridge — injected by QuantumBuild to enable parent-frame navigation control */
    (function(){
      // Resolve navigation functions from the AI-generated code.
      // The prompt asks for window.goToSlide/nextSlide/prevSlide/getSlideCount,
      // but we try common alternatives for resilience.
      function resolve(names){
        for(var i=0;i<names.length;i++){if(typeof window[names[i]]==='function')return window[names[i]];}
        return null;
      }
      var _goTo=resolve(['goToSlide','showSlide','navigateToSlide','gotoSlide']);
      var _next=resolve(['nextSlide','goNext','nextPage']);
      var _prev=resolve(['prevSlide','previousSlide','goPrev','prevPage']);
      var _count=resolve(['getSlideCount','getTotalSlides','slideCount']);

      // Fallback: try clicking nav buttons if functions not found
      function clickBtn(sel){
        var b=document.querySelector(sel);
        if(b){b.click();return true;}
        return false;
      }
      function fallbackNext(){return clickBtn('[data-nav="next"],.next-btn,.nav-next,button:last-of-type');}
      function fallbackPrev(){return clickBtn('[data-nav="prev"],.prev-btn,.nav-prev,button:first-of-type');}

      // Count slides by checking the slides array or DOM elements
      function countSlides(){
        if(_count)return _count();
        if(window.slides&&window.slides.length)return window.slides.length;
        var dots=document.querySelectorAll('.dot,.slide-dot,[data-slide]');
        return dots.length||1;
      }

      // Detect current slide index from the slides array or active dot
      function currentSlide(){
        if(typeof window.currentSlideIndex==='number')return window.currentSlideIndex;
        if(typeof window.current==='number')return window.current;
        if(typeof window.currentSlide==='number')return window.currentSlide;
        var active=document.querySelector('.dot.active,.slide-dot.active,[data-slide].active');
        if(active&&active.dataset.slide)return parseInt(active.dataset.slide,10);
        return 0;
      }

      function notifyParent(){
        parent.postMessage({type:'slideChanged',current:currentSlide(),total:countSlides()},'*');
      }

      // Listen for commands from the React parent
      window.addEventListener('message',function(e){
        var d=e.data;
        if(!d||!d.type)return;
        if(d.type==='goToSlide'){
          if(_goTo)_goTo(d.slide);
          setTimeout(notifyParent,350);
        }else if(d.type==='nextSlide'){
          if(_next)_next();else fallbackNext();
          setTimeout(notifyParent,350);
        }else if(d.type==='prevSlide'){
          if(_prev)_prev();else fallbackPrev();
          setTimeout(notifyParent,350);
        }else if(d.type==='getSlideCount'){
          notifyParent();
        }
      });

      // Observe slide changes via a MutationObserver on the content area
      var observer=new MutationObserver(function(){setTimeout(notifyParent,400);});
      var target=document.querySelector('.slide-content,.content,main,[class*="slide"]');
      if(target)observer.observe(target,{childList:true,subtree:true,attributes:true});

      // Notify parent on initial load
      setTimeout(notifyParent,500);
    })();
    </script>
    """;

    private void LogTokenUsage(string responseBody)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseBody);
            if (jsonDoc.RootElement.TryGetProperty("usage", out var usageEl))
            {
                var inputTokens = usageEl.TryGetProperty("input_tokens", out var inputEl) ? inputEl.GetInt32() : 0;
                var outputTokens = usageEl.TryGetProperty("output_tokens", out var outputEl) ? outputEl.GetInt32() : 0;
                _logger.LogInformation(
                    "Slideshow generation token usage: input={InputTokens}, output={OutputTokens}, total={TotalTokens}",
                    inputTokens, outputTokens, inputTokens + outputTokens);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse token usage from response");
        }
    }
}
