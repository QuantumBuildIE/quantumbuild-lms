namespace QuantumBuild.Modules.ToolboxTalks.Application.Prompts;

/// <summary>
/// Centralized prompts for AI quiz question generation.
/// </summary>
public static class QuizGenerationPrompts
{
    /// <summary>
    /// Builds the prompt for generating quiz questions from training content.
    /// </summary>
    public static string BuildQuizPrompt(
        string content,
        string? videoFinalPortionContent,
        bool hasVideo,
        bool hasPdf,
        int minimumQuestions)
    {
        var sourceGuidance = (hasVideo, hasPdf) switch
        {
            (true, true) => @"
- Create questions from BOTH the video transcript AND the PDF document
- Mark each question with its source (""Video"" or ""Pdf"")
- Aim for a balanced mix of questions from both sources",
            (true, false) => @"
- All questions should come from the video transcript
- Mark all questions with source ""Video""",
            (false, true) => @"
- All questions should come from the PDF document
- Mark all questions with source ""Pdf""",
            _ => ""
        };

        var finalPortionRequirement = "";
        if (hasVideo && !string.IsNullOrEmpty(videoFinalPortionContent))
        {
            finalPortionRequirement = $@"

CRITICAL REQUIREMENT - FINAL PORTION QUESTION:
At least ONE question MUST be based on content from the VIDEO FINAL PORTION section below.
This ensures employees watched the entire video. Mark this question with ""isFromVideoFinalPortion"": true.

VIDEO FINAL PORTION CONTENT (80-100% of video):
{videoFinalPortionContent}
--- END OF FINAL PORTION ---";
        }

        var maxQuestions = Math.Max(10, minimumQuestions + 5);

        return $@"You are a professional training content expert. Create multiple-choice quiz questions to test employee understanding of the following training content.

REQUIREMENTS:
- Create at least {minimumQuestions} questions (up to {maxQuestions} for longer content)
- Each question must have exactly 4 options (A, B, C, D)
- Only ONE option should be correct
- Questions should test important knowledge, not trivial details
- Use clear, unambiguous language
- Options should be plausible (avoid obviously wrong answers)
{sourceGuidance}
{finalPortionRequirement}

OUTPUT FORMAT:
Return your response as a JSON array with this exact structure:
```json
[
  {{
    ""sortOrder"": 1,
    ""questionText"": ""What is the correct procedure for...?"",
    ""options"": [""Option A text"", ""Option B text"", ""Option C text"", ""Option D text""],
    ""correctAnswerIndex"": 2,
    ""source"": ""Video"",
    ""isFromVideoFinalPortion"": false,
    ""videoTimestamp"": ""2:30""
  }},
  {{
    ""sortOrder"": 2,
    ""questionText"": ""According to the training guidelines...?"",
    ""options"": [""Option A"", ""Option B"", ""Option C"", ""Option D""],
    ""correctAnswerIndex"": 0,
    ""source"": ""Pdf"",
    ""isFromVideoFinalPortion"": false,
    ""videoTimestamp"": null
  }}
]
```

Note: correctAnswerIndex is 0-based (0 = first option, 3 = fourth option)

IMPORTANT: Return ONLY the JSON array, no additional text or explanation.

CONTENT TO ANALYZE:
{content}

Generate the quiz questions now as a JSON array:";
    }

    /// <summary>
    /// Builds the prompt for generating a single question from the final portion of a video.
    /// </summary>
    public static string BuildFinalPortionQuestionPrompt(
        string finalPortionContent,
        int sortOrder)
    {
        return $@"You are a professional training content expert. Create ONE multiple-choice quiz question based ONLY on the following content from the final portion of a training video.

This question is specifically to verify the employee watched the entire video.

CONTENT FROM VIDEO FINAL PORTION (80-100%):
{finalPortionContent}

REQUIREMENTS:
- Create exactly ONE question
- The question must have exactly 4 options (A, B, C, D)
- Only ONE option should be correct
- The question should test important knowledge from this portion
- Use clear, unambiguous language

OUTPUT FORMAT:
Return your response as a JSON object with this exact structure:
```json
{{
  ""sortOrder"": {sortOrder},
  ""questionText"": ""Your question here?"",
  ""options"": [""Option A"", ""Option B"", ""Option C"", ""Option D""],
  ""correctAnswerIndex"": 0,
  ""source"": ""Video"",
  ""isFromVideoFinalPortion"": true,
  ""videoTimestamp"": ""final portion""
}}
```

Note: correctAnswerIndex is 0-based (0 = first option, 3 = fourth option)

IMPORTANT: Return ONLY the JSON object, no additional text or explanation.

Generate the question now:";
    }
}
