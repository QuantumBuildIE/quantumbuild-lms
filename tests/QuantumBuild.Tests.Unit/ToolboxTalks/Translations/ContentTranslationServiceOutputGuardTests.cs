using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using QuantumBuild.Core.Application.Abstractions.AI;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Translations;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Translations;

/// <summary>
/// Characterisation tests for the translation output guard (reasoning-leak fix) in ContentTranslationService.
/// </summary>
public class ContentTranslationServiceOutputGuardTests
{
    // Source: a section long enough for the length-ratio check to apply.
    private const string HtmlSource =
        "<p>Always wear your hard hat and safety boots on site.</p><p>Report every hazard to your supervisor immediately.</p>";

    private const string PlainSource =
        "Always wear your hard hat and safety boots on site. Report every hazard to your supervisor immediately.";

    // Real Production output (Sonnet 4.6, Afrikaans, plain-text section labelled "HTML content").
    private const string LeakedAfrikaans = """
        The user wants me to translate HTML content to Afrikaans. However, the content provided doesn't appear to have HTML tags - it's just plain text. Let me re-read the instructions. "Translate the following en HTML content to Afrikaans. Keep all HTML tags exactly as they are. Only translate the text content between tags." The content seems to be plain text without HTML tags. I'll translate it as-is, treating it as text content. Wait, let me re-read - "Return ONLY the translated HTML content". So I should return the translated text (which has no HTML tags to preserve). Let me translate this properly: "When dropping a trailer, it is important to reverse into one available parking spot carefully and position the trailer in the center of a single spot." "Wanneer 'n sleepwa afgehaak word, is dit belangrik om versigtig in een beskikbare parkeerplekkie in te ry en die sleepwa in die middel van 'n enkele plek te posisioneer." Let me think more carefully: - "dropping a trailer" = "aflaai van 'n sleepwa" or "afsit van 'n sleepwa" - in trucking context, "dropping a trailer" means unhooking/leaving a trailer. In Afrikaans, this could be "afsit van 'n sleepwa" or "los van 'n sleepwa" - "reverse into" = "terugtrek in" or "inry (agteruit)" - "parking spot" = "parkeerplek" - "position the trailer in the center" = "die sleepwa in die middel posisioneer" - "single spot" = "enkele plek" - "adjacent spot" = "aangrensende plek" - "landing legs" = "landingsbene" or "ondersteuningsbene" - these are the legs that support a trailer when it's disconnected from the truck - "yard" = "werf" (in a logistics/trucking context) Let me translate: "Wanneer 'n sleepwa afgesit word, is dit belangrik om versigtig agteruit in een beskikbare parkeerplek in te ry en die sleepwa in die middel van 'n enkele plek te posisioneer. Dit verseker dat slegs een parkeerplek gebruik word, sodat die aangrensende plek volledig beskikbaar bly vir 'n ander sleepwa. Behoorlike posisionering beteken ook dat daar genoeg afstand gehandhaaf word van enige naburige sleepwaens om veilige toegang tot die landingsbene te verseker. Deur hierdie prosedure te volg, bly die werf georganiseer en doeltreffend vir alle bestuurders." Let me review: - "dropping a trailer" → "afgesit word" (a trailer is dropped/set down) - this works - "reverse into one available parking spot carefully" → "versigtig agteruit in een beskikbare parkeerplek in te ry" - good - "position the trailer in the center of a single spot" → "die sleepwa in die middel van 'n enkele plek te posisioneer" - good - "This ensures that only one parking space is used" → "Dit verseker dat slegs een parkeerplek gebruik word" - good - "leaving the adjacent spot fully available for another trailer" → "sodat die aangrensende plek volledig beskikbaar bly vir 'n ander sleepwa" - good - "Proper positioning also means maintaining enough distance from any neighboring trailers" → "Behoorlike posisionering beteken ook dat daar genoeg afstand gehandhaaf word van enige naburige sleepwaens" - good - "to allow safe access to the landing legs" → "om veilige toegang tot die landingsbene te verseker" - good - "Following this procedure keeps the yard organized and efficient for all drivers" → "Deur hierdie prosedure te volg, bly die werf georganiseer en doeltreffend vir alle bestuurders" - good This looks good. Since there are no HTML tags in the original, I'll just return the translated text. Actually, wait - "sleepwa" vs "sleepwaens" - let me be consistent. Singular: sleepwa, Plural: sleepwaens. That's correct. Also "neighboring trailers" - I used "naburige sleepwaens" which is good. The translation looks natural and professional. Let me finalize it. Wanneer 'n sleepwa afgesit word, is dit belangrik om versigtig agteruit in een beskikbare parkeerplek in te ry en die sleepwa in die middel van 'n enkele plek te posisioneer. Dit verseker dat slegs een parkeerplek gebruik word, sodat die aangrensende plek volledig beskikbaar bly vir 'n ander sleepwa. Behoorlike posisionering beteken ook dat daar genoeg afstand van enige naburige sleepwaens gehandhaaf moet word om veilige toegang tot die landingsbene te verseker. Deur hierdie prosedure te volg, bly die werf georganiseer en doeltreffend vir alle bestuurders.
        """;

