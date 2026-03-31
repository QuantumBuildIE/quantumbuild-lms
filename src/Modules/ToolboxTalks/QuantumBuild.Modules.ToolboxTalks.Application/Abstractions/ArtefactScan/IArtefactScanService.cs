namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ArtefactScan;

public interface IArtefactScanService
{
    ArtefactScanResult Scan(string originalText, string translatedText, string targetLanguage);
}
