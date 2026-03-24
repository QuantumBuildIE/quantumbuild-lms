using System.Text;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Prompts;

/// <summary>
/// Instruction for a mandatory glossary term translation.
/// </summary>
public record GlossaryTermInstruction(string EnglishTerm, string ApprovedTranslation);

/// <summary>
/// Centralized prompts for AI content and subtitle translation.
/// Implements a tiered prompt system: base → sector → safety-critical → glossary → language-specific.
/// </summary>
public static class TranslationPrompts
{
    /// <summary>
    /// Builds a tiered translation prompt for compliance-sensitive content.
    /// Layers: Tier 1 (base) → Tier 2 (sector) → Tier 3 (safety) → Tier 4 (glossary) → Tier 5 (language).
    /// </summary>
    public static string BuildTranslationPrompt(
        string text,
        string sourceLanguage,
        string targetLanguage,
        bool isHtml,
        string? sectorKey = null,
        bool isSafetyCritical = false,
        IEnumerable<GlossaryTermInstruction>? glossaryTerms = null)
    {
        var contentType = isHtml ? "HTML content" : "text";
        var sb = new StringBuilder();

        // --- Tier 1: Base instructions (always included) ---
        sb.AppendLine($"Translate the following {sourceLanguage} {contentType} to {targetLanguage}.");
        if (isHtml)
        {
            sb.AppendLine("IMPORTANT: Keep all HTML tags exactly as they are. Only translate the text content between tags.");
        }
        sb.AppendLine($"Return ONLY the translated {contentType} — no explanations, no markdown, no preamble.");
        sb.AppendLine();
        sb.AppendLine("TRANSLATION STANDARDS — mandatory for all translations:");
        sb.AppendLine("- REGISTER: Use formal, professional language throughout. This is a workplace training document.");
        sb.AppendLine("- TERMINOLOGY: Use consistent translations for the same term throughout. Do not vary the same term across sentences.");
        sb.AppendLine($"- CAPITALISATION: Follow {targetLanguage} capitalisation conventions. Do NOT capitalise common nouns mid-sentence unless required by {targetLanguage} grammar rules.");
        sb.AppendLine("- COMPLETENESS: Translate every word. Do not leave English terms untranslated unless they are proper nouns or internationally recognised acronyms.");
        sb.AppendLine($"- FLUENCY: Write as a native {targetLanguage} speaker would — not as translated English. Restructure sentences if needed for natural fluency.");
        sb.AppendLine("- SAFETY LANGUAGE: Preserve the full force of all obligations and warnings. \"Must\" stays \"must\". \"Never\" stays \"never\". Do not weaken mandatory language.");

        // --- Tier 2: Sector-specific layer ---
        if (!string.IsNullOrEmpty(sectorKey))
        {
            var sectorInstructions = GetSectorInstructions(sectorKey, targetLanguage);
            if (!string.IsNullOrEmpty(sectorInstructions))
            {
                sb.AppendLine();
                sb.Append(sectorInstructions);
            }
        }

        // --- Tier 3: Safety-critical boost ---
        if (isSafetyCritical)
        {
            sb.AppendLine();
            sb.AppendLine("SAFETY-CRITICAL CONTENT — ABSOLUTE PRECISION REQUIRED:");
            sb.AppendLine("Translate with maximum precision. Preserve every prohibition (do not, never, must not), every deadline, every numeric value, every emergency instruction, and every regulatory term exactly as written. Do not paraphrase, soften, or add words not present in the source. A mistranslation of this content could directly harm a person.");
        }

        // --- Tier 4: Glossary injection ---
        var terms = glossaryTerms?.Where(t => !string.IsNullOrWhiteSpace(t.ApprovedTranslation)).ToList();
        if (terms is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("APPROVED TERMINOLOGY — use these exact translations, no alternatives:");
            foreach (var term in terms)
            {
                sb.AppendLine($"- \"{term.EnglishTerm}\" → use \"{term.ApprovedTranslation}\"");
            }
            sb.AppendLine("These are mandatory. Do not substitute synonyms or alternative phrasings.");
        }

        // --- Tier 5: Language-specific rules ---
        var languageInstructions = GetLanguageInstructions(targetLanguage);
        if (!string.IsNullOrEmpty(languageInstructions))
        {
            sb.AppendLine();
            sb.Append(languageInstructions);
        }

        // Append the text to translate
        sb.AppendLine();
        sb.Append(text);

        return sb.ToString();
    }

