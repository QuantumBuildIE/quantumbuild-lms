namespace QuantumBuild.Modules.ToolboxTalks.Application.Prompts;

/// <summary>
/// Centralized prompts for AI section generation.
/// </summary>
public static class SectionGenerationPrompts
{
    /// <summary>
    /// Builds the prompt for generating training sections from content.
    /// </summary>
    public static string BuildSectionPrompt(
        string content,
        string sourceDescription,
        int minimumSections,
        bool hasVideo,
        bool hasPdf)
    {
        var sourceTracking = (hasVideo, hasPdf) switch
        {
            (true, true) => @"
For each section, indicate the source:
- ""Video"" if the information comes primarily from the video transcript
- ""Pdf"" if the information comes primarily from the PDF document
- ""Both"" if the information combines content from both sources",
            (true, false) => @"All sections should have source ""Video"" since content is from video transcript only.",
            (false, true) => @"All sections should have source ""Pdf"" since content is from PDF document only.",
            _ => ""
        };

        return $@"You are a professional training content expert. Analyze the following {sourceDescription} and create clear, concise sections that summarize the key points.

REQUIREMENTS:
- Create at least {minimumSections} sections (more if the content warrants it)
- Each section needs a clear, descriptive title
- Each section content should be 4-5 lines (a short paragraph)
- Focus on the most important information, procedures, and requirements
- Use clear, simple language suitable for all employees
- Sections should be logically ordered (general concepts first, then specific procedures)
{sourceTracking}

OUTPUT FORMAT:
Return your response as a JSON array with this exact structure:
```json
[
  {{
    ""sortOrder"": 1,
    ""title"": ""Section Title Here"",
    ""content"": ""The paragraph content here, 4-5 lines covering this key point."",
    ""source"": ""Video""
  }},
  {{
    ""sortOrder"": 2,
    ""title"": ""Another Section Title"",
    ""content"": ""Another paragraph covering a different key point."",
    ""source"": ""Pdf""
  }}
]
```

IMPORTANT: Return ONLY the JSON array, no additional text or explanation.

CONTENT TO ANALYZE:
{content}";
    }
}
