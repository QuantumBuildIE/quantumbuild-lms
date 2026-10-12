# RSA ingestion: Standard 12 'invalid JSON' failure: recon

Read-only. No code changes. All line refs are `RequirementIngestionJob.cs` (Infrastructure/Jobs) unless stated.

Tags: **OBSERVED** = read in code or in the reported error. **INFERRED** = reasoning, not confirmed.

## Headline

The raw model response for Standard 12 is not captured anywhere durable. Only the first 200 characters survive, in a Warning log. We cannot say from code alone why Claude's reply was not JSON. That preview is the one thing to fetch from Dev.

This failure is **not** the empty-array bug. An empty `[]` is treated as valid and cannot produce this message (see 1).

## 1. Parsing: expected shape, extraction, empty array

**OBSERVED**
- Expected shape: a top-level JSON array of `{identifier, block, verbatimText}` (`ExtractedFeature`, lines 1094-1099). `TryParseFeatures` deserialises to `List<ExtractedFeature>` (line 937).
- Extraction is minimal. The only pre-processing is stripping a leading ```` ``` ```` fence line and a trailing ```` ``` ```` (lines 929-935). There is no bracket-scan, no 'find the first `[`', and no tolerance for prose.
- Empty array is **not** invalid. The doc comment (lines 914-919) and code return an empty list for `[]`. `ExtractStandardSegmentAsync` then runs `FindMissingFeatures`, finds all 5 missing, and the failure surfaces as `extraction_incomplete` ('missing declared features (...)'). The RequirementMappingJob bug does not recur here.
- `InvalidJson` (the reported reason) is only reached when `TryParseFeatures` returns null on **both** attempts (lines 633-636). Null comes from three cases:
  - blank response (line 922)
  - a `JsonException` (line 939)
  - the literal `null`
- It is also not truncation: `stop_reason == "max_tokens"` is checked first and would have reported `extraction_truncated`.

**INFERRED** (what makes a non-truncated, non-empty reply fail to parse)
1. A prose preamble or postamble around the JSON. Examples: 'Looking at Article 12...' or a trailing note. The parser fails on any non-fence leading text. A fenced block followed by trailing prose also fails, because `EndsWith("```")` is false.
2. A top-level JSON object instead of an array.
3. Unescaped characters inside `verbatimText`. This looks unlikely here: none of the Article 12 texts contain double quotes.

Cause 1 is the most probable, and the retry's 'respond ONLY with JSON' addendum evidently did not fix it. Both attempts failed.

## 2. What is logged, and where to find it on Dev

**OBSERVED**
- Per failed attempt, `TryParseFeatures` logs a **Warning** (lines 941-942): 'Failed to parse Claude response as JSON: {Preview}'. The preview is the **first 200 chars** only. The `JsonException` is attached, and its message gives the byte position and the offending character. For example, `'T' is an invalid start of a value` would mean the reply began with prose.
- `ExtractStandardSegmentAsync` logs a Warning on the first failure (lines 600-603) and a final **Error** (lines 633-636). The Error carries standard id and document id and **no response text**.
- `ExecuteAsync` logs a Warning per failed standard (lines 196-198), then `MarkFailedAsync` logs an Error with the aggregated message. That message is also stored in `RegulatoryDocument.LastIngestionErrorMessage`, truncated to 2000 chars.
- No raw response is persisted. `AiUsageLog` stores token counts only.
- Sentry: `MinimumEventLevel = Error` (Program.cs:49), so the line-633 Error becomes a Sentry event. No body is attached to it.

**Where to look on Dev**
- Railway logs for the API service, filtered on `Failed to parse Claude response as JSON` near the ingest time. The Dev document is probably the one with `LastIngestionErrorCode = extraction_invalid_json`. There should be 2 hits for Standard 12 (initial + retry, lines 941-942). The Hangfire `[AutomaticRetry(Attempts = 1)]` applies only to unhandled exceptions, and this path returns normally, so there is no extra job-level retry.
- **INFERRED:** the Sentry event for the line-633 Error may show the two Warnings as breadcrumbs, since Sentry's logging integration records Information+ logs as breadcrumbs by default. If so, the 200-char previews appear there. Not verified.

**Gap:** the 200-char preview shows only how the reply started. Compare the mapping job, which logs a longer truncated body on both attempts (`RequirementMappingJob.cs` lines 224-242, via `TruncateForLogging`). Ingestion has no equivalent on its final failure.

## 3. The exact prompt for Standard 12

Built by `BuildExtractionPrompt` (lines 825-855) with `principleNumber = 4` and `standard.Id = "12"`. There are no person features, so the Person section is empty. Rendered:

