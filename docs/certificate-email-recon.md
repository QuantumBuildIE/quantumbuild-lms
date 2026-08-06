# Certificate Email Recon

Date: 2026-07-16
Scope: Read-only static code analysis. No source files modified.

## Background

Certificate generation itself is confirmed working (fixed in `2cc0f42`): `ToolboxTalkCertificate` rows
are created, the PDF uploads to R2, the UI shows the certificate, and download works. Operators are
not receiving an email telling them the certificate is available. `EmailProvider__Provider=MailerSend`
is confirmed set in Development, so this is **not** the StubEmailProvider issue diagnosed previously
for assignment emails (that one was schedules stuck in `Draft` instead of `Active`).

---

## Part 1 — CertificateGenerationService email path

File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/CertificateGenerationService.cs`

- `GenerateTalkCertificateAsync` (lines 22–111) and `GenerateCourseCertificateAsync` (lines 113–201) are
  the only two public methods. Both follow the identical shape:
  1. Load talk/course, employee, verify `GenerateCertificate` flag.
  2. Build the `ToolboxTalkCertificate` entity.
  3. Generate the PDF (`GenerateCertificatePdf`, lines 228–367) and upload to R2 (`UploadCertificatePdf`,
     lines 213–226).
  4. `context.ToolboxTalkCertificates.Add(certificate); await context.SaveChangesAsync(ct);`
  5. **`return certificate;`** — that is the last line of both methods (line 110 and line 200).

- **After the `SaveChangesAsync` that persists the certificate row, the method does nothing else.** No
  call to any email service, no domain event/MediatR notification raised, no Hangfire job enqueued.
  The method's job ends at "persist + return the entity." Email is entirely the caller's responsibility,
  and every caller (Part 4 below) declines that responsibility.

- This class has no constructor dependency on `IToolboxTalkEmailService`, `IEmailService`, or
  `IEmailProvider` at all — confirmed by inspecting the primary constructor (lines 16–20): only
  `IToolboxTalksDbContext`, `IR2StorageService`, `ITenantSettingsService`, `ILogger`. There is no email
  service injected into this class, so there is nothing inside it that *could* silently swallow an
  email failure — the swallowing pattern noted in the prior certificate-generation recon (exception
  caught, logged, `CertificateGenerationFailed` never set) applies only to certificate PDF
  generation/upload failures, not to any email step, because no email step exists in this file.

---

## Part 2 — Email service(s)

Two distinct email abstractions exist in this codebase; only one is relevant here.

### `IToolboxTalkEmailService` / `ToolboxTalkEmailService` (the relevant one)

- Interface: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkEmailService.cs`
- Implementation: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs`
- Registered in DI: `ServiceCollectionExtensions.cs:76` — `services.AddScoped<IToolboxTalkEmailService, ToolboxTalkEmailService>();` (present and correctly typed, not an interface-enqueue issue since this isn't a Hangfire job).
- Delivery path: constructor-injects `IEmailProvider` (`Core.Application.Abstractions.Email`), builds an
  inline HTML string per method, and calls `_emailProvider.SendAsync(emailMessage, cancellationToken)`
  directly — no Hangfire job wraps any of these calls.
- Seven methods exist: `SendTalkAssignmentEmailAsync`, `SendReminderEmailAsync`,
  `SendCompletionConfirmationEmailAsync` (lines 208–302), `SendEscalationEmailAsync`,
  `SendRefresherReminderAsync`, `SendCourseAssignmentEmailAsync`, `SendCourseRefresherReminderAsync`.
- **`SendCompletionConfirmationEmailAsync` is the one relevant template.** It builds a "Learning
  Completed" email and, at lines 235–240, conditionally appends a certificate section:
  ```csharp
  var certificateSection = "";
  if (!string.IsNullOrEmpty(completion.CertificateUrl))
  {
      certificateSection = $@"
      <p><a href='{completion.CertificateUrl}' style='color: #007bff;'>Download your completion certificate</a></p>";
  }
  ```
  This is an inline HTML string, not a separate template file — there is no template-registry lookup to
  fail; the string is always well-formed regardless of whether `CertificateUrl` is populated. No missing-
  template failure mode is possible here because there is no template file at all — everything is
  generated inline in C#, same pattern as every other method in this class.
- There is no dedicated "certificate available" / "certificate issued" template distinct from the
  completion-confirmation email — grepping the whole repo for `"certificate"` inside any email/template
  content turns up only this one inline conditional block. **There is no other certificate email
  template anywhere in the codebase**, wired or unwired.

### `IEmailService` / `EmailService` (the other one — not used for certificates)

- `src/Core/QuantumBuild.Core.Application/Interfaces/IEmailService.cs`
- Methods: `SendPasswordSetupEmailAsync`, `SendEmailAsync`, `SendPinEmailAsync`,
  `SendUserCreatedEmailAsync`, `SendExternalReviewInvitationEmailAsync`.
- None of these relate to toolbox talk completions or certificates — this is the Core-module identity/
  onboarding email service (password setup, QR PINs, user welcome, external reviewer invites). Confirmed
  irrelevant to the certificate flow; included only because the recon brief asked to check `IEmailService`.

**Conclusion for Part 2:** the correct method (`SendCompletionConfirmationEmailAsync`) exists, is fully
implemented, uses the same `IEmailProvider.SendAsync` delivery path as the confirmed-working assignment
email, and would correctly include the certificate link if `completion.CertificateUrl` is set. The
method is not a no-op. **The problem is that nothing calls it for standalone-talk completions.**

---

## Part 3 — Alternative trigger paths

Grepped for every caller of `GenerateTalkCertificateAsync` / `GenerateCourseCertificateAsync` and every
caller of `SendCompletionConfirmationEmailAsync` across `src/`.

Callers of certificate generation (3 total):
1. `CompleteToolboxTalkCommandHandler.cs:212` — standalone talk completion (employee-facing).
2. `CourseProgressService.cs:87` — course completion (fires when all required talks in a course are done).
3. `ToolboxTalksController.cs:2534` (`RegenerateCertificate` admin endpoint) — manual admin retry when
   generation previously failed.

Callers of `SendCompletionConfirmationEmailAsync`: **zero**, anywhere in `src/`. Confirmed via
`grep -rn "SendCompletionConfirmationEmailAsync" src/` — the only two hits are the interface declaration
and the implementation itself. No command handler, no controller, no Hangfire job, no MediatR
notification handler calls it.

No MediatR domain events or notification classes exist for certificate issuance or talk completion —
grepped for `INotification`, `TalkCompletedEvent`, `CertificateGeneratedEvent`, `CertificateIssuedEvent`
across `src/`: no matches. This codebase's event-like flows (e.g. TransVal's SignalR hub) are not used
here; toolbox-talk completion is a plain synchronous MediatR **command** handler with no notification
side-channel.

No Hangfire job exists for certificate email either — grepped the `Jobs/` folder
(`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/`) for "Certificate":
no matches. The 12 existing jobs (content generation, translations, schedules, reminders, validation,
etc. — see CLAUDE.md Background Jobs table) contain nothing certificate-related.

The QR Location Training path (`QrScanController.cs`, Note 10 in CLAUDE.md) creates
`ScheduledTalkCompletion` rows directly but never references "Certificate" at all — that path appears
not to generate certificates or certificate emails, which is consistent with (out of scope for this
recon, noted as an observation only).

**This is a clear GAP, not a subtler bug**: the trigger to call `SendCompletionConfirmationEmailAsync`
after a successful certificate generation was simply never added at any of the three certificate-
generation call sites.

---

## Part 4 — Comparison with the working assignment email flow

Confirmed-working pattern (`ProcessToolboxTalkSchedulesJob` → creates `ScheduledTalk` → calls
`_emailService.SendTalkAssignmentEmailAsync` inline, confirmed at
`ProcessToolboxTalkScheduleCommandHandler.cs:137`):

```
persist ScheduledTalk  →  same method, same call stack  →  _emailService.SendTalkAssignmentEmailAsync(...)
```

Certificate flow, traced through the actual call site
(`CompleteToolboxTalkCommandHandler.cs`, lines 199–241):

```csharp
// For standalone talks (not part of a course), schedule refresher directly
await _refresherSchedulingService.ScheduleRefresherIfRequired(scheduledTalk, cancellationToken);