    private const string TrailerSource =
        "When dropping a trailer, it is important to reverse into one available parking spot carefully and position the trailer in the center of a single spot. " +
        "This ensures that only one parking space is used, leaving the adjacent spot fully available for another trailer. " +
        "Proper positioning also means maintaining enough distance from any neighboring trailers to allow safe access to the landing legs. " +
        "Following this procedure keeps the yard organized and efficient for all drivers.";

    private const string CleanTrailer =
        "Wanneer 'n sleepwa afgesit word, is dit belangrik om versigtig agteruit in een beskikbare parkeerplek in te ry en die sleepwa in die middel van 'n enkele plek te posisioneer. " +
        "Dit verseker dat slegs een parkeerplek gebruik word, sodat die aangrensende plek volledig beskikbaar bly vir 'n ander sleepwa. " +
        "Behoorlike posisionering beteken ook dat daar genoeg afstand van enige naburige sleepwaens gehandhaaf moet word om veilige toegang tot die landingsbene te verseker. " +
        "Deur hierdie prosedure te volg, bly die werf georganiseer en doeltreffend vir alle bestuurders.";

    private const string CleanHtml =
        "<p>Dra altyd jou veiligheidshelm en veiligheidstewels op die terrein.</p><p>Rapporteer elke gevaar onmiddellik aan jou toesighouer.</p>";

    private readonly Mock<HttpMessageHandler> _handler = new();
    private readonly Mock<ILogger<ContentTranslationService>> _logger = new();
    private readonly List<string> _requestBodies = new();
    private readonly Queue<string> _responses = new();

