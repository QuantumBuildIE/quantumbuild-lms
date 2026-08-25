# Certificate Email Recon (v2)

Date: 2026-08-25
Scope: Read-only static + config analysis. No source files modified.
Supersedes: this file previously documented a 2026-07-16 recon (v1) whose finding —
"the email is never called" — was fixed by commit `ec62805` the same day. v1's
content is preserved in git history (`git show ec62805~1:docs/certificate-email-recon.md`
if needed); this version investigates the *current* symptom against the *current* code.

## tl;dr

The email trigger IS wired (commit `ec62805`, 2026-07-16, present on both `main` and
`transval`) and is covered by 7 passing integration tests. The code path that composes
and sends the certificate-completion email is correct. **The most likely cause of "cert
issued, no email" is a configuration-key mismatch that makes `IEmailProvider` resolve to
`StubEmailProvider` — a provider that always reports success while sending nothing —
and this is not certificate-specific: it would silently no-op every email in the
application (assignment, reminder, completion, escalation, refresher, course).** A second,
narrower failure mode also exists and is specific to how this particular flow reports
failure: a MailerSend-returned error (bad API key, 429, rejected recipient) is logged as
a **Warning**, not an Error, so it does **not** reach Sentry (`MinimumEventLevel = Error`)
and does **not** set `CertificateEmailFailed` (that flag is only set when the send call
*throws*, not when it returns a structured failure). Both explanations produce the exact
reported symptom — certificate visibly generated, no email, no visible error anywhere —
and neither requires a delivery/spam problem on MailerSend's side. See "Diagnosis" for
which is more likely and how to tell them apart with a live check.

---

## A. The certificate email path

### A1. Trigger — synchronous, inline in the HTTP request (not a Hangfire job)

Three call sites, all synchronous, all fire in the same request/method that persists the
certificate — there is no background job, no MediatR notification, no SignalR event:

