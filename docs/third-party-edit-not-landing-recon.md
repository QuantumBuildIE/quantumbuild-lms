# Third-Party (External) Reviewer Edit Not Landing — Recon

**Date:** 2026-07-08
**Branch:** transval
**Scope:** Read-only investigation. No application files, migrations, or data modified.

---

## 1. Headline

**Break identified — `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` (missing endpoint) and `TranslationWorkflowPanel.tsx`/`ReviewScreen.tsx` (missing UI surface).**

The external reviewer submission path itself (token verification → submit → DB write → workflow event → state transition to `ThirdPartyReviewed`) is implemented correctly, including tenant handling. The break is **downstream of that**: `ITranslationWorkflowService.ConfirmExternalReview(...)` — the only method in the entire codebase that copies the reviewer's edited text out of the holding table (`WorkflowReview.EditedContent`) into the live `ToolboxTalkTranslation.TranslatedSections` field that employees/admins actually see — **has no HTTP endpoint anywhere in the API**. It is fully implemented, fully unit-tested, and completely unreachable from any UI action. There is also no UI surface anywhere in the authenticated app that reads or displays the reviewer's actual submitted edits.

Net effect: the reviewer's submission is durably recorded (row + event), the workflow state **does** advance to `ThirdPartyReviewed`, but there is no code path by which:
- an internal admin can see what the reviewer actually wrote, or
- the edited content can ever be propagated into the published/visible translation.

This matches the boss's report closely on the "edited content is not visible" symptom, and partially explains "workflow state does not advance" (it advances in the DB, but there is no user-facing action available at `ThirdPartyReviewed` that does anything meaningful with it — see §3 and §5 for the nuance).

---

## 2. The full chain, step by step

### Step 1 — Invitation created (internal admin action, not the reviewer)