```
You are transcribing verbatim text from a regulatory document. A structure map — authored and reviewed separately, NOT by you — has already determined exactly which features this document defines for Standard 12 (Principle 4). Your ONLY task is to locate each declared feature's exact printed text in the document below and copy it. Do not judge whether a feature belongs, do not paraphrase, summarise, reorder, merge, or omit any of them.

The structure map declares these features for Standard 12:

Person-experience features (numbered items describing what the person receiving the service experiences):
<empty>

Provider features (numbered items describing what the service provider must have in place):
- 12(para 1)
- 12(para 2)
- 12(para 3)
- 12(para 4)
- 12(para 5)

For EACH of the 5 features listed above, find its exact verbatim text in the document text below (search near "Standard 12" and its person-experience/provider feature lists) and transcribe it exactly as printed. Exclude footnote marker glyphs and footnote text itself — footnotes are handled separately from this transcription.

Respond ONLY with a valid JSON array of exactly 5 objects, one per feature listed above (any order), each shaped as:
{"identifier": "<exact identifier exactly as listed above, unchanged>", "block": "Person" or "Provider", "verbatimText": "<the feature's exact printed text>"}

IMPORTANT RULES:
- Copy text EXACTLY as printed ... [rules as in code, lines 846-851]

DOCUMENT TEXT:
<entire extracted document text>
```

**OBSERVED:** the prompt carries only the identifiers, never the expected text. The model must find 'paragraph 1-5 of Article 12' from the bare labels `12(para N)` plus the instruction to 'search near "Standard 12"'.

**Notable risks in this prompt (all INFERRED as causes; each is real in the code)**
1. **Prompt is written for HIQA, not RSA.** It says 'Standard 12', 'person-experience', 'service provider', 'numbered items'. The RSA document has 'Article 12', no person block, and **unnumbered** paragraphs. 'Search near "Standard 12"' will not match the document's heading. The empty Person list invites the model to comment on it.
2. **Unnumbered paragraphs.** Article 12 has five unnumbered paragraphs, labelled by ordinal (`12(para 1)` ...). The model has to count paragraphs and guess the mapping from the label alone. Article 7 has the same trait and, per the report, did not fail, but its paragraph 3 is excluded and the gap is a further hint to confuse.
3. **Derogation wording.** Article 12 is itself a derogation ('the driver may depart from Articles 6 to 9...'). Para 4 ('The driver shall indicate the reason for such departure...') is near-identical to the second sentence of para 1. Duplicate-looking text can prompt 'these two appear to be duplicates, so I am returning...' explanatory prose. This is speculation about model behaviour.
4. **Provider description is wrong for RSA.** The prompt describes 'what the service provider must have in place'. Article 12 duties fall on the **driver** (map header comment says the Provider label is only an internal key). The 'Do not judge whether a feature belongs' rule conflicts with the model's reading of the text, and may tip it into an explanatory reply.
5. **Prose-friendly neighbours.** A reply that mixes a note about Article 12(2)/(3) conditions is plausible. We have not seen it.
6. Nothing in the Article 12 map texts themselves is JSON-hostile: no double quotes, no backslashes, only `\n`-free single-paragraph strings. The text is not the problem.

**Not checked:** the actual Dev document text. `SourceUrl` is not set by the seed (`RegulatoryProfileSeedData.cs` leaves it blank). It is entered by hand, so we do not know what URL or file Dev fetched. The map was transcribed from the EUR-Lex consolidated PDF, so Dev should point at the same source. If Dev points at a different rendering (HTML page, older consolidation), Article 12 may read differently from the map.

## 4. Why one failed standard blocks persistence of all

**OBSERVED**, by design, not accident.
- Comment, lines 175-177: 'All segments are extracted and validated in memory first — nothing is persisted until every segment has succeeded (all-or-nothing: one failed segment fails the whole document)'.
- Loop, lines 186-206: every standard is attempted, each outcome collected into `segmentOutcomes` (run-all, not fail-fast).
- Gate, lines 208-215: `failedSegments.Count > 0` calls `MapSegmentFailures` then `MarkFailedAsync` and **returns**. Persistence (`PersistDraftRequirementsAsync`, line 312, Step 4) sits after that return, so it is never reached.
- Message, lines 769-773: single-failure branch gives 'N of M standards failed extraction. Standard 12 (Principle 4): ... No requirements were persisted for any standard.' That wording matches what was reported. The RSA map has 9 standards (5, 6, 7, 8, 9, 10, 12, 16, 20), so the probable text is '1 of 9 standards failed'.
- Consequence: the other 8 standards' transcriptions, successful or not, are discarded. That is 8 wasted standard calls plus up to 2 calls for Standard 12, each sending the whole document. The retry cannot help either, as persist dedupe (lines 993-1012) is only needed after a partial persist, and none exists.

Persisting the good standards would be safe against duplication, since `PersistDraftRequirementsAsync` dedups on `(FeatureIdentifier, Block)`. The blocker is the deliberate all-or-nothing rule plus `MapCoverageVerifier` (lines 271-281), which requires the whole map be covered.

## Suggested next steps (not done; need go-ahead)

1. **Fetch the evidence first.** Railway Dev logs: 'Failed to parse Claude response as JSON' near the ingest. The 200-char preview and the `JsonException` message will settle prose-vs-structure.
2. **Capture the raw response on final failure** (truncated to ~2000 chars, as the mapping job does), so the next failure is diagnosable without log archaeology.
3. **Parser tolerance**: extract the outermost `[` ... `]` span before deserialising, so a preamble or postamble does not fail the segment.
4. **Make the prompt map-aware.** Include the expected text, or at least first words, per feature from the map, so unnumbered paragraphs can be located. Make the wording driven by the map (Article/paragraph, no empty Person section) rather than HIQA phrasing. This is probably the real fix for RSA generally.
5. Whether to relax all-or-nothing is a product decision; leave as is unless asked.
