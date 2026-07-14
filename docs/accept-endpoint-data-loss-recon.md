# Accept Endpoint Data-Loss Risk from ThirdPartyReviewed State — Recon

**Date:** 2026-07-08
**Branch:** transval
**Scope:** Read-only investigation. No application files, migrations, or data modified.

---

## 1. Headline

**Data-loss trap confirmed: `POST /api/toolbox-talks/{id}/translations/{languageCode}/accept` (`AcceptAsFinal`) discards the external reviewer's edits when called from `ThirdPartyReviewed` state — and it is directly reachable from the UI, via the same "Review" button/screen an admin is expected to use to close out an external review round.**

This is not a theoretical risk gated behind an unused endpoint. The chain is: admin sees `ThirdPartyReviewed` badge with tooltip *"External reviewer submitted; awaiting final accept"* → clicks **Review** → lands on `ReviewScreen` → the **"Accept this language as final"** button at the bottom is already enabled (because `canAccept` only checks that the *internal* per-section reviewer decisions are non-pending, which they already are by the time external review even started) → click → `AcceptAsFinal` fires → state moves to `Accepted`, event `AcceptedAsFinal` is written, and **`TranslatedSections` is never touched**. The reviewer's `WorkflowReview.EditedContent` row survives in the database untouched, but there is no reachable code path left to apply it — `ConfirmExternalReview`, the only method that would, is guarded to require state `ThirdPartyReviewed`, which has just been vacated.

---

## 2. Accept-shaped endpoints (Q1)

| Route | Method | Auth policy | Handler | State transition driven |
|---|---|---|---|---|
| `POST /api/toolbox-talks/{id}/translations/{languageCode}/accept` | POST | `Learnings.Manage` | `ToolboxTalksController.AcceptTranslationAsFinal` (`src/QuantumBuild.API/Controllers/ToolboxTalksController.cs:1792-1822`) → `ITranslationWorkflowService.AcceptAsFinal` | `Validated`/`ReviewerAccepted`/`ThirdPartyReviewed` → `Accepted` |
| *(none exists)* `.../confirm-external-review` | — | — | `ITranslationWorkflowService.ConfirmExternalReview` (`TranslationWorkflowService.cs:502-532`) — implemented, unit-tested, **no controller route** | `ThirdPartyReviewed` → `Accepted` (with propagation if `accepted:true`) |
| `PUT /api/toolbox-talks/{talkId}/validation/runs/{runId}/sections/{sectionIndex}/accept` | PUT | `Learnings.Admin` | `TranslationValidationController.AcceptSection` (`TranslationValidationController.cs:218-265`) | Per-section `ReviewerDecision` on `TranslationValidationResult`, **not** a `TranslationWorkflowState` transition |
| `PUT .../sections/{sectionIndex}/reject` | PUT | `Learnings.Admin` | `TranslationValidationController.RejectSection` (lines 270-299) | Per-section `ReviewerDecision`, not workflow state |
| `PUT .../sections/{sectionIndex}/edit` | PUT | `Learnings.Admin` | `TranslationValidationController.EditSection` (lines 304+) | Per-section edit + re-validation, not workflow state |

Two structurally different "accept" concepts exist and it's important not to conflate them:

- **Workflow-level accept** (`AcceptAsFinal`) — operates on `TranslationWorkflowState`, one call finalises the entire language.
- **Section-level accept** (`TranslationValidationController.AcceptSection`) — operates on individual `TranslationValidationResult` rows inside a validation run; used during the *internal* reviewer pass (states `Validated`/`ReviewerAccepted`, i.e. the same screen — `ReviewScreen` — is reused for both the pre-external-review pass and, per this recon, incorrectly reused post-external-review too). This endpoint reads/writes `TranslationValidationResult.EditedTranslation`/`EditedSource`, **never** `WorkflowReview.EditedContent`. It is not itself the data-loss trap; it's a distinct field on a distinct entity. See §7 for why it's still relevant.

## 3. Reachability from ThirdPartyReviewed (Q2)

`AcceptAsFinal`'s state guard (`TranslationWorkflowService.cs:748-753`):

```csharp
if (state is not (TranslationWorkflowState.Validated
               or TranslationWorkflowState.ReviewerAccepted
               or TranslationWorkflowState.ThirdPartyReviewed))
    return Result.Fail(
        $"Cannot accept as final from state {state}; requires Validated, ReviewerAccepted, or ThirdPartyReviewed.",
        FailureCode.WorkflowInvalidState);

AddEvent(talkId, languageCode, WorkflowEventTypes.AcceptedAsFinal, payloadJson: null, TriggeredByType.User, tenantId);
await context.SaveChangesAsync(ct);
```