`TranslationWorkflowService.InitiateExternalReview` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Workflows/TranslationWorkflowService.cs:336-428`

- Called from `POST /api/toolbox-talks/{id}/translations/{languageCode}/initiate-external-review` (`ToolboxTalksController.cs:1880-1915`), gated `[Authorize(Policy = "Learnings.Manage")]`.
- Valid only from workflow state `Validated` or `ReviewerAccepted` (line 350).
- Creates an `ExternalParticipantInvitation` row (line 367-382):
  ```csharp
  var invitation = new ExternalParticipantInvitation
  {
      TenantId = tenantId,                       // = ResolveTenantId(explicitTenantId) — the ADMIN's tenant, correctly captured
      WorkflowType = WorkflowType.Translation,
      TargetEntityId = talkId,
      TargetEntitySubKey = languageCode,
      InvitedEmail = invitedEmail,
      TokenHash = tokenHash,                      // SHA-256 of a Guid.NewGuid("N") raw token
      ExpiresAt = expiresAt,                      // default 30 days, tenant-configurable
      Status = InvitationStatus.Pending,
      ContextType = "TranslationReview",
      ...
  };
  ```
- Writes `WorkflowEventTypes.ExternalReviewInitiated` (line 384) → state becomes `AwaitingThirdParty` (event map, line 820).
- Sends the invitation email via `IEmailService.SendExternalReviewInvitationEmailAsync` (line 411) with `portalUrl = "{baseUrl}/external-review/{rawToken}"` (line 396). Failure to send is caught and logged as a warning only (line 414-419) — the invitation row is committed either way.
- **Frontend trigger:** `TranslationWorkflowPanel.tsx:366-382` — "Send for review" button, visible when `canSendForExternalReview(state)` is `Validated`/`ReviewerAccepted` (line 70-72). Calls `useInitiateExternalReview` → `POST .../initiate-external-review`.

This step is correctly implemented and not in question — the trial reviewer clearly got a real link (see §4 tenant-context finding for how the raw token maps back to tenant).

### Step 2 — Reviewer opens the URL (public, no JWT)

Page: `web/src/app/external-review/[token]/page.tsx` — outside the `(authenticated)` route group, structurally analogous to `/qr/[codeToken]` but its own independent implementation (does **not** reuse `QrSession`/`QrScanController` machinery — verified independently, no shared code).

- On mount (line 100-128), calls `getExternalReviewPortal(token)` → `GET {NEXT_PUBLIC_API_URL}/external-review/{token}` (`web/src/lib/api/external-review.ts:8-14`) — a **plain `fetch`**, not the Axios `apiClient` used elsewhere in the app (no JWT interceptor involved — correct, since there is no session).
- Backend: `ExternalReviewController.GetPortalContext` (`src/QuantumBuild.API/Controllers/ExternalReviewController.cs:24-53`) → `TranslationWorkflowService.GetPortalContext(token, ct)` (`TranslationWorkflowService.cs:642-733`).
  - Looks up the invitation by `TokenHash` with `.IgnoreQueryFilters()` (line 648-650) — correct, since there is no tenant/JWT context to filter by.
  - Derives `portalStatus` (`Active`/`Used`/`Revoked`/`Expired`) from `InvitationStatus` + `ExpiresAt` (`DerivePortalStatus`, line 867-878).
  - If `Active`, loads the latest **completed** `TranslationValidationRun` for that talk+language and its `TranslationValidationResults` (the AI-generated back-translation sections), and returns them as `ExternalReviewSectionDto[]` (line 684-717) — this is the **original AI section content**, not anything from a prior reviewer round.
  - Controller returns `200` for `Active`/`Used`, `410 Gone` for `Revoked`/`Expired`, `404` for unknown token.

This step works correctly for a first-time visit to a valid, unexpired, unused token.

### Step 3 — Reviewer submits

Frontend: `web/src/app/external-review/[token]/page.tsx:130-153`
```tsx
const editedArray: ExternalReviewEditedSectionDto[] = Object.entries(editedSections).map(
  ([idx, text]) => ({ sectionIndex: parseInt(idx, 10), translatedText: text })
);
const editedContent = JSON.stringify(editedArray);
const { status } = await submitExternalReview(token, { accepted: true, editedContent });
```
- `accepted` is **hard-coded to `true`** on Submit — there is no reject option in the "Active" step; a separate "Decline" button (before starting) calls the `/decline` endpoint with a mandatory reason instead. This matches the documented product pattern of no explicit reject on the reviewer side (analogous to internal reviewer's Accept/Edit-only UI per CLAUDE.md §TransVal notes).
- `submitExternalReview` → `POST {API}/external-review/{token}/submit` (`external-review.ts:16-29`).

Backend: `ExternalReviewController.Submit` (`ExternalReviewController.cs:58-92`) → `TranslationWorkflowService.SubmitExternalReview(token, accepted, editedContent, ct)` (`TranslationWorkflowService.cs:430-500`).

### Step 4 — Handler writes to DB, under what tenant context

`SubmitExternalReview` (`TranslationWorkflowService.cs:430-500`):

```csharp
var invitation = await context.ExternalParticipantInvitations
    .IgnoreQueryFilters()                                    // line 436 — correct: no JWT tenant to filter by
    .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && !i.IsDeleted, ct);

if (invitation is null) return Result.Fail(..., FailureCode.WorkflowTokenInvalid);
if (invitation.Status == InvitationStatus.Used) return Result.Fail(..., FailureCode.WorkflowTokenAlreadyUsed);
if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt < DateTime.UtcNow)
    return Result.Fail(..., FailureCode.WorkflowTokenExpired);

var currentState = await GetStateIgnoringTenantAsync(invitation.TargetEntityId, invitation.TargetEntitySubKey, ct); // line 449 — also IgnoreQueryFilters, no tenant predicate at all (not even explicit)
if (currentState != TranslationWorkflowState.AwaitingThirdParty)
    return Result.Fail(..., FailureCode.WorkflowInvalidState);

invitation.Status = InvitationStatus.Used;
invitation.UsedAt = DateTime.UtcNow;

context.WorkflowReviews.Add(new WorkflowReview {
    TenantId = invitation.TenantId,               // *** tenant sourced from the invitation record, NOT ICurrentUserService ***
    WorkflowType = invitation.WorkflowType,
    TargetEntityId = invitation.TargetEntityId,
    TargetEntitySubKey = invitation.TargetEntitySubKey,
    ReviewerType = ReviewerType.External,
    ExternalParticipantInvitationId = invitation.Id,
    EditedContent = editedContent,
    Accepted = accepted,
    SubmittedAt = DateTime.UtcNow
});

