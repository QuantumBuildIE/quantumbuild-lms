using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;

/// <summary>
/// Handler for generating content translations for toolbox talk sections and questions.
/// </summary>
public class GenerateContentTranslationsCommandHandler
    : IRequestHandler<GenerateContentTranslationsCommand, GenerateContentTranslationsResult>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly IContentTranslationService _translationService;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ILogger<GenerateContentTranslationsCommandHandler> _logger;

    public GenerateContentTranslationsCommandHandler(
        IToolboxTalksDbContext context,
        IContentTranslationService translationService,
        ILanguageCodeService languageCodeService,
        ILogger<GenerateContentTranslationsCommandHandler> logger)
    {
        _context = context;
        _translationService = translationService;
        _languageCodeService = languageCodeService;
        _logger = logger;
    }

    public async Task<GenerateContentTranslationsResult> Handle(
        GenerateContentTranslationsCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "GenerateContentTranslationsCommandHandler started. " +
            "ToolboxTalkId: {ToolboxTalkId}, TenantId: {TenantId}, Languages: {Languages}",
            request.ToolboxTalkId, request.TenantId, string.Join(", ", request.TargetLanguages));

        // NOTE: IgnoreQueryFilters() because this handler runs in a Hangfire background job context
        // where the DbContext TenantId may not be set. We filter by TenantId explicitly.
        var toolboxTalk = await _context.ToolboxTalks
            .IgnoreQueryFilters()
            .Include(t => t.Sections.Where(s => !s.IsDeleted).OrderBy(s => s.SectionNumber))
            .Include(t => t.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.QuestionNumber))
            .Include(t => t.Translations.Where(tr => !tr.IsDeleted))
            .Include(t => t.Slides.Where(s => !s.IsDeleted).OrderBy(s => s.PageNumber))
                .ThenInclude(s => s.Translations)
            .Include(t => t.SlideshowTranslations)
            .FirstOrDefaultAsync(t => t.Id == request.ToolboxTalkId
                && t.TenantId == request.TenantId
                && !t.IsDeleted, cancellationToken);

        if (toolboxTalk == null)
        {
            _logger.LogWarning(
                "ToolboxTalk {ToolboxTalkId} not found for TenantId {TenantId}",
                request.ToolboxTalkId, request.TenantId);
            return GenerateContentTranslationsResult.FailureResult("Toolbox talk not found");
        }

        // Get the source language name from the code
        var sourceLanguageName = _languageCodeService.GetLanguageName(toolboxTalk.SourceLanguageCode);

        _logger.LogInformation(
            "Loaded ToolboxTalk '{Title}'. SourceLanguage: {SourceLanguage} ({SourceCode}), " +
            "Sections: {SectionCount}, Questions: {QuestionCount}, " +
            "Existing translations: {TranslationCount}, Slides with text: {SlideCount}",
            toolboxTalk.Title, sourceLanguageName, toolboxTalk.SourceLanguageCode,
            toolboxTalk.Sections.Count, toolboxTalk.Questions.Count,
            toolboxTalk.Translations.Count,
            toolboxTalk.Slides.Count(s => !string.IsNullOrEmpty(s.OriginalText)));

        var results = new List<LanguageTranslationResult>();

        foreach (var language in request.TargetLanguages)
        {
            // Skip translation if target language is the same as source language
            var targetCode = _languageCodeService.GetLanguageCode(language);
            if (string.Equals(targetCode, toolboxTalk.SourceLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Skipping translation to {Language} ({Code}) - same as source language",
                    language, targetCode);
                continue;
            }

            _logger.LogInformation("Starting translation for language: {Language} (from {Source})", language, sourceLanguageName);
            var result = await TranslateForLanguageAsync(toolboxTalk, language, sourceLanguageName, cancellationToken);
            _logger.LogInformation(
                "Translation result for {Language}: Success={Success}, " +
                "SectionsTranslated={Sections}, QuestionsTranslated={Questions}, " +
                "SlidesTranslated={Slides}, SlideshowTranslated={Slideshow}, Error={Error}",
                language, result.Success, result.SectionsTranslated, result.QuestionsTranslated,
                result.SlidesTranslated, result.SlideshowTranslated, result.ErrorMessage ?? "none");
            results.Add(result);
        }

        _logger.LogInformation(
            "All translations done. Saving translation entities to database...");

        try
        {
            var rowsAffected = await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "SaveChangesAsync completed. Rows affected: {RowsAffected}. " +
                "Content translation completed for ToolboxTalk {ToolboxTalkId}. Success: {SuccessCount}/{TotalCount}",
                rowsAffected,
                request.ToolboxTalkId,
                results.Count(r => r.Success),
                results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SaveChangesAsync FAILED for translations. " +
                "ToolboxTalkId: {ToolboxTalkId}, TenantId: {TenantId}, Error: {Error}",
                request.ToolboxTalkId, request.TenantId, ex.Message);
            throw;
        }

        return GenerateContentTranslationsResult.SuccessResult(results);
    }

    private async Task<LanguageTranslationResult> TranslateForLanguageAsync(
        ToolboxTalk toolboxTalk,
        string language,
        string sourceLanguage,
        CancellationToken cancellationToken)
    {
        var languageCode = _languageCodeService.GetLanguageCode(language);

        _logger.LogInformation(
            "Translating ToolboxTalk {ToolboxTalkId} to {Language} ({LanguageCode})",
            toolboxTalk.Id, language, languageCode);

        try
        {
            // Find existing translation or create new one
            var translation = toolboxTalk.Translations
                .FirstOrDefault(t => t.LanguageCode == languageCode);

            var isNew = translation == null;
            translation ??= new ToolboxTalkTranslation
            {
                Id = Guid.NewGuid(),
                TenantId = toolboxTalk.TenantId,
                ToolboxTalkId = toolboxTalk.Id,
                LanguageCode = languageCode
            };

            // Translate title (required - skip this language entirely if title fails)
            var titleResult = await _translationService.TranslateTextAsync(
                toolboxTalk.Title, language, false, cancellationToken, sourceLanguage);

            if (!titleResult.Success)
            {
                _logger.LogWarning(
                    "Title translation failed for ToolboxTalk {ToolboxTalkId} to {Language}: {Error}. " +
                    "Skipping this language entirely.",
                    toolboxTalk.Id, language, titleResult.ErrorMessage);

                // Don't add an entity with empty data - skip this language
                return new LanguageTranslationResult
                {
                    Language = language,
                    LanguageCode = languageCode,
                    Success = false,
                    ErrorMessage = $"Failed to translate title: {titleResult.ErrorMessage}"
                };
            }

            translation.TranslatedTitle = titleResult.TranslatedContent;

            // Translate description if present
            if (!string.IsNullOrWhiteSpace(toolboxTalk.Description))
            {
                var descResult = await _translationService.TranslateTextAsync(
                    toolboxTalk.Description, language, false, cancellationToken, sourceLanguage);

                translation.TranslatedDescription = descResult.Success
                    ? descResult.TranslatedContent
                    : null;

                if (!descResult.Success)
                {
                    _logger.LogWarning(
                        "Description translation failed for ToolboxTalk {ToolboxTalkId} to {Language}: {Error}",
                        toolboxTalk.Id, language, descResult.ErrorMessage);
                }
            }

            // Translate sections
            var sectionsTranslated = 0;
            var translatedSections = new List<TranslatedSectionData>();

            foreach (var section in toolboxTalk.Sections)
            {
                var sectionTitleResult = await _translationService.TranslateTextAsync(
                    section.Title, language, false, cancellationToken, sourceLanguage);

                var sectionContentResult = await _translationService.TranslateTextAsync(
                    section.Content, language, true, cancellationToken, sourceLanguage);

                if (sectionTitleResult.Success && sectionContentResult.Success)
                {
                    translatedSections.Add(new TranslatedSectionData
                    {
                        SectionId = section.Id,
                        Title = sectionTitleResult.TranslatedContent,
                        Content = sectionContentResult.TranslatedContent
                    });
                    sectionsTranslated++;
                }
                else
                {
                    _logger.LogWarning(
                        "Section {SectionId} translation failed for {Language}. Title: {TitleSuccess}, Content: {ContentSuccess}",
                        section.Id, language, sectionTitleResult.Success, sectionContentResult.Success);
                }
            }

            translation.TranslatedSections = JsonSerializer.Serialize(translatedSections);

            // Translate questions - save partial results even if some questions fail
            var questionsTranslated = 0;
            var questionsSkipped = 0;
            var translatedQuestions = new List<TranslatedQuestionData>();

            foreach (var question in toolboxTalk.Questions)
            {
                var questionTextResult = await _translationService.TranslateTextAsync(
                    question.QuestionText, language, false, cancellationToken, sourceLanguage);

                if (!questionTextResult.Success)
                {
                    _logger.LogWarning(
                        "Question {QuestionId} text translation failed for {Language}: {Error}. " +
                        "Skipping question, will fall back to English.",
                        question.Id, language, questionTextResult.ErrorMessage);
                    questionsSkipped++;
                    continue;
                }

                List<string>? translatedOptions = null;
                var allOptionsTranslated = true;

                if (!string.IsNullOrWhiteSpace(question.Options))
                {
                    try
                    {
                        var options = JsonSerializer.Deserialize<List<string>>(question.Options);
                        if (options != null && options.Count > 0)
                        {
                            translatedOptions = new List<string>();
                            foreach (var option in options)
                            {
                                var optionResult = await _translationService.TranslateTextAsync(
                                    option, language, false, cancellationToken, sourceLanguage);
                                if (optionResult.Success)
                                {
                                    translatedOptions.Add(optionResult.TranslatedContent);
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "Question {QuestionId} option translation failed for {Language}. " +
                                        "Skipping question, will fall back to English.",
                                        question.Id, language);
                                    allOptionsTranslated = false;
                                    break;
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Failed to parse options for question {QuestionId}", question.Id);
                        allOptionsTranslated = false;
                    }
                }

                if (allOptionsTranslated)
                {
                    translatedQuestions.Add(new TranslatedQuestionData
                    {
                        QuestionId = question.Id,
                        QuestionText = questionTextResult.TranslatedContent,
                        Options = translatedOptions
                    });
                    questionsTranslated++;
                }
                else
                {
                    questionsSkipped++;
                }
            }

            translation.TranslatedQuestions = JsonSerializer.Serialize(translatedQuestions);

            // Update metadata
            translation.TranslatedAt = DateTime.UtcNow;
            translation.TranslationProvider = "Claude";

            // Generate translated email templates
            var emailSubjectResult = await _translationService.TranslateTextAsync(
                $"Action Required: Complete Toolbox Talk - {toolboxTalk.Title}",
                language, false, cancellationToken, sourceLanguage);
            translation.EmailSubject = emailSubjectResult.Success
                ? emailSubjectResult.TranslatedContent
                : $"Action Required: Complete Toolbox Talk - {translation.TranslatedTitle}";

            var emailBodyResult = await _translationService.TranslateTextAsync(
                $"You have been assigned a new toolbox talk: {toolboxTalk.Title}. Please complete it by the due date.",
                language, false, cancellationToken, sourceLanguage);
            translation.EmailBody = emailBodyResult.Success
                ? emailBodyResult.TranslatedContent
                : $"You have been assigned a new toolbox talk: {translation.TranslatedTitle}. Please complete it by the due date.";

            // Translate slide text
            var slidesTranslated = 0;
            var slidesWithText = toolboxTalk.Slides
                .Where(s => !string.IsNullOrEmpty(s.OriginalText))
                .ToList();

            foreach (var slide in slidesWithText)
            {
                // Skip if already translated to this language
                if (slide.Translations.Any(t => t.LanguageCode == languageCode))
                {
                    slidesTranslated++;
                    continue;
                }

                var slideTextResult = await _translationService.TranslateTextAsync(
                    slide.OriginalText!, language, false, cancellationToken, sourceLanguage);

                if (slideTextResult.Success && !string.IsNullOrEmpty(slideTextResult.TranslatedContent))
                {
                    var slideTranslation = new ToolboxTalkSlideTranslation
                    {
                        Id = Guid.NewGuid(),
                        SlideId = slide.Id,
                        LanguageCode = languageCode,
                        TranslatedText = slideTextResult.TranslatedContent
                    };
                    _context.ToolboxTalkSlideTranslations.Add(slideTranslation);
                    slidesTranslated++;
                }
                else
                {
                    _logger.LogWarning(
                        "Slide {SlideId} (page {PageNumber}) translation failed for {Language}: {Error}",
                        slide.Id, slide.PageNumber, language, slideTextResult.ErrorMessage);
                }
            }

            // Translate HTML slideshow if it exists
            var slideshowTranslated = false;
            _logger.LogInformation(
                "Slideshow translation check for talk {TalkId}: HasSlideshowHtml={HasHtml}, Length={Length}",
                toolboxTalk.Id,
                !string.IsNullOrEmpty(toolboxTalk.SlideshowHtml),
                toolboxTalk.SlideshowHtml?.Length ?? 0);
            if (!string.IsNullOrEmpty(toolboxTalk.SlideshowHtml))
            {
                slideshowTranslated = await TranslateSlideshowAsync(
                    toolboxTalk, languageCode, language, sourceLanguage, cancellationToken);
            }

            // Explicitly add to DbSet so the change tracker registers it as Added.
            // Using navigation collection Add alone is unreliable when the parent entity
            // was already tracked by a previous operation (content generation) on the same
            // scoped DbContext - DetectChanges() may not pick up the new entity.
            if (isNew)
            {
                _context.ToolboxTalkTranslations.Add(translation);
                _logger.LogInformation(
                    "Added new translation entity for {Language} ({LanguageCode}) via DbSet.Add(). " +
                    "Sections: {Sections}, Questions: {Questions} (skipped: {Skipped}), Slides: {Slides}, Slideshow: {Slideshow}",
                    language, languageCode, sectionsTranslated, questionsTranslated, questionsSkipped, slidesTranslated, slideshowTranslated);
            }
            else
            {
                _logger.LogInformation(
                    "Updated existing translation entity for {Language} ({LanguageCode}). " +
                    "Sections: {Sections}, Questions: {Questions} (skipped: {Skipped}), Slides: {Slides}, Slideshow: {Slideshow}",
                    language, languageCode, sectionsTranslated, questionsTranslated, questionsSkipped, slidesTranslated, slideshowTranslated);
            }

            return new LanguageTranslationResult
            {
                Language = language,
                LanguageCode = languageCode,
                Success = true,
                SectionsTranslated = sectionsTranslated,
                QuestionsTranslated = questionsTranslated,
                SlidesTranslated = slidesTranslated,
                SlideshowTranslated = slideshowTranslated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate ToolboxTalk {ToolboxTalkId} to {Language}",
                toolboxTalk.Id, language);

            return new LanguageTranslationResult
            {
                Language = language,
                LanguageCode = languageCode,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<bool> TranslateSlideshowAsync(
        ToolboxTalk toolboxTalk,
        string languageCode,
        string targetLanguageName,
        string sourceLanguageName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if translation already exists
            var existingTranslation = toolboxTalk.SlideshowTranslations
                .FirstOrDefault(t => t.LanguageCode == languageCode);

            if (existingTranslation != null)
            {
                _logger.LogInformation(
                    "Slideshow translation for {Lang} already exists, skipping",
                    languageCode);
                return true;
            }

            _logger.LogInformation("Translating slideshow HTML to {Lang}. Source HTML length: {HtmlLength}",
                languageCode, toolboxTalk.SlideshowHtml!.Length);

            // Extract the slides array from the HTML
            var slidesText = ExtractSlidesArrayFromHtml(toolboxTalk.SlideshowHtml!);
            if (string.IsNullOrEmpty(slidesText))
            {
                _logger.LogWarning(
                    "Could not extract slides array from HTML for translation to {Lang}. " +
                    "Regex failed to match 'const slides = [...]' pattern. HTML preview: {Preview}",
                    languageCode,
                    toolboxTalk.SlideshowHtml!.Length > 300
                        ? toolboxTalk.SlideshowHtml![..300]
                        : toolboxTalk.SlideshowHtml!);
                return false;
            }

            // Find all translatable strings in the JavaScript object notation
            var translatableStrings = FindTranslatableStrings(slidesText);
            var uniqueStrings = translatableStrings.Select(t => t.Value).Distinct().ToList();

            _logger.LogInformation(
                "Slideshow translation for {Lang}: Found {Total} translatable string occurrences, " +
                "{Unique} unique strings to translate. SlidesText length: {Length}",
                languageCode, translatableStrings.Count, uniqueStrings.Count, slidesText.Length);

            if (uniqueStrings.Count == 0)
            {
                _logger.LogWarning(
                    "No translatable strings found in slideshow for {Lang}. " +
                    "SlidesText preview: {Preview}",
                    languageCode,
                    slidesText.Length > 300 ? slidesText[..300] : slidesText);
                return false;
            }

            // Translate each unique string individually via TranslateTextAsync
            var translations = new Dictionary<string, string>();
            var translated = 0;
            var failed = 0;

            foreach (var originalRaw in uniqueStrings)
            {
                var textToTranslate = UnescapeJsString(originalRaw);

                var result = await _translationService.TranslateTextAsync(
                    textToTranslate, targetLanguageName, false, cancellationToken, sourceLanguageName);

                if (result.Success && !string.IsNullOrEmpty(result.TranslatedContent))
                {
                    translations[originalRaw] = result.TranslatedContent;
                    translated++;
                }
                else
                {
                    _logger.LogWarning(
                        "Slideshow string translation failed for {Lang}. " +
                        "Original: '{Original}', Error: {Error}",
                        languageCode,
                        textToTranslate.Length > 80 ? textToTranslate[..80] + "..." : textToTranslate,
                        result.ErrorMessage ?? "empty result");
                    failed++;
                }
            }

            _logger.LogInformation(
                "Slideshow string translations for {Lang}: {Translated} succeeded, {Failed} failed out of {Total} unique strings",
                languageCode, translated, failed, uniqueStrings.Count);

            if (translated == 0)
            {
                _logger.LogWarning(
                    "All slideshow string translations failed for {Lang}. Skipping slideshow translation.",
                    languageCode);
                return false;
            }

            // Replace strings in the slides text (from end to start to preserve positions)
            var sb = new StringBuilder(slidesText);
            foreach (var match in translatableStrings.OrderByDescending(t => t.Index))
            {
                if (translations.TryGetValue(match.Value, out var translatedText))
                {
                    var escapedTranslation = $"\"{EscapeJsString(translatedText)}\"";
                    sb.Remove(match.Index, match.Length);
                    sb.Insert(match.Index, escapedTranslation);
                }
            }

            var translatedSlidesText = sb.ToString();

            // Replace the slides array in the HTML
            var translatedHtml = ReplaceSlidesArrayInHtml(toolboxTalk.SlideshowHtml!, translatedSlidesText);

            _logger.LogInformation(
                "Slideshow HTML replacement for {Lang}: OriginalHtmlLength={Original}, " +
                "TranslatedHtmlLength={Translated}, LengthDiff={Diff}",
                languageCode, toolboxTalk.SlideshowHtml!.Length,
                translatedHtml.Length,
                translatedHtml.Length - toolboxTalk.SlideshowHtml!.Length);

            // Save the translation using DbSet.Add() for reliable change tracking
            var slideshowTranslation = new ToolboxTalkSlideshowTranslation
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalk.Id,
                LanguageCode = languageCode,
                TranslatedHtml = translatedHtml,
                TranslatedAt = DateTime.UtcNow
            };

            _context.ToolboxTalkSlideshowTranslations.Add(slideshowTranslation);

            _logger.LogInformation(
                "Slideshow translation entity ADDED to DbContext for {Lang}. " +
                "EntityId={EntityId}, ToolboxTalkId={TalkId}, HTML length: {Length}, " +
                "Strings translated: {Translated}/{Total}",
                languageCode, slideshowTranslation.Id, toolboxTalk.Id, translatedHtml.Length,
                translated, uniqueStrings.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error translating slideshow to {Lang} for ToolboxTalk {ToolboxTalkId}",
                languageCode, toolboxTalk.Id);
            return false;
        }
    }

    private static string? ExtractSlidesArrayFromHtml(string html)
    {
        // The HTML contains: const slides = [...];
        // Extract the JSON array
        var match = Regex.Match(html, @"const\s+slides\s*=\s*(\[[\s\S]*?\]);", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ReplaceSlidesArrayInHtml(string html, string newSlidesJson)
    {
        return Regex.Replace(
            html,
            @"const\s+slides\s*=\s*\[[\s\S]*?\];",
            $"const slides = {newSlidesJson};",
            RegexOptions.Multiline);
    }

    // --- Slideshow string extraction and filtering helpers ---

    /// <summary>
    /// Represents a translatable string found in the JavaScript slides text.
    /// Index and Length refer to the full match including quotes.
    /// Value is the content between quotes (raw, with any JS escape sequences).
    /// </summary>
    private record TranslatableStringMatch(int Index, int Length, string Value, string? Key);

    /// <summary>
    /// Property keys whose string values should NOT be translated (technical/styling values).
    /// </summary>
    private static readonly HashSet<string> SkipKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "type", "icon", "color", "bgGrad", "bgColor", "gradient", "bg"
    };

    /// <summary>
    /// Finds all translatable double-quoted string values in JavaScript object notation text.
    /// Parses each "..." value, determines its property key, and filters out non-translatable values.
    /// </summary>
    private static List<TranslatableStringMatch> FindTranslatableStrings(string jsText)
    {
        var results = new List<TranslatableStringMatch>();
        // Match all double-quoted strings (handles \" escape sequences inside strings)
        var regex = new Regex(@"""((?:[^""\\]|\\.)*)""");

        foreach (Match match in regex.Matches(jsText))
        {
            var value = match.Groups[1].Value;
            var key = GetKeyForString(jsText, match.Index);

            if (ShouldTranslateString(key, value))
            {
                results.Add(new TranslatableStringMatch(match.Index, match.Length, value, key));
            }
        }

        return results;
    }

    /// <summary>
    /// Determines the property key for a string value by looking at the text preceding it.
    /// For direct properties (key: "value"), returns the key name.
    /// For array elements ("value" inside [...]), finds the enclosing array's key.
    /// </summary>
    private static string? GetKeyForString(string jsText, int stringIndex)
    {
        var before = jsText[..stringIndex].TrimEnd();

        // Direct key: "value" pattern (JS object notation - keys are unquoted)
        var keyMatch = Regex.Match(before, @"(\w+)\s*:\s*$");
        if (keyMatch.Success)
            return keyMatch.Groups[1].Value;

        // Array element: preceded by [ or , (string is inside an array of strings)
        if (before.Length > 0 && (before[^1] == ',' || before[^1] == '['))
        {
            return FindEnclosingArrayKey(jsText, stringIndex);
        }

        return null;
    }

    /// <summary>
    /// Walks backwards from a position to find the key of the enclosing array bracket.
    /// </summary>
    private static string? FindEnclosingArrayKey(string jsText, int position)
    {
        int depth = 0;
        for (int i = position - 1; i >= 0; i--)
        {
            var c = jsText[i];
            if (c == ']') depth++;
            else if (c == '[')
            {
                if (depth == 0)
                {
                    // Found the opening bracket, find the key before it
                    var before = jsText[..i].TrimEnd();
                    var keyMatch = Regex.Match(before, @"(\w+)\s*:\s*$");
                    return keyMatch.Success ? keyMatch.Groups[1].Value : null;
                }
                depth--;
            }
        }
        return null;
    }

    /// <summary>
    /// Determines whether a string value should be translated based on its key and content.
    /// </summary>
    private static bool ShouldTranslateString(string? key, string value)
    {
        // Skip empty or whitespace-only strings
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Skip if key is in the skip list
        if (key != null && SkipKeys.Contains(key)) return false;

        // Skip color values (#hex)
        if (value.StartsWith('#')) return false;

        // Skip CSS gradients
        if (value.StartsWith("linear-gradient", StringComparison.OrdinalIgnoreCase)) return false;
        if (value.StartsWith("radial-gradient", StringComparison.OrdinalIgnoreCase)) return false;

        // Skip purely numeric values (integers, decimals, percentages like "50%")
        if (Regex.IsMatch(value, @"^\d+\.?\d*%?$")) return false;

        // Skip phone number-like strings (digits, spaces, dashes, parentheses, plus)
        if (Regex.IsMatch(value, @"^[\d\s\-\+\(\)\.]+$")) return false;

        // Skip URLs
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;

        // Skip single emoji characters (short strings of only non-ASCII chars)
        if (value.Length <= 4 && value.All(c => !char.IsAscii(c) || char.IsWhiteSpace(c))) return false;

        return true;
    }

    /// <summary>
    /// Unescapes a JavaScript string value (reverses backslash escape sequences).
    /// </summary>
    private static string UnescapeJsString(string value)
    {
        if (!value.Contains('\\')) return value;

        return value
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\\", "\\");
    }

    /// <summary>
    /// Escapes a string for use inside a JavaScript double-quoted string.
    /// </summary>
    private static string EscapeJsString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    // Internal DTOs for JSON serialization (matching existing structure)
    private class TranslatedSectionData
    {
        public Guid SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class TranslatedQuestionData
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
    }
}