// Generate certificate for standalone talks (course certs generated by CourseProgressService)
try
{
    var certificate = await _certificateService.GenerateTalkCertificateAsync(
        scheduledTalk, request.SignatureData, cancellationToken);

    if (certificate != null)
    {
        completion.CertificateUrl = certificate.PdfStoragePath;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    else
    {
        _logger.LogWarning(/* ... */);
        completion.CertificateGenerationFailed = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to generate certificate for scheduled talk {TalkId}", scheduledTalk.Id);
    // Don't rethrow — completion should still succeed
}

return new ScheduledTalkCompletionDto { /* ... */ };
```

**Structural difference:** the assignment flow persists, then in the *same method* calls the email
service. The certificate flow persists (`completion.CertificateUrl = ...; SaveChangesAsync(...)`) and
then the method simply returns the DTO — there is no equivalent `_emailService.SendCompletionConfirmationEmailAsync(completion, employee, cancellationToken)` call anywhere after line 220 (the success branch) or anywhere else in the method. `CompleteToolboxTalkCommandHandler` does not even have `IToolboxTalkEmailService` injected — checking the
constructor (lines 25–43): `IToolboxTalksDbContext`, `ICoreDbContext`, `ICurrentUserService`,
`IHttpContextAccessor`, `ICourseProgressService`, `IRefresherSchedulingService`,
`ICertificateGenerationService`, `ILogger`. **`IToolboxTalkEmailService` is absent from the dependency
list entirely** — the handler has no way to send this email even if someone added the call, without
first adding the dependency.

Same absence in `CourseProgressService.cs` (course-certificate path): constructor (lines 8–12) injects
`IToolboxTalksDbContext`, `IRefresherSchedulingService`, `ICertificateGenerationService`, `ILogger` —
again no `IToolboxTalkEmailService`.

This is not an event-driven design that's missing its handler (Part 3 already established there is no
event/notification infrastructure for this at all) — it's the simpler, more direct explanation: the
inline "persist then email" pattern used successfully for assignments was simply never replicated for
completions/certificates. The method that would need to make the call
(`SendCompletionConfirmationEmailAsync`) already exists and is fully built and functional; it is just
never invoked.

---

## Part 5 — Recent history

- Commit `80bad46` ("fix: certificate generation reliability improvements", 2026-03-31) — reviewed the
  full diff. It touched: `CompleteToolboxTalkCommandHandler.cs` (added the `else` branch + `CertificateGenerationFailed` flag — no email code), `ScheduledTalkCompletion` entity + EF config + DTOs (added
  `CertificateGenerationFailed` field), `R2StorageService.cs` (content-length capture fix, 6 upload
  methods), `ToolboxTalk.cs` (`GenerateCertificate` default flip), `TestTenantSeeder.cs`,
  `ToolboxTalksController.cs` (new `regenerate-certificate` endpoint), plus frontend files for the
  Completions report page and the regenerate button. **It did not touch `ToolboxTalkEmailService.cs`,
  `IToolboxTalkEmailService.cs`, or any email call site.** This commit made certificate generation
  failures visible and recoverable — it did not address (and was never scoped to address) certificate
  email delivery.

- `git log --oneline --all -S "SendCompletionConfirmationEmailAsync"` (pickaxe across all history,
  all branches): **one hit** — `9e575bf` ("feat: QuantumBuild LMS - standalone LMS extracted from Rascor
  (Core + ToolboxTalks)"). This is the commit that introduced the method into this repository's history.
  There is no subsequent commit that added or removed a call site — the method has been present and
  uncalled since the LMS was extracted as its own codebase. This strongly suggests the gap is not a
  regression introduced by any recent QuantumBuild-side change (including `80bad46` or `2cc0f42`); it
  looks like a pre-existing gap carried over from (or never closed since) the original extraction.

- `git log --oneline --all -S "CertificateEmail"`: no hits — confirms no differently-named
  certificate-email trigger was ever added and later removed.

- `git log` on `ToolboxTalkEmailService.cs` itself shows 4 commits total (`9e575bf` initial extraction,
  `98d8bae` base-URL fix, `279ae22` Toolbox Talk → Learning rename, `bc8f02a` tenant settings /
  `GetTeamNameAsync` addition) — none of the 3 post-extraction commits added a call site or a new
  certificate-specific method; they are cosmetic/config changes to the existing methods.

- `git log` on `CompleteToolboxTalkCommandHandler.cs` shows 4 commits (`9e575bf`, `db61575` UserId type
  change, `279ae22` rename, `80bad46` reliability fix already covered above) — none add an email call.

**Conclusion: this is not a regression from `2cc0f42` (the course-sync fix) or from `80bad46` (the
generation-reliability fix). Both of those commits are certificate-generation-side fixes, not
email-side. The email trigger has simply never existed for this flow since the module was extracted.**

---

## Diagnosis — ranked most to least likely

1. **Email trigger never wired (most likely, confirmed by static analysis).** `CertificateGenerationService` only persists and returns the certificate entity — no email call, no event, no job (Part 1). None of its 3 callers (`CompleteToolboxTalkCommandHandler`, `CourseProgressService`, the `RegenerateCertificate` admin endpoint) call `IToolboxTalkEmailService.SendCompletionConfirmationEmailAsync` — and two of the three callers don't even have that service injected, so the call is structurally impossible without a code change (Parts 3–4). `git log -S` shows the method has been present-but-uncalled since the LMS was extracted into its own repo (`9e575bf`) and no commit since has closed the gap (Part 5). This fully explains the reported symptom with no need to invoke a runtime/delivery failure.

2. **Email trigger wired but gated by an unmet condition.** Not applicable — there is no gating condition to check because there is no call site at all. (The `NotifyOn*` settings on `ToolboxTalkSettings` exist but govern TransVal admin notifications — translation/validation complete, failure, external review — not employee-facing completion/certificate emails; confirmed by grepping their only consumer, `ToolboxTalkNotificationService.cs`.)

3. **Email trigger fires but the service call is a no-op.** Not applicable for the same reason — `SendCompletionConfirmationEmailAsync` is fully implemented (builds HTML, calls `_emailProvider.SendAsync`, logs success/failure) and would work correctly if called. It is simply never called.

4. **Send is attempted but silently fails at delivery time (MailerSend-side).** Not supported by any evidence found — there is no code path that even attempts the send for certificate emails, so there is nothing that could be silently failing. This candidate would only become relevant if a future fix adds the missing call and *then* the email still doesn't arrive — at that point it would require checking MailerSend's own dashboard/logs, which is outside the scope of static code analysis and not checked here.

5. **Something else.** No other explanation found. The scaffolding (template content, provider, DI registration, `CertificateUrl` field being populated correctly on `ScheduledTalkCompletion`) is all present and correct — the single missing piece is the call site(s).

---

## Recommended fix scope (description only — no code/prompts here)

A follow-up chunk would need to:
- Inject `IToolboxTalkEmailService` into `CompleteToolboxTalkCommandHandler` and call
  `SendCompletionConfirmationEmailAsync(completion, employee, cancellationToken)` after the completion
  record (and, where applicable, `CertificateUrl`) is finalized — for both the certificate-success and
  certificate-generation-failed branches (the employee should still get a completion email even if the
  certificate itself failed; the certificate link section already handles an empty `CertificateUrl`
  gracefully by omitting it).
- Inject the same service into `CourseProgressService` and call it when a course assignment completes
  (course completions currently have no completion-confirmation email touchpoint of their own — worth
  confirming with the product owner whether course completions should get a distinct email or reuse the
  same template, since `SendCompletionConfirmationEmailAsync` is currently talk-shaped, not course-shaped).
  Course completions don't have a single `ScheduledTalkCompletion` to hand to the existing method
  signature — decide whether to add a new course-specific method (mirroring `SendCourseAssignmentEmailAsync`'s pattern) rather than force-fitting the talk-shaped method to
- Decide whether the `RegenerateCertificate` admin endpoint should also trigger (or re-trigger) the
  completion email once a previously-failed certificate is regenerated.
- Whichever of the above is chosen, follow the existing inline-call pattern used by the assignment flow
  (no Hangfire job needed — `ToolboxTalkEmailService`'s own `SendAsync` call is already synchronous and
  fire-effectively-non-blocking against the mediator pipeline, consistent with every other method in
  this class) unless a product decision is made to move all such emails to the "fire-and-notify"
  background pattern already tracked in CLAUDE.md's Backlog ("Long-running job UX").
