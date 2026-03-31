namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.SafetyTermRegistry;

public interface ISafetyTermRegistryService
{
    RegistryScanResult Scan(string translatedText, string targetLanguage);
}