`ThirdPartyReviewed` is explicitly one of the three permitted source states — this is not an oversight in a generic catch-all guard, it's a deliberate inclusion (the doc comment on the controller action even says so: `ToolboxTalksController.cs:1780-1781`, *"Valid from states: Validated, ReviewerAccepted, ThirdPartyReviewed"*). The state machine allows the transition; this is a real, sanctioned path, not a bug where the UI exposes something the backend would reject.

## 4. Behaviour with `WorkflowReview.EditedContent` (Q3)

`AcceptAsFinal` in full (`TranslationWorkflowService.cs:735-760`):

```csharp
public async Task<Result> AcceptAsFinal(
    Guid talkId,
    string languageCode,
    Guid? explicitTenantId = null,
    CancellationToken ct = default)
{
    var guard = ValidateExplicitTenantId(explicitTenantId);
    if (guard is not null) return guard;

    var tenantId = ResolveTenantId(explicitTenantId);
    var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
    var state = stateDto.State;

    if (state is not (TranslationWorkflowState.Validated
                   or TranslationWorkflowState.ReviewerAccepted
                   or TranslationWorkflowState.ThirdPartyReviewed))
        return Result.Fail(...);

    AddEvent(talkId, languageCode, WorkflowEventTypes.AcceptedAsFinal, payloadJson: null, TriggeredByType.User, tenantId);
    await context.SaveChangesAsync(ct);

    // TODO Phase 7: fire WorkflowNotificationTrigger
    return Result.Ok();
}
```

It does exactly one thing: writes an `AcceptedAsFinal` workflow event and saves. It never queries `WorkflowReviews`, never calls `PropagateExternalReviewEditsAsync`, and never touches `ToolboxTalkTranslation.TranslatedSections`. **Confirmed: ignores `WorkflowReview.EditedContent` entirely.**

Contrast with `ConfirmExternalReview` (`TranslationWorkflowService.cs:502-532`), the method that *does* handle it correctly:

```csharp
if (state != TranslationWorkflowState.ThirdPartyReviewed)
    return Result.Fail(
        $"Cannot confirm external review from state {state}; requires ThirdPartyReviewed.",
        FailureCode.WorkflowInvalidState);

if (accepted)
{
    await PropagateExternalReviewEditsAsync(talkId, languageCode, ct);
}

AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewConfirmed,
    Serialize(new { accepted }), TriggeredByType.User, tenantId);
await context.SaveChangesAsync(ct);
```

And `PropagateExternalReviewEditsAsync` (`TranslationWorkflowService.cs:970-1060`) is the only code in the repository that reads `review.EditedContent` and writes it into `translation.TranslatedSections`:

```csharp
var review = await context.WorkflowReviews
    .IgnoreQueryFilters()
    .Where(r => r.WorkflowType == WorkflowType.Translation
             && r.TargetEntityId == talkId
             && r.TargetEntitySubKey == languageCode
             && r.ReviewerType == ReviewerType.External
             && r.Accepted
             && !r.IsDeleted)
    .OrderByDescending(r => r.SubmittedAt)
    .FirstOrDefaultAsync(ct);
...
foreach (var edit in edits)
{
    if (edit.SectionIndex < 0 || edit.SectionIndex >= sections.Count) { ...continue; }
    sections[edit.SectionIndex].Content = edit.TranslatedText;
}
translation.TranslatedSections = JsonSerializer.Serialize(sections);
```

Confirmed via repository-wide search: `ConfirmExternalReview` has exactly zero call sites outside its interface declaration (`ITranslationWorkflowService.cs`), its implementation, and the unit test file (`tests/QuantumBuild.Tests.Integration/Workflows/TranslationWorkflowServiceTests.cs`). No controller in `src/QuantumBuild.API/Controllers/**` references it. This matches and independently corroborates the prior recon's finding — the endpoint genuinely does not exist.