    private ContentTranslationService CreateService()
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                _requestBodies.Add(await req.Content!.ReadAsStringAsync(ct));
                var text = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
                var json = JsonSerializer.Serialize(new { content = new[] { new { type = "text", text } } });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            });

        var settings = Options.Create(new SubtitleProcessingSettings
        {
            Claude = new ClaudeSettings { ApiKey = "k", MaxTokens = 4000, BaseUrl = "https://api.anthropic.com/v1" }
        });
        var providers = Options.Create(new AIProviderOptions
        {
            Anthropic = new AnthropicProviderOptions { Models = new AnthropicModels { Sonnet = "claude-sonnet-4-5" } }
        });

        return new ContentTranslationService(
            new HttpClient(_handler.Object),
            settings,
            providers,
            new Mock<IAiUsageLogger>().Object,
            new Mock<IApplicableFrameworksService>().Object,
            _logger.Object);
    }

    private void Respond(params string[] outputs)
    {
        foreach (var o in outputs) _responses.Enqueue(o);
    }

    private void VerifyLogged(LogLevel level, Times times) =>
        _logger.Verify(l => l.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), times);

    [Fact]
    public async Task RealLeakedAfrikaansOutput_OnBothAttempts_IsRejected()
    {
        Respond(LeakedAfrikaans);
        var result = await CreateService().TranslateTextAsync(TrailerSource, "Afrikaans", isHtml: true);

        result.Success.Should().BeFalse();
        result.TranslatedContent.Should().BeNullOrEmpty();
    }

    [Fact]
    public void RealLeakedAfrikaansOutput_IsRejectedByGuard_AndCleanTrailerIsAccepted()
    {
        TranslationOutputGuard.GetRejectionReason(TrailerSource, LeakedAfrikaans).Should().NotBeNull();
        TranslationOutputGuard.GetRejectionReason(TrailerSource, CleanTrailer).Should().BeNull();
    }

    [Fact]
    public async Task CleanTranslation_ContainingGenericMarkerMidSentence_IsAccepted()
    {
        var clean = "Jy moet altyd jou helm dra. In Engels sê ons: I need to wear a helmet, maar hier is die Afrikaanse reël: dra altyd jou veiligheidshelm op die terrein.";
        Respond(clean);
        var result = await CreateService().TranslateTextAsync(
            "You must always wear your helmet. Always wear your safety helmet on site.", "Afrikaans");

        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Be(clean);
    }

    [Fact]
    public async Task GenericMarkerAtStartOfLine_IsRejected()
    {
        Respond("<p>Dra altyd jou helm.</p>\nI need to translate the next paragraph too.\n<p>Rapporteer gevaar.</p>");
        var result = await CreateService().TranslateTextAsync(HtmlSource, "Afrikaans");

        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData("Let me translate this HTML content to Polish: <p>Zawsze noś kask i buty ochronne na terenie budowy.</p><p>Zgłaszaj każde zagrożenie.</p>")]
    [InlineData("Dlaczego pytanie? Wait, let me re-translate properly: Czy zawsze nosisz kask ochronny na budowie?")]
    public async Task ReasoningMarkers_AreRejected(string leaked)
    {
        Respond(leaked);
        var result = await CreateService().TranslateTextAsync(
            "Do you always wear your hard hat and safety boots on site?", "Polish");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CleanTranslation_OfSimilarLength_IsAccepted()
    {
        Respond(CleanHtml);
        var result = await CreateService().TranslateTextAsync(HtmlSource, "Afrikaans");

        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Be(CleanHtml);
        _requestBodies.Should().HaveCount(1);
    }

    [Fact]
    public async Task OutputThreeTimesSourceLength_IsRejected()
    {
        Respond(new string('a', PlainSource.Length * 3));
        var result = await CreateService().TranslateTextAsync(PlainSource, "Afrikaans");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task OutputOnePointFourTimesSourceLength_IsAccepted()
    {
        var output = new string('a', (int)(PlainSource.Length * 1.4));
        Respond(output);
        var result = await CreateService().TranslateTextAsync(PlainSource, "Afrikaans");

        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Be(output);
    }

    [Fact]
    public async Task LeakThenCleanOnRetry_PersistsCleanAndUsesStricterPrompt()
    {
        Respond(LeakedAfrikaans, CleanHtml);
        var result = await CreateService().TranslateTextAsync(HtmlSource, "Afrikaans", toolboxTalkId: Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.TranslatedContent.Should().Be(CleanHtml);
        _requestBodies.Should().HaveCount(2);
        _requestBodies[1].Should().Contain("previous response contained commentary");
        _requestBodies[0].Should().NotContain("previous response contained commentary");
        VerifyLogged(LogLevel.Warning, Times.AtLeastOnce());
    }

    [Fact]
    public async Task LeakTwice_PersistsNothingAndLogsError()
    {
        Respond(LeakedAfrikaans);
        var result = await CreateService().TranslateTextAsync(HtmlSource, "Afrikaans", toolboxTalkId: Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.TranslatedContent.Should().BeNullOrEmpty();
        _requestBodies.Should().HaveCount(2);
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task EveryRequest_CarriesTranslationEngineSystemPrompt()
    {
        Respond(CleanHtml);
        await CreateService().TranslateTextAsync(HtmlSource, "Afrikaans");

        using var doc = JsonDocument.Parse(_requestBodies[0]);
        doc.RootElement.GetProperty("system").GetString().Should().Contain("translation engine");
    }

    [Fact]
    public async Task PlainTextSection_GetsPlainTextPrompt_EvenWhenCallerSaysHtml()
    {
        Respond("Dra altyd jou veiligheidshelm en veiligheidstewels op die terrein. Rapporteer elke gevaar onmiddellik.");
        await CreateService().TranslateTextAsync(PlainSource, "Afrikaans", isHtml: true);

        _requestBodies[0].Should().NotContain("HTML content");
        _requestBodies[0].Should().NotContain("Keep all HTML tags");
    }

    [Fact]
    public async Task HtmlSection_GetsHtmlPrompt_EvenWhenCallerSaysPlain()
    {
        Respond(CleanHtml);
        await CreateService().TranslateTextAsync(HtmlSource, "Afrikaans", isHtml: false);

        _requestBodies[0].Should().Contain("HTML");
        _requestBodies[0].Should().Contain("Keep all HTML tags");
    }
}