1. **Standalone talk completion** —
   [`CompleteToolboxTalkCommandHandler.cs:213-262`](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CompleteToolboxTalk/CompleteToolboxTalkCommandHandler.cs#L213)
   runs inside `POST /api/my/toolbox-talks/{id}/complete`. After
   `_certificateService.GenerateTalkCertificateAsync(...)` returns a non-null certificate
   (line 215-218), it sets `completion.CertificateUrl`, saves, then at line 230 calls
   `_emailService.SendCompletionConfirmationEmailAsync(completion, employee, cancellationToken)`.

2. **Course completion** —
   [`CourseProgressService.cs:90-126`](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/CourseProgressService.cs#L90),
   invoked from the same completion request via
   `CompleteToolboxTalkCommandHandler.cs:205` → `_courseProgressService.UpdateProgressAsync(...)`
   whenever the completed talk belongs to a course. After
   `certificateService.GenerateCourseCertificateAsync(...)` returns non-null, line 106 calls
   `emailService.SendCourseCompletionConfirmationEmailAsync(assignment, employee, certificate.PdfStoragePath, cancellationToken)`.

3. **Admin manual regenerate** —
   [`ToolboxTalksController.cs:2567-2608`](../src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2567)
   (`POST /api/toolbox-talks/{talkId}/completions/{completionId}/regenerate-certificate`,
   policy `Learnings.Admin`). Re-runs generation, then re-sends the same completion email.

All three are **fire-effectively-synchronous**: the HTTP response does not return until
the email send attempt (success or failure) completes, but a failure never blocks the
response — see A2.

### A2. The send call and its failure handling

Composition happens in
[`ToolboxTalkEmailService.SendCompletionConfirmationEmailAsync`](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs#L208-L302)
(talk) and `SendCourseCompletionConfirmationEmailAsync` (course, lines 304-386). Both:

- Return early with a `LogWarning` (no send attempt) if `employee.Email` is empty
  (lines 213-219 / 310-316).
- Build an inline HTML string (no external template file, nothing that can 404/fail to load).
- Call `_emailProvider.SendAsync(emailMessage, cancellationToken)` — this is the actual
  send call, going through `IEmailProvider` (see B for which implementation this resolves to).
- On `result.Success == true`: `LogInformation` ("... sent to {Email} ...").
- On `result.Success == false`: **`LogWarning`** ("Failed to send ... email to {Email}: {Error}")
  — not an exception, not rethrown. The method returns normally either way.

The caller wraps the whole call in its own try/catch purely to catch *exceptions* (network
errors, timeouts, the provider's own unhandled throw):

```csharp
// CompleteToolboxTalkCommandHandler.cs:227-241
try
{
    completion.ScheduledTalk = scheduledTalk;
    await _emailService.SendCompletionConfirmationEmailAsync(completion, employee, cancellationToken);
}
catch (Exception emailEx)
{
    _logger.LogError(emailEx, "Failed to send completion confirmation email for ...");
    certificate.CertificateEmailFailed = true;
    await _dbContext.SaveChangesAsync(cancellationToken);
}
```

**Critical consequence: `CertificateEmailFailed` is only set when the send call throws.**
`SendCompletionConfirmationEmailAsync` does **not** throw when `IEmailProvider.SendAsync`
returns a structured failure (`EmailSendResult.Failed(...)` / `NotConfigured()`) — it logs
a Warning and returns normally. From the caller's point of view, this looks identical to a
successful send: no exception, no flag set, `certificate.CertificateEmailFailed` stays
`false`. Same pattern in `CourseProgressService.cs:104-118` and the regenerate endpoint.

This is the "swallowing-catch" pattern the recent Hangfire/Sentry work targeted — but here
it's one layer removed: the *outer* try/catch is fine (it does set a flag, on the one
failure mode it can see), but the *inner* method's own error handling only escalates to
a log line, so an entire class of real failures (provider rejected the request) never
reaches the outer catch at all.

### A3. Not a Hangfire job — the recent Hangfire swallowing-catch audit doesn't apply here

This flow runs inline in the web request pipeline, not via `BackgroundJob.Enqueue`. Note 21
(concrete-vs-interface enqueue) and the general Hangfire-swallowing-catch pattern referenced
in the brief are not applicable — there is no job here to enqueue incorrectly. The relevant
swallowing is the LogWarning-vs-LogError distinction in A2, not a Hangfire attribute issue.

---

## B. Not-sent vs sent-but-not-delivered

### B1. What the system records

Two persisted signals exist, both booleans, both default `false`:

- `ScheduledTalkCompletion.CertificateGenerationFailed` — set when **certificate PDF
  generation/upload** fails (talk flow) or `ToolboxTalkCourseAssignment.CertificateGenerationFailed`
  (course flow). Not email-related.
- `ToolboxTalkCertificate.CertificateEmailFailed` — set **only when the email send call
  throws** (A2). Added in the same commit that wired the email (`ec62805`,
  migration `20260716085124_AddCertificateEmailFailedAndCourseGenerationFailed`).

**Neither flag is surfaced anywhere in the frontend** — confirmed via
`grep -r "CertificateEmailFailed" web/`: zero matches. It exists only in the database and
in `ScheduledTalkCompletionDto`/API responses; no admin report, certificate list, or
completions page renders it. An admin cannot currently see "this cert's email failed"
without querying the database directly.

### B2. Reading the evidence for a specific case

For the client's reproduction, the fastest way to determine which bucket this falls into:

1. **Query `ToolboxTalkCertificate.CertificateEmailFailed` for that certificate row.**
   - If `true` → the send call threw an exception. Check the app logs / Sentry (D2) around
     that timestamp for `"Failed to send completion confirmation email"` — this is a
     genuine attempted-and-errored send (network failure, DNS, provider outage, HttpClient
     timeout). Points toward B section "not-sent, code/logic" but with a clear, already-
     captured signal.
   - If `false` (the default) → **inconclusive by itself** — this is consistent with either
     a real successful send (delivery-side problem, see B3) *or* a silent structured
     failure that never threw (A2) *or* a `StubEmailProvider` false-success (C, D).
     Distinguishing these three requires log inspection, not just the flag.

2. **Grep application logs for the info/warning lines the code actually emits**, scoped to
   the employee/time window:
   - `"Toolbox Talk completion email sent to {email}"` (LogInformation, `ToolboxTalkEmailService.cs:292`)
     → the code believes it succeeded. If this line exists but the inbox is empty, either
     (a) it's a genuine MailerSend delivery problem (B3), or (b) it's a StubEmailProvider
     fake-success — these two log identically (see D1), so the log line alone cannot
     distinguish them.
   - `"Failed to send Toolbox Talk completion email to {email}: {Error}"` (LogWarning,
     line 298-300) → send was attempted, provider rejected it structurally. This is the
     "not sent" case that produces **no** `CertificateEmailFailed` flag and **no** Sentry
     event (D2) — check the `{Error}` text in this log line directly; it contains the raw
     MailerSend response body (e.g. 401 invalid token, 429 rate limited, 422 invalid
     recipient).
   - Nothing at all for that employee/time window → the method was never called, or
     `employee.Email` was empty (early-return warning, A2) — check
     `"Cannot send completion email: Employee {id} has no email address"`.

### B3. If the evidence says "the system believed it sent it"

This is a genuine delivery-side question at that point, external to this codebase:
provider is MailerSend (`FromEmail: noreply@quantumbuild.ai` per config, see D1), and
diagnosing would mean checking the MailerSend dashboard/activity log for that message
(bounce, spam-block, suppression list, unverified sending domain), not this repo. Nothing
in code raises this as more likely than the config-mismatch/warning-swallow explanations
below — see Diagnosis for the ranking.

---

## C. How the certificate email differs from other emails — and the attachment question

### C1. All ToolboxTalk-module emails share one code path and one provider

`ToolboxTalkEmailService` has seven methods (assignment, reminder, completion, escalation,
refresher reminder, course assignment, course refresher reminder) — all inline HTML, all
routed through the same injected `IEmailProvider.SendAsync`, all logging Info-on-success /
Warning-on-failure in the identical pattern. There is nothing structurally special about
`SendCompletionConfirmationEmailAsync` versus, say, `SendTalkAssignmentEmailAsync` — same
provider, same DI registration, same try/catch shape at the call site. **If assignment
emails are confirmed reaching inboxes in the same environment where certificate emails are
not, that would be strong evidence against the shared-provider/config-mismatch theory** (C
below) and would point at something certificate-flow-specific — but nothing in the code
itself distinguishes the two flows enough to explain that outcome; both go through the
identical `_emailProvider.SendAsync` call. Two independent recon docs already exist for the
comparison flows and reach the same root-cause candidates:
[`docs/assignment-notification-recon.md`](assignment-notification-recon.md) and
[`docs/refresher-notification-recon.md`](refresher-notification-recon.md) — both flag the
same `EmailProvider` config-section gap documented in D1 below. This is not a coincidence;
it's the same shared dependency surfacing in three independent recons.

### C2. The certificate is a LINK, not an attachment — the attachment-failure hypothesis does not apply

Confirmed by reading [`EmailMessage.cs`](../src/Core/QuantumBuild.Core.Application/Abstractions/Email/EmailMessage.cs)
in full: the class has `ToEmail`, `ToName`, `Subject`, `HtmlBody`, `PlainTextBody`,
`ReplyToEmail`, `ReplyToName` — **no attachment field of any kind.** `IEmailProvider.SendAsync`
takes only an `EmailMessage`; `MailerSendEmailProvider.SendAsync` builds its JSON payload
from exactly those fields (no `attachments` array in `MailerSendRequest`). The certificate
section of the email body is:

```csharp
// ToolboxTalkEmailService.cs:235-240
var certificateSection = "";
if (!string.IsNullOrEmpty(completion.CertificateUrl))
{
    certificateSection = $@"<p><a href='{completion.CertificateUrl}' style='color: #007bff;'>Download your completion certificate</a></p>";
}
```

This is a plain `<a href>` link to the R2-hosted PDF's public URL, conditionally included
if `CertificateUrl` is non-empty (and it always is by the time this runs — the caller only
reaches the email-send call inside the `if (certificate != null)` branch, after
`completion.CertificateUrl = certificate.PdfStoragePath` has already been set and saved).
**There is no PDF byte data anywhere in the email send path, no attachment generation, no
attachment-size limit, nothing that could throw while "attaching" a certificate — because
nothing attaches one.** The "cert issued but no email" symptom is fully explained without
invoking an attachment failure; that hypothesis can be ruled out.

### C3. What genuinely differs

The only structural difference between the certificate/completion email and, say, the
assignment email is *when* it fires relative to other work in the same request: completion
also does quiz validation, video-watch validation, course-progress updates, refresher
scheduling, and certificate PDF generation/R2 upload all in the same call stack before the
email step is even reached. None of those upstream steps' own try/catch blocks swallow
into the email step (each has its own catch that sets its own flag and returns/continues,
per A1) — but it does mean the completion request is doing substantially more work than a
schedule-processing job's assignment-email step, which is a comparatively higher surface
area for *something* upstream to have gone wrong even before the email call is reached.
No evidence found that this is actually happening (both `CertificateGenerationFailed` and
`CertificateEmailFailed` being `false`, per B2, rules out both of the code-visible upstream
failure points) — noted for completeness, not as a live finding.

---

## D. Configuration / environment dependencies

### D1. `IEmailProvider` DI selection — a config-key mismatch that silently no-ops every email in the app

[`Program.cs:146-176`](../src/QuantumBuild.API/Program.cs#L146):

```csharp
builder.Services.Configure<EmailProviderSettings>(
    builder.Configuration.GetSection(EmailProviderSettings.SectionName));   // "EmailProvider"
...
var emailProvider = builder.Configuration.GetValue<string>("EmailProvider:Provider");
if (string.Equals(emailProvider, "MailerSend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailProvider, MailerSendEmailProvider>((sp, client) => { ... });
}
else
{
    builder.Services.AddSingleton<IEmailProvider, StubEmailProvider>();
}
```

`EmailProviderSettings.SectionName = "EmailProvider"` ([EmailProviderSettings.cs:5](../src/Core/QuantumBuild.Core.Infrastructure/Services/Email/EmailProviderSettings.cs#L5)).

**Confirmed by grep across every checked-in `appsettings*.json` in the repo (`appsettings.json`,
`appsettings.Development.json`, `appsettings.Testing.json`): there is no `"EmailProvider"`
JSON section anywhere.** What *does* exist is a section literally named `"Email"`
([appsettings.json:61-79](../src/QuantumBuild.API/appsettings.json#L61)):

```json
"Email": {
  "Provider": "MailerSend",
  "FromEmail": "noreply@quantumbuild.ai",
  "FromName": "QUANTUMBUILD Safety",
  "SpaSubmissionBaseUrl": "https://rascorweb-production.up.railway.app/site-attendance/spa/submit",
  "MailerSend": { "ApiKey": "mlsn.786be9c0..." },
  "SendGrid": { "ApiKey": "" },
  "Smtp": { "Host": "", "Port": 587, ... }
}
```

This section is **never read by any C# code** — grepped for `"Email:Provider"`,
`GetSection("Email")`, and any binding to this shape: no consumer found. Its presence
alongside a `SpaSubmissionBaseUrl` key (a RASCOR site-attendance concept, not an LMS one)
and the same `rascorweb-production.up.railway.app` host that also appears as the *default*
fallback `AppSettings:BaseUrl` in `ToolboxTalkEmailService.cs:53` strongly suggests this
whole `appsettings.json` file is inherited/shared from a sibling RASCOR codebase and this
`Email` block is dead, vestigial config — present, plausible-looking, and completely
disconnected from the code path that actually selects the email provider.

**Effect:** `builder.Configuration.GetValue<string>("EmailProvider:Provider")` returns
`null` from every checked-in config file (no such key exists). `string.Equals(null,
"MailerSend", ...)` is `false`, so the `else` branch runs and **`StubEmailProvider` is
registered as the singleton `IEmailProvider`** for the whole application — not just
certificate emails. `StubEmailProvider.SendAsync` ([StubEmailProvider.cs:17-24](../src/Core/QuantumBuild.Core.Infrastructure/Services/Email/StubEmailProvider.cs#L17))
unconditionally returns `EmailSendResult.Succeeded("stub-...")` after only logging an
`Information`-level line — from every caller's point of view this is indistinguishable
from a real MailerSend success: same `result.Success == true`, same `LogInformation` line
at the call site, no flag ever set anywhere.

**This is not a certificate-specific bug.** If active, it silently no-ops:
assignment emails, reminders, escalations, refresher reminders, course-assignment emails,
course refresher reminders, *and* completion/certificate emails — every single
`IToolboxTalkEmailService` method, plus every `IEmailService` method in Core (password
setup, user-created, PIN, external review invitation — `EmailService.cs` injects the same
`IEmailProvider`). Two other independent recon docs already reached this exact same config
gap for the assignment and refresher flows
([`docs/assignment-notification-recon.md:12,166,178`](assignment-notification-recon.md),
which additionally confirms via grep of `appsettings.Testing.json` too and explicitly
states "this recon cannot determine ... whether Development/Demo/Production Railway
services currently have `EmailProvider__Provider=MailerSend` set").

**Whether this is live in the environment where the client's certificate-email failure was
reproduced cannot be determined from the repo** — Railway environment variables
(`EmailProvider__Provider`, `EmailProvider__ApiKey`, using ASP.NET Core's `__` double-
underscore section-nesting convention) are not visible in source control by design. The
July 16 v1 of this doc asserted "`EmailProvider__Provider=MailerSend` is confirmed set in
Development" but did not cite how that was confirmed (no Railway CLI output, no log excerpt
checked into the repo) — treat that as an unverified claim from a prior session, not a
fact this recon can stand behind. **This must be checked directly against the actual
environment** (Railway dashboard, `railway variables --service <api>`, or by grepping that
environment's own logs for the literal string `"[StubEmailProvider] Email logged (not
sent)"` — if that string appears in the logs around the reproduction time, this is the
answer; if it never appears, `MailerSend` was genuinely selected and D1 is ruled out for
that environment).

### D2. Sentry coverage of the swallowed-exception path

`Program.cs:44-68` wires `builder.WebHost.UseSentry(...)` with
`options.MinimumEventLevel = LogLevel.Error` ([Program.cs:49](../src/QuantumBuild.API/Program.cs#L49)),
inert unless `SENTRY_DSN`/`Sentry:Dsn` is set (per the recent Sentry work referenced in the
brief — commits `abb64e5`, `33a7be4`, `94936f2`, `1f3a55e`).

- The `_logger.LogError(emailEx, "Failed to send completion confirmation email for ...")`
  call in `CompleteToolboxTalkCommandHandler.cs:234` (and the equivalent in
  `CourseProgressService.cs:111`) **is at Error level and would be captured as a Sentry
  event** if the send call *threw* — Sentry's ASP.NET Core integration auto-wires
  `Sentry.Extensions.Logging`, and any `ILogger.LogError` call at or above
  `MinimumEventLevel` becomes an event, independent of whether the exception is rethrown.
  **If `CertificateEmailFailed = true` for the affected certificate and a DSN is
  configured in that environment, check Sentry first — this is the fastest path to a
  concrete stack trace/error message for that specific failure.**
- The `_logger.LogWarning(...)` call in `ToolboxTalkEmailService.cs:298-300` (provider
  returned a structured failure, A2) is below `MinimumEventLevel = Error` and **will not
  reach Sentry**, regardless of DSN configuration. This is the gap: the more common
  provider-rejection failure mode (bad API key, 429, invalid recipient — anything that
  returns rather than throws) is invisible to both the DB flag (A2) and Sentry (this
  section) simultaneously.
- `StubEmailProvider`'s fake-success path (D1) never logs at Warning or Error level at
  all — it logs `Information` — so it is invisible to Sentry by design, correctly, since
  from the code's perspective nothing went wrong.

### D3. Other config dependencies

- `EmailProviderSettings.FromEmail` defaults to `noreply@quantumbuild.ie` if unset
  ([EmailProviderSettings.cs:9](../src/Core/QuantumBuild.Core.Infrastructure/Services/Email/EmailProviderSettings.cs#L9))
  — note the `.ie` vs the dead config block's `.ai` domain (D1) — another sign the two
  configs are unrelated. Whether either domain has valid outbound SPF/DKIM/DMARC records
  for MailerSend, and whether that sending domain is verified in the MailerSend account,
  are facts external to this repo (would explain spam-folder delivery rather than total
  non-delivery, and is a B3-category question, not a code question).
- `AppSettings:BaseUrl` (used for the "Start Talk" / "Complete Now" links in *other*
  ToolboxTalkEmailService methods, not the completion email, which links straight to the
  R2 `CertificateUrl` instead) defaults to `rascorweb-production.up.railway.app` in code
  ([ToolboxTalkEmailService.cs:53](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs#L53))
  and is literally set to that same non-LMS host in `appsettings.Development.json:21` —
  irrelevant to the certificate email specifically (it doesn't use this URL) but another
  data point that this config file carries cross-project leftovers.

---

## E. Test coverage

`tests/QuantumBuild.Tests.Integration/ToolboxTalks/CertificateEmailTests.cs` (411 lines, 7
integration tests, added in `ec62805`) covers:

1. `CompleteTalk_WithGenerateCertificate_CreatesCertificateAndSendsCompletionEmail` — happy path, asserts `FakeToolboxTalkEmailService.CompletionEmails` received the call with the right `CertificateUrl`.
2. `CompleteTalk_Repeated_ReturnsBadRequest_AndDoesNotSendSecondEmail` — idempotency guard.
3. `CompleteTalk_WhenEmailSendThrows_CompletesSuccessfullyAndFlagsCertificateEmailFailed` — forces `FakeToolboxTalkEmailService.ShouldThrowOnCompletionEmail = true`, asserts completion still succeeds and `CertificateEmailFailed` is set.
4. `CompleteTalk_WhenCertificateGenerationThrows_...AndDoesNotSendEmail` — generation failure skips the email entirely.
5. `RegenerateCertificate_AfterPriorFailure_SendsCompletionEmail` — admin regenerate path.
6. `CourseCompletion_WithGenerateCertificate_CreatesCertificateAndSendsCourseCompletionEmail`.
7. `CourseCompletion_WhenCertificateGenerationThrows_FlagsCertificateGenerationFailed_AndDoesNotSendEmail`.

**What this coverage verifies:** the *wiring* — that `IToolboxTalkEmailService` is called
with correct arguments at the right moments, and that the `CertificateEmailFailed` /
`CertificateGenerationFailed` flags are set correctly when the *fake* email service throws.

**What this coverage does NOT verify** (and cannot, as written): anything below the
`IToolboxTalkEmailService` interface. `FakeToolboxTalkEmailService`
([tests/QuantumBuild.Tests.Integration/Setup/Fakes/FakeToolboxTalkEmailService.cs](../tests/QuantumBuild.Tests.Integration/Setup/Fakes/FakeToolboxTalkEmailService.cs))
is registered in `CustomWebApplicationFactory` in place of the real
`ToolboxTalkEmailService`, so:

- No test exercises `IEmailProvider` at all — the `EmailProvider:Provider` config-key
  mismatch (D1) is entirely untested and untestable by this suite, since the fake bypasses
  that layer completely.
- No test exercises `MailerSendEmailProvider.SendAsync`'s HTTP call, its success/failure
  branches, or its error-body parsing.
- No test exercises the LogWarning-not-LogError gap (A2/D2) — the only failure mode tested
  is "the fake throws," which maps to the code's *exception* path, not its *structured-
  failure-result* path. The scenario most likely to explain a silent real-world failure
  (provider returns `Failed(...)` without throwing) has zero test coverage.
- No test asserts on the actual HTML content of the certificate link, or verifies the link
  is well-formed/reachable.

A fix (or a diagnosis-confirming test) would need either a fake/mock at the
`IEmailProvider` level (one level lower than the existing fake) to exercise
`ToolboxTalkEmailService`'s own Warning-vs-Error branching, or a config-level test that
asserts `IEmailProvider` resolves to `MailerSendEmailProvider` under a given configuration
shape (which would have caught D1 directly).

---

## Diagnosis — ranked most to least likely

1. **Config-key mismatch → `StubEmailProvider` active (D1), most likely given the local
   repo's checked-in config.** No `EmailProvider` section exists in any committed
   `appsettings*.json`; the file instead carries a same-shaped but disconnected `Email`
   section that nothing reads, alongside other cross-project leftovers (`SpaSubmissionBaseUrl`,
   the `rascorweb` base URL). Unless a Railway environment variable overrides this, every
   email in the entire application — not just certificates — silently no-ops while logging
   fake success. This exactly matches the reported symptom ("certificate issued, no email
   arrived," no error visible anywhere) and is corroborated independently by two other
   recon docs investigating different email flows in this same codebase. **Action: check
   the actual environment's `EmailProvider__Provider` variable, or grep its logs for
   `"[StubEmailProvider] Email logged (not sent)"` around the reproduction timestamp.**

2. **MailerSend genuinely configured, but the send returns a structured failure that never
   throws (A2/D2).** If (1) is ruled out, this is next: `ToolboxTalkEmailService` logs
   provider rejections as `Warning`, which never sets `CertificateEmailFailed` and never
   reaches Sentry (`MinimumEventLevel = Error`). A bad/expired MailerSend API key, an
   `EmailProvider__ApiKey` that doesn't match the account, a 429, or a rejected recipient
   would all produce exactly this — a completion that "succeeds," a certificate that's
   real, and a log line at Warning level that nobody is necessarily watching. **Action:
   grep logs for `"Failed to send Toolbox Talk completion email to"` around the
   reproduction time; the `{Error}` field carries the raw MailerSend response.**

3. **Send call throws (visible, self-diagnosing).** `CertificateEmailFailed = true` on the
   specific certificate row is the direct signal for this case, and if a Sentry DSN is
   configured in that environment, the LogError at the throw site is captured as an event
   with a full stack trace. **Action: query the flag first — if true, check Sentry, this
   is the easy case.**

4. **Genuine deliverability problem (sent, not delivered) — B3.** Only relevant once (1)-(3)
   are ruled out, i.e. logs show a real `result.Success == true` from a confirmed-active
   MailerSend provider. At that point this becomes a MailerSend-dashboard question
   (bounce, spam-block, suppression, domain verification/SPF-DKIM), not a code question.
   Nothing found in this recon makes this the more likely explanation versus (1) or (2).

5. **Attachment failure — ruled out (C2).** The certificate is a hyperlink to an R2-hosted
   PDF, never an email attachment. `EmailMessage` has no attachment field at all. This
   hypothesis does not apply to this codebase's implementation.

**Fastest concrete next step:** for the specific reproduction, (a) look up the
`ToolboxTalkCertificate.CertificateEmailFailed` value for that completion, then (b) grep
that environment's application logs for the employee's email address / that time window
for any of the three log lines in B2 — this alone will place the failure into bucket 1, 2,
or 3 above without needing to touch the environment's email provider directly. If none of
the three log lines appear at all, the flow may not have reached the email step — re-check
whether `certificate != null` actually held (i.e., `GenerateCertificate` was `true` on the
talk/course and generation itself didn't fail) for that specific completion.

---

## Non-scope / not investigated

- Whether the actual Railway environment (Development/Demo/Production) has
  `EmailProvider__Provider` / `EmailProvider__ApiKey` set — operational fact, not
  determinable from this repo. Flagged as the top action item.
- MailerSend account state: domain verification, SPF/DKIM/DMARC records, sending
  reputation, suppression lists. External to this codebase.
- No code was changed. No fix proposed beyond the diagnostic next-steps above.