**Important nuance on "silently discard":** the reviewer's `WorkflowReview` row (and its `EditedContent` JSON) is **not physically deleted** by `AcceptAsFinal`. It remains in the `workflows."WorkflowReviews"` table indefinitely (append-only; see §7). What is lost is *reachability*: `PropagateExternalReviewEditsAsync` is only ever invoked from `ConfirmExternalReview`, which requires the live state to still be `ThirdPartyReviewed`. Once `AcceptAsFinal` fires, state becomes `Accepted`, and the guard permanently blocks recovery through any existing code path — the data would need a manual DB read (or a future targeted repair script/relaxed guard) to recover, not just "add the missing endpoint and click it." So the practical effect for an admin using only the product is indistinguishable from true data loss.

## 5. UI surfaces (Q4)

**"Review" button on `TranslationWorkflowPanel`** (`web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx:66-68, 343-356`):

```ts
function canReview(state: TranslationWorkflowState): boolean {
  return state === 'Validated' || state === 'ReviewerAccepted' || state === 'ThirdPartyReviewed';
}
...
<Button ... disabled={isBusy || !canReview(row.state)}
  onClick={() => router.push(`/admin/toolbox-talks/talks/${toolboxTalkId}/translations/${row.languageCode}/review`)}>
  Review
</Button>
```

Confirmed: this component has **no** Accept button of its own anywhere (grepped the full file) — "Review" is the only action available for `ThirdPartyReviewed`, and it routes to the internal review page. This matches the prior recon's description exactly.

**Destination — `TranslationReviewPage`** (`web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/translations/[languageCode]/review/page.tsx:29-47, 170-177`):

```ts
const runId = matchedState?.lastValidationRunId ?? null;
const { data: runDetail } = useValidationRun(runId ? talkId : null, runId);

const canAccept = useMemo(
  () =>
    runDetail?.results.every(
      (r) => r.reviewerDecision != null && r.reviewerDecision !== 'Pending'
    ) ?? false,
  [runDetail]
);
...
<ReviewScreen ... runDetail={runDetail} canAccept={canAccept} ... />
```

`runId` comes from `matchedState.lastValidationRunId` — the most recent *validation run* on this talk/language, which predates external review entirely (external review submission does not create a new `TranslationValidationRun`; it only writes a `WorkflowReview` row — see the prior recon §2 Step 2). So the sections rendered are the pre-external-review AI translation and internal accept/edit/retry decisions, exactly as the prior recon described.

`canAccept` depends only on every section's **internal** `reviewerDecision` (on `TranslationValidationResult`) being non-`Pending`. Internal review is a prerequisite to even reach `ReviewerAccepted` (required before `Send for review`/`initiate-external-review` is available at all — `canSendForExternalReview` requires `Validated`/`ReviewerAccepted`), so by the time a talk reaches `ThirdPartyReviewed`, every section's internal decision was already made in an earlier phase. **`canAccept` is therefore already `true` when an admin lands on this page for a `ThirdPartyReviewed` language** — the button is not merely present, it is immediately clickable with no additional step.

**The "Accept this language as final" button** (`web/src/features/toolbox-talks/components/ReviewScreen.tsx:234-258`):

```tsx
<Button
  disabled={!canAccept || acceptTranslation.isPending}
  onClick={() => {
    acceptTranslation.mutate(
      { toolboxTalkId, languageCode },
      {
        onSuccess: () => {
          toast.success(`${languageCode.toUpperCase()} translation accepted as final`);
          router.push(`/admin/toolbox-talks/talks/${toolboxTalkId}`);
        },
        onError: () => { toast.error('Failed to accept translation'); },
      }
    );
  }}
>
  Accept this language as final
</Button>
```

`useAcceptTranslation` (`web/src/lib/api/toolbox-talks/use-toolbox-talks.ts:229-233`) calls `acceptTranslation()` (`toolbox-talks.ts:649-654`) → `apiClient.post(\`/toolbox-talks/${id}/translations/${languageCode}/accept\`)` — the exact `AcceptAsFinal` endpoint from §2-4.

**Confirmed end-to-end: the only Review-shaped UI path for `ThirdPartyReviewed` state leads to a screen whose primary call-to-action button is pre-enabled and, when clicked, fires the endpoint that discards the reviewer's edits.**

## 6. UI-level protection (Q5)

**None found.**

