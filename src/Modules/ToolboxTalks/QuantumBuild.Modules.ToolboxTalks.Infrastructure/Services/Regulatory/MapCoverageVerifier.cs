using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;

/// <summary>
/// One feature RequirementIngestionJob has assembled for persistence, identified and attributed
/// the same way the structure map identifies and attributes its own declared features.
/// Deliberately a narrower shape than the job's own TranscribedFeature — MapCoverageVerifier only
/// ever needs identity (Identifier, Block) and attribution (StandardId, PrincipleNumber), never
/// VerbatimText or FootnoteDefinition. See MapCoverageVerifier's remarks for why content is out of
/// scope by construction, not by a special-cased exemption.
/// </summary>
public sealed record CandidateFeature(
    string Identifier,
    RequirementBlock Block,
    string StandardId,
    int PrincipleNumber);

/// <summary>One (Identifier, Block) pair, named the same way RequirementIngestionJob.FindMissingFeatures names one ("Person 1.1.2").</summary>
public sealed record MapCoverageEntry(string Identifier, RequirementBlock Block)
{
    public override string ToString() => $"{Block} {Identifier}";
}

/// <summary>
/// A feature present in both the map's declared set and the candidate set under the same
/// (Identifier, Block) key, but attributed to a different standard/principle than the map
/// declares for it — the cross-boundary contamination class from the old free-judged era.
/// </summary>
public sealed record MisattributedFeature(
    string Identifier,
    RequirementBlock Block,
    string DeclaredStandardId,
    int DeclaredPrincipleNumber,
    string ActualStandardId,
    int ActualPrincipleNumber);

/// <summary>
/// Outcome of <see cref="MapCoverageVerifier.Verify"/>: every way a candidate set can diverge from
/// what the map declares. <see cref="IsComplete"/> is true only when all three lists are empty —
/// missing, unexpected, and misattributed are each independently disqualifying.
/// </summary>
public sealed record MapCoverageResult(
    IReadOnlyList<MapCoverageEntry> Missing,
    IReadOnlyList<MapCoverageEntry> Unexpected,
    IReadOnlyList<MisattributedFeature> Misattributed)
{
    public bool IsComplete => Missing.Count == 0 && Unexpected.Count == 0 && Misattributed.Count == 0;
}

/// <summary>
/// Feature-level completeness + attribution check for map-driven extraction
/// (docs/faithful-extraction-build-recon.md, sub-chunk 3). Replaces the old standard-presence
/// check, which passed as long as each standard appeared at all — the reason free-judged
/// extraction returning 43-of-151 features still "passed" completeness. This check instead
/// compares the full candidate set RequirementIngestionJob is about to persist against every
/// (Identifier, Block) pair the structure map declares, across every standard and principle in
/// the document, catching three independently distinct failure classes:
///
/// - Missing: a map-declared feature has no matching candidate at all.
/// - Unexpected: a candidate exists whose (Identifier, Block) the map does not declare anywhere.
/// - Misattributed: a candidate's (Identifier, Block) matches a declared feature, but the
///   candidate's StandardId/PrincipleNumber does not match what the map declares that feature
///   belongs to.
///
/// This is a document-level safety net on top of RequirementIngestionJob's own per-segment
/// completeness check (FindMissingFeatures, which already guarantees each standard's own Claude
/// response is complete before assembly runs) and its map-driven assembly (which, by walking the
/// map's own feature list rather than the model's output, structurally cannot introduce unexpected
/// or misattributed candidates today). Calling this explicitly — rather than relying on assembly's
/// construction to keep the property true — turns "the map's declared set is exactly what gets
/// persisted, correctly attributed" into a checked invariant instead of an implicit guarantee
/// (previously just a code comment) that a future refactor of assembly could silently break
/// without any test failing.
///
/// DELIBERATELY DOES NOT EXAMINE CONTENT. Verification keys only on identity (Identifier, Block)
/// and attribution (StandardId, PrincipleNumber) — never VerbatimText or FootnoteDefinition. A
/// map-declared feature whose own FootnoteDefinition is null (a legitimate map-side gap — e.g.
/// HIQA 4.5.5) cannot fail this check on that basis: FootnoteDefinition is not part of the key
/// this check compares, so a null value on the map side is invisible to it by construction, not by
/// a special-cased exemption. This proves coverage/attribution MECHANICS against the map; it does
/// NOT prove the transcribed VerbatimText is faithful to the source document — a feature can be
/// present and correctly attributed while its transcribed text is wrong. That remains the human
/// output-review step.
///
/// Runs identically regardless of whether the map is Draft or Verified — coverage is a property of
/// the map's declared content, not of whether a human has confirmed that content is correct. This
/// method does not accept (and cannot examine) the map's Status/IsVerified at all, so there is no
/// branch that could weaken or skip the check for a Draft map.
/// </summary>
public static class MapCoverageVerifier
{
    public static MapCoverageResult Verify(
        DocumentStructureMap structureMap,
        IReadOnlyList<CandidateFeature> candidates)
    {
        var declared = new Dictionary<(string Identifier, RequirementBlock Block), (string StandardId, int PrincipleNumber)>();
        foreach (var principle in structureMap.Principles)
        {
            foreach (var standard in principle.Standards)
            {
                foreach (var feature in standard.Features)
                {
                    // Last-write-wins on a duplicate (Identifier, Block) declared more than once in
                    // the map itself — the map's own internal uniqueness is authored/reviewed data,
                    // out of scope here (this validates extraction against the map, not the map
                    // against itself).
                    declared[(feature.Identifier, feature.Block)] = (standard.Id, principle.Number);
                }
            }
        }

        var actual = new Dictionary<(string Identifier, RequirementBlock Block), (string StandardId, int PrincipleNumber)>();
        foreach (var candidate in candidates)
        {
            actual[(candidate.Identifier, candidate.Block)] = (candidate.StandardId, candidate.PrincipleNumber);
        }

        var missing = declared.Keys
            .Where(key => !actual.ContainsKey(key))
            .OrderBy(key => key.Identifier, StringComparer.Ordinal)
            .Select(key => new MapCoverageEntry(key.Identifier, key.Block))
            .ToList();

        var unexpected = actual.Keys
            .Where(key => !declared.ContainsKey(key))
            .OrderBy(key => key.Identifier, StringComparer.Ordinal)
            .Select(key => new MapCoverageEntry(key.Identifier, key.Block))
            .ToList();

        var misattributed = declared.Keys
            .Where(key => actual.ContainsKey(key) && actual[key] != declared[key])
            .OrderBy(key => key.Identifier, StringComparer.Ordinal)
            .Select(key => new MisattributedFeature(
                key.Identifier,
                key.Block,
                declared[key].StandardId,
                declared[key].PrincipleNumber,
                actual[key].StandardId,
                actual[key].PrincipleNumber))
            .ToList();

        return new MapCoverageResult(missing, unexpected, misattributed);
    }
}