    /// <summary>
    /// Builds a tiered batch translation prompt for compliance-sensitive content.
    /// Same tier structure as BuildTranslationPrompt but adapted for JSON array format.
    /// </summary>
    public static string BuildBatchTranslationPrompt(
        List<TranslationItem> items,
        string sourceLanguage,
        string targetLanguage,
        string? sectorKey = null,
        bool isSafetyCritical = false,
        IEnumerable<GlossaryTermInstruction>? glossaryTerms = null)
    {
        var sb = new StringBuilder();

        // --- Tier 1: Base instructions ---
        sb.AppendLine($"Translate the following {sourceLanguage} items to {targetLanguage}.");
        sb.AppendLine("Return the translations as a JSON array with the same order as the input.");
        sb.AppendLine("Each element should be the translated text only.");
        sb.AppendLine("For HTML content (marked with [HTML]), preserve all HTML tags and only translate the text.");
        sb.AppendLine();
        sb.AppendLine("TRANSLATION STANDARDS — mandatory for all translations:");
        sb.AppendLine("- REGISTER: Use formal, professional language throughout. This is a workplace training document.");
        sb.AppendLine("- TERMINOLOGY: Use consistent translations for the same term throughout. Do not vary the same term across sentences.");
        sb.AppendLine($"- CAPITALISATION: Follow {targetLanguage} capitalisation conventions. Do NOT capitalise common nouns mid-sentence unless required by {targetLanguage} grammar rules.");
        sb.AppendLine("- COMPLETENESS: Translate every word. Do not leave English terms untranslated unless they are proper nouns or internationally recognised acronyms.");
        sb.AppendLine($"- FLUENCY: Write as a native {targetLanguage} speaker would — not as translated English. Restructure sentences if needed for natural fluency.");
        sb.AppendLine("- SAFETY LANGUAGE: Preserve the full force of all obligations and warnings. \"Must\" stays \"must\". \"Never\" stays \"never\". Do not weaken mandatory language.");

        // --- Tier 2: Sector-specific layer ---
        if (!string.IsNullOrEmpty(sectorKey))
        {
            var sectorInstructions = GetSectorInstructions(sectorKey, targetLanguage);
            if (!string.IsNullOrEmpty(sectorInstructions))
            {
                sb.AppendLine();
                sb.Append(sectorInstructions);
            }
        }

        // --- Tier 3: Safety-critical boost ---
        if (isSafetyCritical)
        {
            sb.AppendLine();
            sb.AppendLine("SAFETY-CRITICAL CONTENT — ABSOLUTE PRECISION REQUIRED:");
            sb.AppendLine("Translate with maximum precision. Preserve every prohibition (do not, never, must not), every deadline, every numeric value, every emergency instruction, and every regulatory term exactly as written. Do not paraphrase, soften, or add words not present in the source. A mistranslation of this content could directly harm a person.");
        }

        // --- Tier 4: Glossary injection ---
        var terms = glossaryTerms?.Where(t => !string.IsNullOrWhiteSpace(t.ApprovedTranslation)).ToList();
        if (terms is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("APPROVED TERMINOLOGY — use these exact translations, no alternatives:");
            foreach (var term in terms)
            {
                sb.AppendLine($"- \"{term.EnglishTerm}\" → use \"{term.ApprovedTranslation}\"");
            }
            sb.AppendLine("These are mandatory. Do not substitute synonyms or alternative phrasings.");
        }

        // --- Tier 5: Language-specific rules ---
        var languageInstructions = GetLanguageInstructions(targetLanguage);
        if (!string.IsNullOrEmpty(languageInstructions))
        {
            sb.AppendLine();
            sb.Append(languageInstructions);
        }

        // Items to translate
        sb.AppendLine();
        sb.AppendLine("Items to translate:");
        sb.AppendLine("```");

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var prefix = item.IsHtml ? "[HTML] " : "";
            var context = !string.IsNullOrEmpty(item.Context) ? $" ({item.Context})" : "";
            sb.AppendLine($"{i + 1}. {prefix}{item.Text}{context}");
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Return only a valid JSON array of translated strings, like:");
        sb.AppendLine("[\"translated text 1\", \"translated text 2\", ...]");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a generic translation prompt for non-compliance content (subtitles, UI strings).
    /// Tier 1 only — no sector, safety, glossary, or language-specific layers.
    /// </summary>
    public static string BuildGenericTranslationPrompt(string text, string sourceLanguage, string targetLanguage, bool isHtml)
    {
        if (isHtml)
        {
            return $@"Translate the following {sourceLanguage} HTML content to {targetLanguage}.
IMPORTANT: Keep all HTML tags exactly as they are. Only translate the text content between tags.
Return only the translated HTML, nothing else.

{text}";
        }

        return $@"Translate the following {sourceLanguage} text to {targetLanguage}.
Return only the translated text, nothing else.

{text}";
    }

    /// <summary>
    /// Builds the prompt for translating SRT subtitle content.
    /// Uses generic prompt — subtitles are not compliance-validated content.
    /// </summary>
    public static string BuildSrtTranslationPrompt(string srtContent, string targetLanguage)
    {
        return $@"Translate the following SRT subtitle text to {targetLanguage}.
Keep the exact same format with numbers and timestamps, only translate the text.
Return only the translated SRT, nothing else:

{srtContent}";
    }

    /// <summary>
    /// Returns sector-specific translation instructions for the given sector.
    /// </summary>
    private static string? GetSectorInstructions(string sectorKey, string targetLanguage)
    {
        return sectorKey.ToLowerInvariant() switch
        {
            "homecare" or "healthcare" => $"""
                SECTOR-SPECIFIC REQUIREMENTS (Healthcare / Homecare — HIQA):
                - REGULATORY CONTEXT: This is a HIQA-regulated homecare/healthcare compliance document. Regulatory precision is mandatory.
                - SAFEGUARDING: "Safeguarding" must retain its full legal meaning — do not translate as generic "safety" or "protection".
                - MANDATORY REPORTING: Notification deadlines (e.g. "within 3 working days") must be numerically exact.
                - JOB TITLES: Designated Liaison Person (DLP), Person in Charge (PIC), Registered Provider — translate consistently using approved equivalents or retain in English if no standard equivalent exists.
                - CONSENT: "Informed consent", "capacity", "advocacy" — use clinical/legal standard translations.
                """,

            "construction" or "manufacturing" => $"""
                SECTOR-SPECIFIC REQUIREMENTS (Construction / Manufacturing — HSA):
                - REGULATORY CONTEXT: This is an HSA-regulated workplace safety document. Safety language precision is mandatory.
                - PPE: All personal protective equipment terms must be translated precisely — no omissions or paraphrasing.
                - PROHIBITIONS: "Do not", "never", "must not" must retain full imperative force — never soften to "should not" or "it is recommended".
                - ROLES: PSDP, PSCS, competent person — translate with approved equivalents only.
                - RISK: Hazard categories and risk levels must match source exactly.
                """,

            "food_hospitality" => $"""
                SECTOR-SPECIFIC REQUIREMENTS (Food & Hospitality — FSAI):
                - REGULATORY CONTEXT: This is an FSAI-regulated food safety document. Allergen and HACCP terminology precision is mandatory.
                - ALLERGENS: All 14 declarable allergens must be named precisely — never paraphrased or approximated.
                - HACCP: Critical Control Point terminology must be consistent and exact throughout.
                - TEMPERATURES: All numeric temperature thresholds must be preserved exactly.
                - CCP LIMITS: Critical limits must not be softened or approximated.
                """,

            "transport" => $"""
                SECTOR-SPECIFIC REQUIREMENTS (Transport — RSA):
                - REGULATORY CONTEXT: This is an RSA-regulated road transport document. Numeric precision is mandatory.
                - DRIVER HOURS: All hour and rest period requirements must be numerically exact.
                - TACHOGRAPH: Technical terms must use approved translations only — do not improvise.
                - LOAD LIMITS: All weight and dimension limits must be preserved exactly.
                - PROHIBITIONS: Driving prohibitions must retain full legal force.
                """,

            _ => null
        };
    }

    /// <summary>
    /// Returns language-specific mandatory translation rules for the target language.
    /// </summary>
    private static string? GetLanguageInstructions(string targetLanguage)
    {
        return targetLanguage.ToLowerInvariant() switch
        {
            "polish" => """
                POLISH MANDATORY RULES — compliance critical:
                - "Must/shall/is required to" → "musi/jest zobowiązany do". NEVER use "należy" or "powinien" — these mean "should" (recommendation) not "must" (obligation). This is a compliance failure in regulated documents.
                - "Must not/is prohibited" → "nie wolno/jest zabronione". NEVER use "nie powinien" (should not).
                - "Immediately/without delay" → "niezwłocznie" (formal legal standard) not "natychmiast" (informal).
                - "Authorised person" → "upoważniona osoba".
                - Formal address: use "Pan/Pani" forms where address is required.
                """,

            "romanian" => """
                ROMANIAN MANDATORY RULES:
                - Use formal "dumneavoastră" (not "tu") for any direct address.
                - "Must" → "trebuie să" (obligation) not "ar trebui să" (recommendation).
                - Maintain consistent gender agreement throughout — check all adjective/noun agreements.
                - "Must not" → "nu trebuie să" or "este interzis să" — never soften.
                """,

            "ukrainian" => """
                UKRAINIAN MANDATORY RULES:
                - Use formal "Ви/Вам/Вас" (not "ти") throughout — informal address is inappropriate for compliance documents.
                - "Must not" → "не можна/заборонено" — preserve full prohibition force.
                - Cyrillic script mandatory — never output Latin transliteration.
                - "Safeguarding" → "захист від зловживань" (literal "protection from abuse") to preserve the full legal meaning.
                """,

            "german" => """
                GERMAN MANDATORY RULES:
                - Capitalise all nouns as required by German grammar.
                - "Must" → "muss" (not "sollte" which means "should").
                - "Must not" → "darf nicht" (prohibition) not "sollte nicht" (recommendation).
                - Use formal "Sie" (not "du") throughout.
                - Maintain consistent compound noun formation for technical terms.
                """,

            _ => null
        };
    }
}