- `WorkflowStateBadge` (`web/src/features/toolbox-talks/components/WorkflowStateBadge.tsx:76-81`) renders `ThirdPartyReviewed` with label *"External review complete"* and tooltip *"External reviewer submitted; awaiting final accept"*. This tooltip is arguably actively misleading — it frames "accept" as the next correct step, with no hint that doing so via this screen discards the reviewer's actual submitted text.
- `ReviewScreen.tsx` has exactly one conditional warning banner in the file — the amber "edited source" notice at lines 222-231, which fires on `hasEditedSource` (an *internal* `TranslationValidationResult.editedSource` flag, unrelated to external review). There is no analogous banner keyed off workflow state or off the existence of an external `WorkflowReview`.
- No confirm dialog, no distinct copy, no disabling of the Accept button based on `matchedState.state === 'ThirdPartyReviewed'` anywhere in `ReviewScreen.tsx` or its parent page — both files were read in full for this recon; grepped for `ThirdPartyReviewed` in `web/src/features/toolbox-talks/components/ReviewScreen.tsx` and the review `page.tsx`: zero matches in either.
- The frontend has no reference to `WorkflowReview` or `EditedContent` anywhere under `web/src/**` (confirmed by repo-wide grep) — so structurally, the UI cannot warn about content it never fetches in the first place.

## 7. Other write paths that could clobber `WorkflowReview.EditedContent` (Q6)

Repository-wide grep for `WorkflowReviews` / `EditedContent` (excluding EF migration snapshots, which are historical schema records, not runtime code) finds exactly three write sites, all in `TranslationWorkflowService.cs`:

1. `SubmitExternalReview` (line 313) — creates the row, `EditedContent = editedContent` (the reviewer's actual submission).
2. `DeclineExternalReview` (line 608) — creates a row with `EditedContent = null` (decline path; no edits exist to lose).
3. `PropagateExternalReviewEditsAsync` (line 976) — **reads** the row (`context.WorkflowReviews.Where(...).FirstOrDefaultAsync`), never writes to it.

There is no `Update`, `Remove`, or soft-delete call against `WorkflowReview` anywhere in the codebase. The FK from `WorkflowReview.ExternalParticipantInvitationId` to `ExternalParticipantInvitation` is configured `OnDelete(DeleteBehavior.SetNull)` (`WorkflowReviewConfiguration.cs:54-57`), so even if an invitation row were deleted, the review row (and its `EditedContent`) survives with the FK nulled out.

**Conclusion: `WorkflowReview` rows are effectively append-only/immutable once written. There is no second write path that overwrites or deletes `EditedContent`.** The risk is entirely the reachability problem described in §4 — the data persists, but `AcceptAsFinal` closes the only door (`ThirdPartyReviewed` state) that a correct propagation path (`ConfirmExternalReview`) is gated behind.

One adjacent, lower-severity compounding path worth flagging: once `AcceptAsFinal` has (incorrectly) moved a language to `Accepted`, the `TranslationWorkflowPanel`'s "Translate" button becomes enabled again (`isTranslateButtonEnabled` includes `Accepted` — `TranslationWorkflowPanel.tsx:58-60`), guarded only by a confirmation dialog whose copy ("Reviewer edits and validation results for this language will be replaced with a fresh AI translation" — lines 479-484) refers to *internal* `TranslationValidationResult.EditedTranslation`/`EditedSource`, not the external `WorkflowReview.EditedContent` the admin has no visibility into. Re-translating at this point would overwrite `TranslatedSections` a second time with fresh AI output, compounding the loss but not changing its fundamental cause.

## 8. Recommended shape of the defensive fix

Block `AcceptAsFinal` from being invoked while the source state is `ThirdPartyReviewed` — remove `ThirdPartyReviewed` from its permitted-state guard at `TranslationWorkflowService.cs:748-750` so the state machine itself refuses the transition (returning `WorkflowInvalidState`, the same failure code already used for every other illegal transition in this service), and correspondingly disable/hide the resulting `Conflict` case on the frontend by having `ReviewScreen`'s `canAccept` also require `matchedState.state !== 'ThirdPartyReviewed'` (passed down from the review page, which already has `matchedState.state` in scope) so the button doesn't invite a doomed click in the first place. This is a purely defensive stop-gap — it does not build the missing "see what the reviewer wrote and confirm" surface (still the primary gap identified in the prior recon), but it closes the one-click data-loss trap immediately without requiring that larger UI to exist yet. Once the dedicated confirm-external-review endpoint and UI are built (per the prior recon's §8), the guard on `AcceptAsFinal` can stay as-is permanently — `ThirdPartyReviewed` should never again be a legitimate source state for the generic accept action, since `ConfirmExternalReview` becomes the sole correct exit from that state.
