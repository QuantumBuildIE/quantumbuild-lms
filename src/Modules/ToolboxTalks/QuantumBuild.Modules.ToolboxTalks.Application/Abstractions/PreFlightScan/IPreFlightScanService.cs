namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.PreFlightScan;

public interface IPreFlightScanService
{
    Task<PreFlightScanResult> ScanAsync(
        IReadOnlyList<string> sectionTexts,
        string targetLanguage,
        string? sectorKey,
        CancellationToken cancellationToken = default);
}
