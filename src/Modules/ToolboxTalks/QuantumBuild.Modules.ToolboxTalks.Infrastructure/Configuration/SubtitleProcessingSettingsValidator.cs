using Microsoft.Extensions.Options;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

/// <summary>
/// Fail-fast startup validator for SubtitleProcessingSettings.
/// Causes the application to throw at startup when required subtitle processing
/// configuration is missing, preventing silent runtime failures during transcription.
/// </summary>
public class SubtitleProcessingSettingsValidator : IValidateOptions<SubtitleProcessingSettings>
{
    public ValidateOptionsResult Validate(string? name, SubtitleProcessingSettings options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ElevenLabs.ApiKey))
            errors.Add("SubtitleProcessing:ElevenLabs:ApiKey must be set");
        if (string.IsNullOrWhiteSpace(options.ElevenLabs.Model))
            errors.Add("SubtitleProcessing:ElevenLabs:Model must be set");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