context.WorkflowEvents.Add(new WorkflowEvent {
    TenantId = invitation.TenantId,               // *** same pattern — tenant from invitation ***
    WorkflowType = WorkflowType.Translation,
    TargetEntityId = invitation.TargetEntityId,
    TargetEntitySubKey = invitation.TargetEntitySubKey,
    EventType = WorkflowEventTypes.ExternalReviewSubmitted,
    TriggeredByType = TriggeredByType.User,
    TriggeredByUserId = null,                     // correctly null — no authenticated user exists
    PayloadJson = Serialize(new { accepted }),
    OccurredAt = DateTime.UtcNow
});

await context.SaveChangesAsync(ct);               // *** single atomic save — review row + event row committed together ***
```

Both the `WorkflowReview` and the `WorkflowEvent` are added to the same `DbContext` and flushed in **one** `SaveChangesAsync` call (line 488). There is no partial-write window between "review recorded" and "event recorded" — they succeed or fail together.

### Step 5 — Workflow event / state transition

- `EventTypeToState` (`TranslationWorkflowService.cs:813-829`): `ExternalReviewSubmitted → ThirdPartyReviewed` (line 821). This mapping is correct and present.
- `GetState`/`GetHistory` (the authenticated-admin-facing read paths, lines 63-153) both apply `.IgnoreQueryFilters()` **plus an explicit** `e.TenantId == tenantId` predicate — this is the correct pattern (Note 14 style) and matches what `AddEvent` and `SubmitExternalReview` wrote, so an admin querying their own tenant's talk **will** see the `ExternalReviewSubmitted` event and the `ThirdPartyReviewed` state, provided the invitation's `TenantId` was correct at creation time (it is — see Step 1).
- Post-write, `NotifyExternalReviewResponseAsync` is called (line 496-497) to email tenant Admins that a response arrived (subject to `ToolboxTalkSettings.NotifyOnExternalReviewResponse`, default `true`). This notification email links to `{baseUrl}/admin/toolbox-talks/talks/{talkId}/validation` (`ToolboxTalkNotificationService.cs:216`) — the validation-runs list page, **not** any page that shows the reviewer's actual edits (there isn't one — see §3).

**Conclusion on this step: the event write is unconditional (same transaction as the review row), not gated on any condition, and not swallowed by a try/catch that could silently drop it.** The only try/catch adjacent to this method wraps `NotifyExternalReviewResponseAsync`'s caller context is nonexistent — that notification call is unwrapped, but `ToolboxTalkNotificationService` methods internally swallow their own exceptions per their class doc ("All methods swallow exceptions so a notification failure never breaks the calling operation") — a notification failure could not roll back or hide the review/event write, since `SaveChangesAsync` already committed by that point.

### Step 6 — "Our-side" read path for the reviewer's content: **this is where the chain actually breaks**

There are two entirely separate places reviewer/edit content could be surfaced, and neither works end-to-end:

**(a) The "Review" button / page for `ThirdPartyReviewed` state**

`TranslationWorkflowPanel.tsx:66-68`:
```ts
function canReview(state: TranslationWorkflowState): boolean {
  return state === 'Validated' || state === 'ReviewerAccepted' || state === 'ThirdPartyReviewed';
}
```
Clicking "Review" while `state === 'ThirdPartyReviewed'` routes to `/admin/toolbox-talks/talks/{id}/translations/{languageCode}/review` (line 348-352), which renders `TranslationReviewPage` (`web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/translations/[languageCode]/review/page.tsx`) → `ReviewScreen`.

This page/component is **the internal-reviewer accept/edit/retry workflow surface for `TranslationValidationResult` rows** (the AI-generated back-translation sections from the original validation run — `matchedState.lastValidationRunId`, line 34). It has **no code path that reads `WorkflowReview` at all** — confirmed by grep: `WorkflowReview`/`EditedContent` appear only in backend C# files (entity, config, service, tests) and never in any `web/src/**` file. The external reviewer's actual submitted text (`WorkflowReview.EditedContent`, a JSON array of `{sectionIndex, translatedText}`) is simply never fetched or rendered anywhere in the authenticated app.

So an admin who clicks "Review" on a `ThirdPartyReviewed` language sees the same screen they'd see for `Validated` — the pre-review AI translation and internal accept/edit/retry controls — with **no visual indication that an external reviewer already responded**, and no way to view what they wrote.

**(b) `ConfirmExternalReview` — the only code that would actually apply the reviewer's edits**

`TranslationWorkflowService.ConfirmExternalReview` (lines 502-532) is the sole method that calls `PropagateExternalReviewEditsAsync` (lines 970-1060), which:
1. Loads the most recent **accepted** external `WorkflowReview` for the talk+language.
2. Deserialises `EditedContent` into `List<ExternalReviewEditedSectionDto>`.
3. Loads the `ToolboxTalkTranslation` row, deserialises `TranslatedSections`.
4. Overwrites each section's `Content` at the matching `SectionIndex`.
5. Re-serialises and assigns back to `translation.TranslatedSections` — **this is the only line in the entire codebase that writes the reviewer's edits into the field employees/admins actually see.**

`ConfirmExternalReview` requires source state `ThirdPartyReviewed` (line 516-519) and is declared on the interface (`ITranslationWorkflowService.cs:69`), fully implemented, and covered by unit tests (`tests/QuantumBuild.Tests.Integration/Workflows/TranslationWorkflowServiceTests.cs` — tests #8, #22, #38-42, e.g. `ConfirmExternalReview_WithAcceptedTrue_PropagatesEditedSectionsIntoTranslatedSections`).

**Grep across the entire repository for `ConfirmExternalReview` finds exactly three call sites: the interface declaration, the implementation, and the test file.** There is no controller action, no Hangfire job, and no other service that calls it. Confirmed by:
- Full-text search of `src/QuantumBuild.API/**` for `ConfirmExternalReview` → 0 matches.
- `ToolboxTalksController.cs` has endpoints for `accept` (`AcceptAsFinal`, line 1787), `cancel-external-review` (`CancelExternalReview`, line 1832), and `initiate-external-review` (`InitiateExternalReview`, line 1880) — **but no `confirm-external-review` route exists.**

Note also that the general-purpose `accept` endpoint (`POST .../translations/{languageCode}/accept` → `AcceptAsFinal`) is valid from `ThirdPartyReviewed` (doc comment line 1781: "Valid from states: Validated, ReviewerAccepted, ThirdPartyReviewed") and **does** transition state to `Accepted` — but `AcceptAsFinal` (lines 735-760) only writes the `AcceptedAsFinal` event; it never calls `PropagateExternalReviewEditsAsync`. So even if an admin somehow triggers this transition, the reviewer's edits are silently discarded — state moves to `Accepted` while `TranslatedSections` still contains the pre-review AI text.

**This is the concrete break: `ConfirmExternalReview` — and therefore `PropagateExternalReviewEditsAsync` — is unreachable in production. The reviewer's edited content can never land in `TranslatedSections` via any user-triggered action that currently exists.**

---

## 3. Where the chain breaks — summary

| Step | Component | Status |
|---|---|---|
| Invitation created + emailed | `InitiateExternalReview` + `ToolboxTalksController` + `TranslationWorkflowPanel` | ✅ Works |
| Reviewer opens link, sees sections | `GetPortalContext` + `/external-review/[token]/page.tsx` | ✅ Works (for a first, valid, unexpired, unused token) |
| Reviewer submits edits | `SubmitExternalReview` | ✅ Works — `WorkflowReview` + `WorkflowEvent` written atomically, correct tenant, no swallowed exceptions |
| Workflow state advances | `ExternalReviewSubmitted → ThirdPartyReviewed` | ✅ Advances correctly in the DB and is queryable by an authenticated admin of the correct tenant |
| Admin notified by email | `NotifyExternalReviewResponseAsync` | ✅ Fires (subject to tenant notification toggle), but links to a page (`.../validation`) that does not show the reviewer's edits |
| Admin views reviewer's actual edited text | *(no code path)* | ❌ **Break** — nothing in the authenticated frontend reads `WorkflowReview.EditedContent`; the "Review" button leads to the unrelated internal-reviewer screen |
| Edits propagate into `TranslatedSections` | `ConfirmExternalReview` → `PropagateExternalReviewEditsAsync` | ❌ **Break** — fully implemented and unit-tested, but has **no HTTP endpoint**; unreachable from any UI action |
| Admin clicks generic "Accept" instead | `AcceptAsFinal` | ⚠️ Reachable from `ThirdPartyReviewed`, transitions to `Accepted`, but **silently discards** the reviewer's edits (never calls the propagation helper) |

**So: the workflow event log is correct and complete for the submission itself. The state machine does advance to `ThirdPartyReviewed`. The two things that are actually broken are (1) there is no way for staff to see what the reviewer wrote, and (2) there is no way to make it count even if they could see it — both because a fully-built service method (`ConfirmExternalReview`) was never wired to a controller route or a UI action.** This is a "scaffolded but not wired" gap of exactly the shape called out as a risk pattern elsewhere in this codebase (cf. Note 32 in CLAUDE.md, a different but structurally similar "partial migration left the system worse than either old or new state" class of bug).

---

## 4. The tenant context question — explicit answer

**Does the submit handler run under a valid tenant context, or under `Guid.Empty`? Is this the break?**

**No — this is not the break. The submit handler is well-designed for the anonymous case and does not fall into the `Guid.Empty` trap.**

Evidence:
- `SubmitExternalReview` and `DeclineExternalReview` never call `ICurrentUserService` at all (there is no authenticated user in this request). Instead, both derive `TenantId` **from the `ExternalParticipantInvitation` row itself** (`invitation.TenantId`), which was correctly stamped with the real tenant ID at *invitation-creation* time by an authenticated admin (`ResolveTenantId(explicitTenantId)` in `InitiateExternalReview`, falling back to `currentUser.TenantId` under a real HTTP/JWT context).
- Every EF query in the anonymous path (`SubmitExternalReview`, `DeclineExternalReview`, `GetPortalContext`, `GetStateIgnoringTenantAsync`) explicitly calls `.IgnoreQueryFilters()` — necessary because there is no JWT and the global tenant query filter (`BypassTenantFilter || TenantId == currentTenantId`, per CLAUDE.md Note 14) would otherwise silently return nothing.
- The `WorkflowReview` and `WorkflowEvent` rows written on submission both use `invitation.TenantId` explicitly, not any ambient/default value.
- The DB probe (see §5) found zero rows in `workflows."ExternalParticipantInvitations"` in the local dev database, so a live all-zeros-tenant example could not be directly observed here — but the source-code evidence is unambiguous: there is no code path in this flow that would produce a `Guid.Empty` tenant. This is the opposite of the Note 14 bug class — this flow explicitly threads tenant context from the invitation record precisely to avoid it, and does so correctly.

**Conclusion: the tenant-context question is a dead end for this incident. The bug is not a cross-tenant/`Guid.Empty` write; it is a missing endpoint + missing UI surface, several steps downstream of the write.**

---

## 5. Data probe (local dev DB)

Connected to `postgres@127.0.0.1:5432/rascor_stock` (trust auth, empty password, confirmed working via `psql`).

```
workflows.ExternalParticipantInvitations: 0 rows
workflows.WorkflowEvents:                 0 rows (all event types, not just external-review)
workflows.WorkflowReviews:                0 rows
toolbox_talks.ToolboxTalks:               34 rows (real content exists)
```

**This local database has never recorded a single workflow event of any kind**, despite containing 34 real toolbox talks. This means:
1. This local machine was **not** where the trial reviewer's session happened (the trial almost certainly ran against a Railway-deployed Development or Production instance, per the branch/deploy strategy documented in CLAUDE.md).
2. No local reproduction was possible from this database — there is nothing to inspect for a suspicious `Guid.Empty` `TenantId` or similar because there is no external-review data here at all.
3. This does **not** contradict the code-level findings in §2-4; it simply means the DB probe could not corroborate or refute them with real trial data. The break identified in §2-3 is a static, deterministic gap (missing route) that would manifest identically regardless of which environment the trial ran in.

No data was created, modified, or deleted during this probe (read-only `SELECT`/`\dt`/`\dn` only).

---

## 6. Log probe

No local application log files exist for the .NET API. Searched the full repository tree (excluding `bin`/`obj`/`node_modules`) for `*.log` and `logs/` paths — the only hits were `.git/logs/**` (git ref logs, irrelevant) and `web/.next/dev/logs/next-development.log` (Next.js dev-server build output, not API runtime logs).

`appsettings.json` has no Serilog configuration — the API uses default ASP.NET Core console logging, which on a Railway deployment is captured as ephemeral container stdout in Railway's own log viewer, not written to any file this machine can access.

**Railway log inspection for the actual trial period (2026-07-01 to 2026-07-08) would need to happen out-of-band** — via the Railway dashboard/CLI against whichever service (Development or Production) the trial reviewer's link pointed to. This recon cannot perform that step from this environment.

---

## 7. Secondary issues surfaced

1. **No endpoint for `ConfirmExternalReview`** (primary break — see §2 Step 6b, §3).
2. **No UI reads `WorkflowReview.EditedContent` anywhere** — even if the endpoint existed, there is currently no screen that would let an admin *see* the reviewer's specific edits before confirming them (primary break — see §2 Step 6a, §3).
3. **`AcceptAsFinal` is reachable from `ThirdPartyReviewed` and silently discards reviewer edits** if used as a workaround for the missing confirm action — an admin who clicks the generic "Accept" (if any UI path currently exposes it for this state — the audited `TranslationWorkflowPanel` does not appear to expose an Accept button in this state, only "Review", but the endpoint itself imposes no such restriction) would move the language to `Accepted` with the pre-review AI translation, not the reviewer's edits, and no error or warning would be surfaced.
4. **`WorkflowHistoryModal.tsx` event-label map is incomplete**: `EVENT_TYPE_LABELS` (lines 22-35) is missing entries for `ExternalReviewConfirmed`, `ExternalReviewCancelled`, and `ExternalReviewDeclined` (it has `ConfirmExternalReviewAccepted`/`ConfirmExternalReviewRejected`, which do not match any actual value in `WorkflowEventTypes.cs` — those constants don't exist; the real constant is `ExternalReviewConfirmed`). This is cosmetic only (the `eventLabel()` fallback renders the raw event-type string if unmapped) and does not affect `ExternalReviewSubmitted`, which **is** correctly mapped ("External review submitted") — so the event itself is visible in the history modal today, just not the associated edited content.
5. **Admin notification email links to the wrong page.** `ToolboxTalkNotificationService.NotifyExternalReviewResponseAsync` (line 216) links recipients to `/admin/toolbox-talks/talks/{talkId}/validation` (the validation-runs list), which has no relationship to the external reviewer's submission and would not show anything new to the admin who clicks through.
6. **The reviewer-facing Submit action always sends `accepted: true`** (`external-review/[token]/page.tsx:141`) — there is no "reject with edits" option on the Active step; rejection is only expressed via the separate pre-review "Decline" flow (with mandatory reason). This appears to be an intentional product simplification (mirrors the internal-reviewer "no explicit reject" pattern documented for TransVal), not a bug, but is worth confirming with product intent given `ConfirmExternalReview(accepted: bool)` still supports a `false` path that currently has no way to be exercised from the reviewer's side.

---

## 8. Recommended next step

This is a **backend + frontend wiring gap**, not a data-corruption or tenant-isolation bug. The fix shape (for a future implementation prompt, not attempted here) would need to cover, atomically per Note 32's "complete or leave intact" principle:

1. A new controller endpoint (e.g. `POST /api/toolbox-talks/{id}/translations/{languageCode}/confirm-external-review`, body `{ accepted: bool }`) that calls `ITranslationWorkflowService.ConfirmExternalReview`.
2. A UI surface that:
   - Shows the admin the external reviewer's actual submitted content (probably a distinct screen/section reading `WorkflowReview.EditedContent` for the language, decoded per-section) rather than routing `ThirdPartyReviewed` into the unrelated internal `ReviewScreen`.
   - Lets the admin confirm (→ calls the new endpoint, `accepted: true`, propagates edits) or reject (→ `accepted: false`, no propagation, but still needs a defined next state/UX — currently `ConfirmExternalReview(accepted: false)` just writes `ExternalReviewConfirmed` and moves state to `Accepted` per the event map, which may itself need product clarification: should a rejected external review really land on `Accepted`?).
3. Fix the `WorkflowHistoryModal` event-label map (§7 item 4) as a small cleanup alongside.
4. Reconsider whether `AcceptAsFinal` should be blocked (or should itself trigger propagation) when called from `ThirdPartyReviewed`, to close the silent-discard path in §7 item 3.

**Before scoping that work, it would be worth asking the boss for the reviewer's actual browser network tab or the literal invitation URL used during the trial** — this would let us confirm (a) which environment (Development vs Production) the trial ran against, and (b) whether the token expired/was already used/hit some other guard in `SubmitExternalReview` (§2 Step 4) before ever reaching the point where this recon's findings apply. Everything in this document explains why the reviewer's edits would never surface *even on a fully successful submission* — it does not rule out an additional environment-specific issue (e.g., email deliverability, a stale token) compounding the problem, which only Railway logs or the reviewer's own browser history could confirm.
