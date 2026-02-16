using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Models;
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
            var prompt = GetSlideshowPrompt();

            var requestBody = new
            {
                model = _settings.Claude.Model,
                max_tokens = 16000,
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
                                text = prompt
                            }
                        }
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.Claude.BaseUrl}/messages");
            request.Headers.Add("x-api-key", _settings.Claude.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation(
                "Sending PDF to Claude for slideshow generation (document: {Title})",
                documentTitle);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Claude API error for slideshow generation: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return Result.Fail<string>($"Claude API error: {response.StatusCode}");
            }

            var html = ExtractHtmlFromResponse(responseBody);

            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.LogWarning("Claude returned empty response for slideshow generation");
                return Result.Fail<string>("AI returned empty response");
            }

            // Validate it looks like HTML
            if (!html.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) &&
                !html.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Claude response doesn't appear to be valid HTML: {Preview}",
                    html[..Math.Min(200, html.Length)]);
                return Result.Fail<string>("AI response is not valid HTML");
            }

            // Log token usage
            LogTokenUsage(responseBody);

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

    private string? ExtractHtmlFromResponse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("content", out var contentArray))
        {
            _logger.LogWarning("No content property found in Claude response");
            return null;
        }

        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString();
            }
        }

        return null;
    }

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

    private static string GetSlideshowPrompt()
    {
        return """
You are a safety training content designer. You will receive a PDF document containing a safety SOP, Code of Practice, Method Statement, Risk Assessment, or procedural document.

Your job is to:
1. Read and analyze the ENTIRE PDF — every single page
2. Extract ALL safety information workers need to know
3. Generate a COMPLETE, self-contained HTML file that presents this information as an animated auto-playing slideshow

## CRITICAL: INFORMATION EXTRACTION PROCESS

Before writing ANY HTML, you MUST complete this extraction checklist. Go through the entire document and extract every item in each category below. If a category is not present in the document, skip it. If it IS present, it MUST appear in the slideshow.

### EXTRACTION CHECKLIST

**A. DOCUMENT IDENTITY** (→ Cover slide)
- Document title / type (Method Statement, SOP, Code of Practice, etc.)
- Company name and contractor details
- Activity / task description
- Project name (if specified)
- Prepared by / reviewed by names and roles
- Document reference numbers

**B. PERSONNEL & EMERGENCY CONTACTS** (→ Dedicated slide)
- Project Manager name + phone
- Safety Advisor name + phone
- Site Supervisor details
- First Aider name + location of first aid box
- Nearest hospital location
- Emergency procedure document references
- All named roles (list every role mentioned in the personnel section)

**C. EVERY RISK ASSESSMENT** (→ One or more slides per RA)
This is the most important extraction. For EACH risk assessment in the document:
- RA reference number (e.g., RA-04, RA-26)
- Area/Task name
- ALL hazards listed
- Risk rating BEFORE controls (e.g., S3×L2=6 HIGH)
- EVERY control measure listed (do not summarize — count them and include them all)
- Risk rating AFTER controls (e.g., S2×L1=2 LOW)
- Who is at risk

Common risk assessments found in construction documents include:
- Housekeeping (slips/trips/falls)
- Biological agents / Leptospirosis
- Manual handling
- Work at height
- Open holes
- Effluent/sewer operations
- Jet Vac / Tanker operations
- Traffic management / roadworks
- Confined spaces
- Electrical hazards
- COSHH / chemical hazards

**D. REQUIRED PPE** (→ Dedicated slide)
- Every PPE item listed anywhere in the document
- Any PPE standards mentioned (e.g., EN471 Class 2 or 3)
- "Other" PPE items (face masks, respirators, etc.)
- Task-specific PPE requirements

**E. EQUIPMENT & PLANT** (→ Dedicated slide)
- All key plant listed (trucks, tankers, pressure washers, etc.)
- All equipment listed (cameras, hoses, tools)
- All materials listed (barriers, signs, etc.)
- Certification/inspection requirements for equipment
- Statutory inspection intervals mentioned
- Safe Working Load (SWL) information

**F. SEQUENCE OF WORKS / METHOD** (→ One or more slides)
- Every step in the work sequence, in order
- Pre-work requirements (inductions, permits, SPA)
- Setup procedures
- Operational procedures
- Completion/exit procedures

**G. TRAINING & PERMITS** (→ Include in relevant slides)
- All required training (Safe Pass, Manual Handling, CSCS cards, WAH, etc.)
- Training renewal intervals (e.g., every 3 years)
- Permits required (permit to work, road closures, etc.)
- Certification requirements (QQI levels, CSCS card types)

**H. HIGH RISKS MATRIX** (→ Dedicated slide or integrated into risk overview)
- Every risk category marked YES in the high risks table
- Every risk category marked NO

**I. SITE LOGISTICS** (→ Include where relevant)
- Access/egress points
- Storage arrangements
- Welfare facilities (washing stations, etc.)
- Parking and setup arrangements

**J. LEGAL / REGULATORY REFERENCES** (→ Include where relevant)
- Regulations cited (e.g., SHWWA Biological Agents Regs 2013)
- Codes of Practice referenced
- Standards referenced (EN471, etc.)
- Company policy documents referenced

## SLIDE PLANNING RULES

After extraction, plan your slides following these rules:

1. **Minimum slides**: Create enough slides to cover ALL extracted content. Typical range is 10-16 slides. DO NOT cap at 12 if more content exists.

2. **Slide allocation priority**:
   - Slide 1: ALWAYS a cover/title slide
   - Slide 2: Emergency contacts and key personnel (if contacts exist in document)
   - Next slides: One slide per HIGH-rated risk assessment (these are most critical)
   - Next slides: One slide per MEDIUM-rated risk assessment (combine 2 related MED risks onto one slide if they're short)
   - Dedicated slide for: PPE requirements
   - Dedicated slide for: Equipment and plant (if substantial equipment list exists)
   - Dedicated slide for: Sequence of works (split into 2 slides if >8 steps)
   - Dedicated slide for: High risks matrix summary with visual risk bars
   - Last slide: ALWAYS a DO's and DON'Ts summary

3. **Never combine unrelated risk assessments** onto one slide. Each RA with distinct hazards gets its own slide.

4. **Control measures are NOT optional**. Every control measure from every RA must appear somewhere. If an RA has 11 control measures, show all 11 (use a scrollable checklist or split across slides if needed).

5. **Numbers and specifics are mandatory**. Include:
   - ALL weight limits (e.g., 25kg)
   - ALL distance measurements (e.g., 2m lanyard)
   - ALL time intervals (e.g., every 3 years, every 6 months)
   - ALL phone numbers
   - ALL risk rating calculations (e.g., S2×L2=4)
   - ALL standards/regulation numbers

## OUTPUT REQUIREMENTS

Return ONLY the complete HTML file. No explanation, no markdown fencing, no preamble. Start with `<!DOCTYPE html>` and end with `</html>`.

## CONTENT FORMATTING RULES

- Keep text CONCISE — max 2 lines per bullet point. Workers on site won't read paragraphs.
- But DO NOT sacrifice completeness for brevity. If an RA has 11 control measures, show all 11 as short bullets.
- Focus on ACTIONABLE information: what to do, what not to do, what to check
- Use ⚠️ emoji markers for HIGH-rated risks
- Use specific numbers, limits, and penalties wherever they appear

## DESIGN SPECIFICATION

The HTML must be a dark-themed, mobile-friendly animated slideshow with these characteristics:

### Layout
- Max width 640px, centered, rounded container with shadow
- Top bar showing: slide icon, slide title, slide counter (e.g., "3 / 14")
- Progress bar under the top bar that fills as slides advance
- Main content area (min-height 520px, scrollable if content overflows) with slide content
- Bottom navigation: Back button, Auto-play/Pause toggle, Next button
- Dot indicators for each slide

### Styling
- Import Google Font: DM Sans (body) and DM Serif Display (headings/numbers)
- Dark backgrounds using CSS gradients — each slide gets a DIFFERENT gradient
- VARY the gradients across slides — use deep blues, purples, dark teals, charcoals, dark reds, dark greens
- Body background: #0a0a0f
- Card/container background: #111
- Text colors: white for headings, rgba(255,255,255,0.75) for body, rgba(255,255,255,0.5) for secondary
- Accent colors — rotate through these across slides: #E63946 (red), #F4A261 (amber), #E76F51 (coral), #2A9D8F (teal), #E9C46A (gold), #264653 (dark teal), #8338EC (purple), #06D6A0 (green)
- Each slide's accent color should be used for: the top bar title, progress bar, check icons, card borders, and the Next button background

### Content Overflow
- The slide content area MUST have `overflow-y: auto` so that slides with many control measures are scrollable
- Add a subtle scroll indicator (gradient fade at bottom) when content overflows

### Animations
Every element must animate in when the slide appears. Use CSS transitions triggered by adding a 'visible' class via JavaScript:

- **Staggered reveals**: Items in lists/grids animate in one by one with increasing delays (0.05–0.1s between items)
- **Stat cards**: Scale from 0.9 to 1.0 + translate up with cubic-bezier(0.34, 1.56, 0.64, 1) for a bouncy feel
- **List items**: Slide in from the right (translateX) with fade
- **Risk/measurement bars**: Width animates from 0% to target with cubic-bezier(0.22, 1, 0.36, 1)
- **Warning boxes**: Scale from 0.95 to 1.0 with fade
- **Cover elements**: Large icon scales from 0.3 to 1.0, title slides up from 40px
- **Slide transitions**: Content fades out (opacity 0 over 300ms), new content builds, then fades in

### Auto-play
- Auto-play button toggles between "▶ Auto-play" and "⏸ Pause"
- When playing: advances every 6 seconds
- Stops automatically on the last slide
- Visual indicator: button changes style when playing (red tinted background)

### Navigation
- Back/Next buttons with disabled states (dimmed when at start/end)
- Clickable dot indicators to jump to any slide
- Active dot is wider (20px vs 8px) and colored with the current slide's accent
- Completed dots are slightly brighter than upcoming dots

## TECHNICAL REQUIREMENTS

- Single self-contained HTML file — NO external dependencies except Google Fonts CDN
- All CSS inline in a `<style>` tag
- All JavaScript inline in a `<script>` tag
- Must work on mobile browsers (responsive, touch-friendly buttons min 44px tap target)
- No frameworks — vanilla HTML/CSS/JS only
- Store slides as a JavaScript array of objects
- Use a single render function that builds HTML from the slide data
- Animations triggered by adding CSS classes after a requestAnimationFrame or setTimeout

## SLIDE DATA STRUCTURE

Store all slides in a JS array. Each slide object should have:
```
{
  id: 0,
  title: "Slide Title",
  icon: "emoji",
  color: "#hexAccent",
  bgGrad: "linear-gradient(135deg, #dark1 0%, #dark2 100%)",
  type: "cover|contacts|stats|checklist|warning|equipment|risks|dos|riskdetail",
  // Plus type-specific fields
}
```

Slide Types Available:
- **cover**: Title slide with document name, company, badge
- **contacts**: Emergency contacts grid with names, phone numbers, roles
- **stats**: Key risk rating cards in a grid (use for HIGH risk overview)
- **checklist**: Numbered step-by-step items (use for sequence of works, control measures)
- **warning**: Alert-styled boxes with critical hazard info + bullet points
- **equipment**: Icon cards in a grid for PPE or plant/tools
- **risks**: Animated progress bars showing risk levels before/after controls
- **riskdetail**: Detailed risk assessment view with hazards, controls, ratings
- **dos**: Two-column DO's and DON'Ts summary

## COMPLETENESS VERIFICATION

Before finalizing the HTML, mentally verify:
- [ ] Every Risk Assessment reference number appears
- [ ] Every control measure from every RA is included
- [ ] All emergency phone numbers are shown
- [ ] All PPE items are listed
- [ ] All equipment/plant is mentioned
- [ ] All training requirements are specified with intervals
- [ ] All weight/distance/time limits are stated
- [ ] The sequence of works is complete
- [ ] First aid location and nearest hospital are mentioned
- [ ] All regulatory/standards references are included
- [ ] High risks YES/NO matrix is represented

## WHAT MAKES A GREAT RESULT

- A site worker with 30 seconds per slide should understand the key safety points
- ALL information from the source document is captured — nothing is lost
- The animations make it feel professional and engaging, not like a boring PDF
- Critical warnings (HIGH rated) STAND OUT with red-tinted borders and alert styling
- Numbers, limits, and phone numbers are LARGE and prominent
- The auto-play mode works as a hands-free briefing display
- It looks polished on a phone screen held in portrait orientation
- A safety auditor comparing the slideshow to the source document would find zero missing information
""";
    }
}
