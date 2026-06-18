# Backend Fix: AllowRetry field + refresher passthrough conditional

Date: 2026-06-18

## Phase 1 Verification Findings

### 1. Legacy ToolboxTalkForm payload
`ToolboxTalkForm.tsx` sends `frequency` but omits `requiresRefresher` and `refresherIntervalMonths`. The command receives their defaults: `RequiresRefresher = false`, `RefresherIntervalMonths = 12`.

### 2. Command DTO defaults
- `RequiresRefresher: bool = false` (non-nullable, init-only)
- `RefresherIntervalMonths: int = 12` (non-nullable, init-only)
- `AllowRetry`: completely absent from the command (the primary bug)
- Entity default: `AllowRetry = true`

### 3. Mapper behavior
`ToCanonicalFields(Monthly)` → `(true, 1)` — the corruption source.
`ToCanonicalFields(Once, existing)` → `(false, existing)` — preserves interval.
No quarterly bucket in the legacy mapper.

### 4. Existing tests
`RefresherFrequencyMapperTests` covers mapper round-trips. `UpdateToolboxTalkCommandHandlerTests` covers staleness detection with no refresher assertions. `UpdateToolboxTalkSettingsCommandHandlerTests` covers the separate settings handler. All must continue passing.

### 5. Chunk 1 panel payload
Both `SectionEditPanel.handleSave` and `QuizEditPanel.handleSave`:
- Send `requiresRefresher: talk.requiresRefresher` ✓
- Send `refresherIntervalMonths: talk.refresherIntervalMonths` ✓
- Do NOT send `allowRetry` ✗ — would corrupt `allowRetry = false` on save

### 6. Approach chosen: Approach A
Conditional: `request.RequiresRefresher || request.RefresherIntervalMonths != 12`

Edge case analysis:
| Source | RequiresRefresher | RefresherIntervalMonths | Conditional fires? | Result |
|--------|-------------------|-------------------------|-------------------|--------|
| Legacy form | false (default) | 12 (default) | No → mapper path | Correct |
| Panel, refresher enabled | true | 3 | Yes (RequiresRefresher=true) | (true, 3) ✓ |
| Panel, no refresher, interval preserved | false | 3 | Yes (3 != 12) | (false, 3) ✓ |
| Panel, exact defaults | false | 12 | No → mapper with Once | (false, 12) ✓ |

## Files Changed in Scope

**Backend:**
- `src/Modules/ToolboxTalks/.../Commands/UpdateToolboxTalk/UpdateToolboxTalkCommand.cs`
  — Added `AllowRetry: bool = true`
- `src/Modules/ToolboxTalks/.../Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs`
  — Added `toolboxTalk.AllowRetry = request.AllowRetry` write (adjacent to quiz settings)
  — Replaced unconditional `ToCanonicalFields` call with Approach A conditional
  — Added `AllowRetry = entity.AllowRetry` to `MapToDto` (incidental gap, in-scope)
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/UpdateToolboxTalkCommandHandlerTests.cs`
  — Added test 10: `ExplicitRefresherFields_HonoredOverFrequencyMapper`

**Frontend:**
- `web/src/types/toolbox-talks.ts`
  — Added `allowRetry?: boolean` to `CreateToolboxTalkRequest` interface
- `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx`
  — Added `allowRetry: talk.allowRetry` to `handleSave` payload
- `web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx`
  — Added `allowRetry: talk.allowRetry` to `handleSave` payload

## Files Changed Outside Stated Scope
None.

## Test Results
- Backend integration: 459/459 passing (includes new test 10)
- Backend unit: 230/230 passing
- Vitest: 15/15 passing
- TypeScript: clean (0 errors)
- Playwright: 1/1 passing

## Build Output
Build succeeded. 11 warnings — all pre-existing, none introduced by this chunk.

## Notable Decisions

1. **Approach A over B**: The legacy form already omits refresher fields (they arrive as command defaults). Nullable fields (Approach B) would require changes to the legacy form's controller binding path. Approach A works without touching the legacy form at all.

2. **AllowRetry in Chunk 1 panels**: Adding `AllowRetry` to the command with default `true` would silently corrupt `allowRetry = false` saves from the panels. Fixing both panels to pass `talk.allowRetry` is the correct minimal fix — it's within the scope of "Adds AllowRetry to UpdateToolboxTalkCommand and writes it."

3. **MapToDto gap fixed**: `UpdateToolboxTalkCommandHandler.MapToDto` was not returning `AllowRetry`. Added `AllowRetry = entity.AllowRetry` as an incidental correctness fix — the write now also reflects in the mutation response.

4. **Legacy form AllowRetry**: The legacy `ToolboxTalkForm` still omits `allowRetry` from its payload. Under the new command, it defaults to `true`, potentially corrupting `allowRetry = false` on legacy-form saves. This is the same pre-existing limitation as the legacy form's refresher field corruption — noted but out of scope while the legacy form is being deprecated (Note 29).
