# Client Evaluation Triage — Top 10 from BACKLOG.md

**Date:** 2026-07-30
**Purpose:** Identify the highest-priority BACKLOG items to tighten up before/during the client evaluation stage (real prospects exploring the app, demos, self-serve trials). This is a read-only triage — no code or BACKLOG changes made.

**Lens used:** not "highest engineering priority" but "what would a prospect notice, get frustrated by, or use to conclude 'this isn't ready' in the first hours-to-days." See the source prompt for the full scoring rubric (visibility / severity / effort / confidence).

---

## 1. Total BACKLOG items reviewed

`BACKLOG.md` (root of repo, ~2,800 lines) is the source of truth — not `docs/BACKLOG.md` (no such file exists) and not the untracked, locally-generated `BACKLOG_CLEAN.md` (a June 22 "open items only" snapshot that is not in git history and predates several closures — not used as a source here to avoid working from stale data).

- **~123 individually-numbered entries** (`#### N.N`, `## §N`, `### N` headings), organized under 6 numbered top-level sections (Content Creation & Translation, QR Workstations & PINs, Employee & User Management, Tenant Management & Regulatory, Infrastructure & Tooling, Security Notes) plus a large "Recently Closed" trail and a run of newer floating entries (§9 through §42) added after the Recently Closed divider.
- Of those, **48 are marked `✅ Done`**, roughly **76 are `Open`**, and the remainder are `Deferred`, `Blocked`, or `PD` (product decision required).
- The document is unusually well-maintained for its size — most entries carry recon references, fix reports, and dated resolutions. The newest entries (§39–§42, added most recently, `[Client]`-origin) are a visible exception: thin, one-paragraph, no effort estimate, no recon link. Noted below.

## 2. Items filtered out (first pass)

Filtered before scoring, with reasoning:

- **All 48 `✅ Done` entries** — already shipped, verified, no eval-readiness action needed (e.g. the entire Ryan's Bakery UAT P0/P1 batch §1.1.1–1.1.9, the strict-review-workflow port §20/§23, the new-wizard visual-parity pass §25, navigation/cancellation parity §26, the §24 edit-workflow chunks, the §5.28/§5.32 Anthropic-model incidents).
- **In-progress work this session** (confirmed via git log / working tree, per instructions): SCORM export (uncommitted, mid-build — `Services/Scorm/`, `ScormExportTests.cs`), regulatory multi-standard work (shipped across 7 commits `eee9562`–`be996fe`, 2026-07-23/24), and learning-wizard tenant-level defaults (shipped, commits `9ba0355`–`a0b20f1`, the top of the current log). None of these are treated as open gaps.
- **Internal-only / no user-facing surface:** §5.2/§5.3 migration tooling, §5.4 DB drift sweep, §5.5 R2 orphan cleanup, §5.15 integration test suite review, §5.33's non-crash lint categories (`no-explicit-any`, `preserve-manual-memoization`, etc.), §12 seeder/JWT test divergence, §32 AI-usage-logger middleware refactor, §37/§38 test/index hygiene.
- **Roadmap features not currently promised, no evidence of active sales commitment:** §2.2.1 Asset Management (explicitly scoped as a "multi-week feature, its own dedicated sprint" and clearly labeled forward-looking), §3.9 2FA, §4.2/§4.3 sector presets/dialect UI, §5.9 AI Chat Assistant.
- **Product-decision-gated items with no clear eval-visible failure mode:** §1.2.1 (translation-validation cost policy), §3.12 (`IsActive` semantics), §7.6/§7.7 (latent edge-case bugs requiring rare data states).
- **Pure engineering process/hygiene:** §5.26 (SPRINT.md, already closed), §30 (legacy code cleanup sprint, explicitly gated on Production usage thresholds not yet met).

## 3. Scoring methodology

Each surviving candidate was scored on four axes — **Visibility** (how likely an evaluator hits it), **Severity** (how bad the impression if hit), **Effort** (rough size), **Confidence** (how sure the scoring is, with reasons for any "Low"). Preference given to High visibility + Critical/Serious severity + Small/Medium effort + High confidence, per the brief. Large-effort items were kept only where visibility/severity was high enough to justify it (flagged explicitly where that trade-off was made). BACKLOG's own P0–P3 priority tags were **not** used as a proxy — several of the items below carry P2/P3 engineering priority but score high on the eval lens, and one or two P1 items were excluded because they're low-visibility.

---

## 4. Top 10 — ranked for client-evaluation priority

### 1. §41 — AI model discontinuation risk (imminent, live deadline)
- **What it fixes:** Anthropic has issued a 7-day discontinuation warning for `claude-opus-4-1-20250805`, effective **Aug 5, 2026** — 6 days from today. This is the same failure class as the §5.28 P0 incident in June (a retired model silently broke help chat, translation, regulatory scoring, and TransVal Round 3).
- **Why it matters for eval:** If this model is still referenced anywhere at retirement, AI-dependent features (content generation, quiz generation, translation, regulatory scoring, help chat) can fail mid-evaluation with no warning — exactly the kind of incident that ends a self-serve trial. §5.28 already proved this exact failure mode happens in this codebase.
- **Effort:** Small — the fix pattern is proven (`AIProviderOptions` config swap + verify no other hardcoded references), per the June precedent.
- **Dependencies/blockers:** None — actionable immediately. Should be resolved well before Aug 5.
- **Confidence:** High. The deadline is explicit and unambiguous; the failure mode is proven by the June incident.

### 2. §5.33 (partial) — Two genuine crash/UI-instability bugs in lint debt
- **What it fixes:** Buried inside a 192-item lint-debt entry are two flagged **genuine bugs**, both confirmed still present (no commit has touched either file since the June 23 audit): (a) `usePermission` called conditionally in `admin/toolbox-talks/schedules/[id]/page.tsx:126` — a React rules-of-hooks violation that can crash the page; (b) `Date.now()` called directly in a `useState` initializer in `SectionContent.tsx:34` — produces unstable UI on every render.
- **Why it matters for eval:** A crash on a core admin page (schedule detail/edit) or visibly flickering/unstable content during a demo is exactly the "visible bug in a live demo" failure mode. The rest of the 192-item lint debt is correctly non-urgent (build is green, no user-facing effect) — only these two are worth pulling forward.
- **Effort:** Trivial — both are described in BACKLOG itself as "five-minute fixes."
- **Dependencies/blockers:** None.
- **Confidence:** High — verified via git history that neither file has been touched since the bugs were documented.

### 3. §19 — Learning list shows "Inactive" instead of "Draft" for draft talks
- **What it fixes:** The main admin Learnings/Talks list renders every in-progress draft talk with an "Inactive" badge, because Active/Inactive and lifecycle-status (Draft/Published) are conflated in one column.
- **Why it matters for eval:** This is the very first list an evaluating admin opens after creating their first piece of content. Seeing a just-created draft labeled "Inactive" reads as broken content management, not as a lifecycle nuance — a bad first impression on the flagship list view.
- **Effort:** Small — conditional render (`Status == Draft` → "Draft" label), fix direction already specified in the entry.
- **Dependencies/blockers:** None. Worth checking the linked question about whether the Actions menu offers nonsensical Activate/Deactivate on draft rows at the same time.
- **Confidence:** High.

### 4. §34 — PDF/video upload has no timeout (indefinite hang)
- **What it fixes:** The wizard's Step-1 file upload (`useUploadSourceFile.ts`, axios PUT to R2) has no timeout. A stalled R2 connection produces an indefinite spinner with zero feedback.
- **Why it matters for eval:** This sits at the very start of the flagship demo flow — content creation. "Loading state that spins forever" is called out explicitly as a first-30-minutes red flag; a Playwright test already burned a 15-minute budget hitting this exact hang.
- **Effort:** Small/Medium — configure a 30–60s timeout, surface failure via toast, allow retry.
- **Dependencies/blockers:** None.
- **Confidence:** High — root cause and repro already documented.

### 5. §5.7 — Demo/evaluation environment readiness (status unclear — verify first)
- **What it fixes:** The dedicated three-tier Demo Railway environment (for "business sign-off and prospect demos," per its own description) was scoped as a 20-task, 6-phase bring-up in early June. Several of its blockers (§25 visual parity, §26 navigation parity) were closed by mid/late June, but the entry itself has no `✅ Done` marker, and a later recon (`docs/regulatory-demo-readiness-recon.md`, dated 2026-06-25) shows the team demoing a real prospect (Platinum Homecare) directly against the **Development** environment rather than an isolated Demo tier — suggesting the dedicated Demo environment may still not be live.
- **Why it matters for eval:** This is arguably the single most eval-relevant infrastructure item in the whole document — if prospects are being shown Development directly, active feature branches, in-flight bugs, and data churn from ongoing engineering work (like the SCORM work happening right now) are one accidental deploy away from being visible to a live evaluation.
- **Effort:** Large (explicitly scoped as substantial, its own sprint) — flagged here despite the effort/visibility trade-off rule because the item's severity-if-neglected (an evaluator sees broken WIP) and its role as a precondition for everything else on this list make it worth surfacing regardless.
- **Dependencies/blockers:** Needs a pre-requisite decision on R2 bucket sharing (Option A vs B, per the entry) and confirmation §5.31 (credential gating) is in place before Demo comes up — both already resolved.
- **Confidence:** Medium — status genuinely can't be confirmed from docs alone. **Recommend verifying current Railway Demo state directly before relying on this triage's ranking.**

### 6. §3.17 — Supervisor can assign courses outside their team (permission scoping gap)
- **What it fixes:** `AssignCourseCommandHandler` has no awareness of caller role — a Supervisor (who should only manage their assigned operators) can assign courses to any employee in the tenant, or the whole tenant. Same shape as the §3.14 schedule-scoping bug, which was already fixed for schedules but not courses.
- **Why it matters for eval:** Auth/permission issues are an explicit eval red-flag signal. If a prospect evaluates the Supervisor role specifically (a named role in the product's RBAC story) and notices scoping doesn't hold for course assignment, it undermines confidence in the access-control model generally — especially since the equivalent schedule-assignment bug was already found and fixed, so the same gap surviving in a sibling endpoint reads poorly on inspection.
- **Effort:** Small/Medium — half a day to one day; the fix pattern is a direct mirror of the already-shipped §3.14 fix.
- **Dependencies/blockers:** The entry notes it should land "after Option B" (the multi-provider config sweep) so the `ICurrentUserService.Roles` pattern is settled — **Option B closed 2026-06-22**, so this is now unblocked.
- **Confidence:** High — recon-documented, fix direction fully specified, reference implementation exists in the codebase.

### 7. §5.6 — MailerSend 429 (rate limit) silently drops invitation emails
- **What it fixes:** When MailerSend's API rate limit (10 req/sec) is hit, the provider call fails and is silently logged/dropped — no retry, no admin-facing error.
- **Why it matters for eval:** Bulk employee import is explicitly named in BACKLOG as the first feature with volume to realistically hit this limit — exactly the kind of feature a prospect would stress-test in a self-serve trial ("let me import our whole team"). The failure is invisible: rows show success, invited employees just never receive a working invitation, and nothing in the UI signals a problem.
- **Effort:** Medium — retry-with-backoff, clearer logging (compounds with the related §3.10 logging-fires-on-failure bug).
- **Dependencies/blockers:** None.
- **Confidence:** Medium/High.

### 8. §5.30 — `ToolboxTalk.IsActive` toggle is functionally decorative
- **What it fixes:** Toggling a published talk's "Active" switch off has no effect on already-assigned learners — they can still see and complete it. Deactivation only works if an admin manually cancels every existing assignment.
- **Why it matters for eval:** This is a feature that visibly exists in the UI (the toggle) and visibly does not do what its label implies. An evaluator who specifically tests "can I deactivate this training" and then checks the employee portal will find it still assignable/completable — a trust-eroding discovery precisely because it looks like a deliberate control, not an edge case.
- **Effort:** Medium/Large — the entry itself says a product decision is needed before sizing (cascade-on-deactivate vs. relabel the toggle).
- **Dependencies/blockers:** Needs the product decision first; that alone (even without a code fix) would let a prospect-facing answer be "the toggle prevents new assignments; existing ones must be cancelled explicitly" rather than silence.
- **Confidence:** Medium — scored on trust-impact-if-discovered rather than probability of discovery, which is lower than items 1–6.

### 9. §2.1.1 — QR codes have no edit action
- **What it fixes:** QR codes (used for workstation/location-based training, one of the product's named modules) can only be toggled active or deleted — never edited. Any change forces delete + recreate, which generates a new `CodeToken` and invalidates any already-printed/deployed code.
- **Why it matters for eval:** QR workstation training is a distinct, demoable module. An evaluator testing the full lifecycle (create code → assign content → realize they picked the wrong course → try to fix it) hits a dead end that looks like a missing basic CRUD action, not a nuanced design choice.
- **Effort:** Small/Medium — add an edit dialog; `CodeToken` stays immutable.
- **Dependencies/blockers:** None.
- **Confidence:** High.

### 10. §40 — Regulatory glossary terms limited to 9 of the system's ~33 languages
- **What it fixes:** The Regulatory Terms input/edit screens hardcode 9 languages for term translations, while the rest of the system (33 languages via the DB-backed lookup) supports far more. This is the same "three divergent language lists" pattern already diagnosed in §2.1.2.
- **Why it matters for eval:** This is a **live client-flagged gap** (`[Client]` origin, not internal speculation) in the Regulatory/Compliance module — one of the product's key sector-differentiators (HIQA/HSA/etc.). A prospect in a regulated sector who wants a glossary term in a language outside the hardcoded 9 hits a hard limitation in a module specifically pitched on compliance rigor.
- **Effort:** Small/Medium — extend the hardcoded 9-language constant to read from the same DB-backed language lookup already used elsewhere (§2.1.2 already scoped this consolidation).
- **Dependencies/blockers:** None structurally; benefits from being done alongside §2.1.2.
- **Confidence:** Medium — the BACKLOG entry itself is thin (no recon, no effort estimate, added in the same recent unstructured batch as §39/§41/§42) — confidence in the *problem* is high (client-flagged), confidence in *scope/effort* is lower.

---

## 5. Close-but-didn't-make-the-cut (11–15)

- **§21 — Post-publish translation management UI, detail-page path.** Likely already resolved: the entry's own last update says the gap "remains blocked on §24 (Chunk 2)," but §24's own entry states "**All 6 chunks shipped 2026-06-18**" including Chunk 2 (translation re-run UI on the talk detail page). This looks like a stale cross-reference rather than a live gap — recommend verifying and closing rather than treating as an open eval risk. See §6 below.
- **§1.1.17 — Per-tenant Translator defaults.** Real friction (re-selecting target languages/options on every import) but a feature request, not a bug; P2, small/medium effort. Didn't beat the cut because it's mild friction rather than a "this looks broken" moment.
- **§3.15 item A — Team Reports / Skills Matrix missing role-redirect for non-Supervisors.** Real gap (an Operator deep-linking to a Supervisor-only URL sees a confusing "0 operators" framing, not a leak) but requires an evaluator to manually construct a URL rather than encountering it through normal navigation — low natural-discovery probability.
- **§39 — WhatsApp Integration (client-flagged, P1 in BACKLOG).** A brand-new delivery channel, not a fix — large effort, and nothing in the codebase or session history suggests partial groundwork exists. Belongs in a roadmap/sales conversation, not a pre-eval tightening pass, unless a specific prospect has been told it's coming.
- **§5.5 — R2 orphan file cleanup nightly job.** Real but entirely invisible to any evaluator; pure ops hygiene.

## 6. Items that concerned us but couldn't be scored confidently

- **§39, §40, §41, §42** — all four of the newest BACKLOG entries share a format gap: single paragraph, no `Surfaced:` date, no recon link, no effort estimate, unlike the rest of the document. §41 and §40 were scored above with reasonable confidence because external facts (a live Anthropic deadline; a known client complaint) filled the gap — §39 and §42 could not be scored with the same confidence:
  - **§42 — "Ensure Regulatory can handle multiple files."** This describes wanting multiple Regulatory documents at the application level and multiple Standards documents at the tenant level for homecare (HIQA + a new Legal requirement). The regulatory multi-standard work that shipped 2026-07-23/24 (`TenantStandardSubscription` entity, Kind discriminator, catalog + "My Standards" UI) **appears to already implement exactly this capability**. This entry may simply be stale and already closed by that work — recommend the team verify against the shipped feature before treating it as open. Flagged here rather than scored as a live gap because confirming would require functional testing this triage didn't perform.
  - **§39 — WhatsApp Integration.** No detail on scope (delivery of new-talk notifications? full two-way quiz completion? certificate delivery?) — too ambiguous to size or judge eval-relevance without a follow-up conversation with whoever took the client request.
- **§5.7 (Demo environment)** — included in the top 10 above but flagged again here: its true current state could not be confirmed from documentation alone (see item 5's notes). This is the one ranking in this triage most worth double-checking against live Railway state before acting on the rest of the list.
- **§5.30 (`IsActive` decorative)** — the BACKLOG entry itself says a product decision is needed before the fix can be sized, which limits how confidently "effort" can be estimated; the trust-impact scoring (severity) is more solid than the effort estimate.

## 7. Summary observations

- **The acute, UAT-sourced trust problems are already resolved.** The entire Ryan's Bakery UAT batch (contradictory validation scores, broken slideshow navigation, un-editable content, quiz-deletion data loss) — the kind of findings that would have been genuinely embarrassing in a live demo — is closed. The wizard's core create/translate/validate/publish path is in good shape for evaluation.
- **A recurring pattern: quiet permission-scoping gaps that trail behind their own fixes.** §3.14 (schedule scoping) was found and fixed; §3.17 (the identical gap in course assignment) was found in the same recon but left open, apparently for sequencing reasons that have since resolved. This looks less like isolated bugs and more like a feature (Supervisor role) that was extended surface-by-surface without a single consistency pass — which BACKLOG's own §3.15 already names as a cleanup candidate. Worth doing the full §3.15 sweep once §3.17 lands, rather than re-discovering siblings of the same bug piecemeal.
- **AI model lifecycle risk is a recurring incident category, not a one-off.** §5.28 (June P0), §5.32 (June P0, same root cause resurfacing), and now §41 (a live, dated warning) are the same shape of problem three times in two months. §33 (proactive retirement monitoring) is exactly the structural fix that would stop this from being a recurring fire — worth prioritizing above its current P3 given the pattern, independent of this triage's eval-specific lens.
- **The newest backlog entries have fallen out of the document's own quality bar.** Everything from §1 through roughly §38 carries dated recon references, fix reports, and effort estimates; §39–§42 do not. This isn't a reason to distrust them, but it does mean they need a scoping pass before they're actionable, and at least one of them (§42) may already be moot.
- **The single highest-leverage open question isn't a code fix at all:** confirming whether the dedicated Demo environment (§5.7) is actually live, isolated from active development, and current. Everything else on this list matters less if prospects are, in practice, being shown a development environment that changes under them.
