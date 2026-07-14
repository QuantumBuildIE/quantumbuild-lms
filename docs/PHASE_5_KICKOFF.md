# Phase 5 Kickoff

> **Note to future Claude:** Read this file first, then read `docs/TRANSLATION_WORKFLOW_DESIGN.md` §9 (the phase summary with Status lines), then skim `docs/LEARNING_LIFECYCLE.md` (process learnings) and `BACKLOG.md` (deferred items). After that, this document is your starting point for Phase 5. The first task is to write the Phase 5 standards doc (`docs/PHASE_5_STANDARDS.md`) — do not start any code chunks until that's in place.

---

## Where we are

Phase 4 (External participant portal) shipped end-to-end on 2026-06-09. The external review flow is functional: requester sends an invitation from the per-language panel → reviewer gets an email with a portal link → reviewer edits the translation in a public portal page → submits → internal team confirms → edits propagate into `ToolboxTalkTranslation.TranslatedSections` for employee display.

Phase 4's eight implementation commits plus eight doc commits sit on the `transval` branch at HEAD `c0a8292` (the Phase 4.6/closure doc commit). Pushed to both `origin/transval` and `company/transval`. 402/402 integration tests passing.

Phase 5 ("New parallel wizard — fork-and-improve") is next. Phase 5.1 recon is complete (see "Recon findings" below). Phase 5 has not been started.

## What Phase 5 is for

Surface framing from §9: *"Fork the existing wizard. Adapt steps that work as-is. Rebuild translation+validation steps around the workflow service. Implement the new decomposed status model and remove the session/draft-talk duplication."*

Real framing established in conversation: **this is the moment to make Learning creation/editing as robust, stable, intuitive, and professional as the codebase can make it, to enterprise standards.** The old wizard works but has a ceiling — accumulated clunk, parallel state systems, cold-refresh data loss, weak validation, no resume path. Phase 5 builds past that ceiling rather than just porting the same shape onto cleaner plumbing.

"Fork" here means *copy as starting point, not lift-and-shift*. New file tree, old wizard untouched until cutover. The old wizard's code is a guiding reference for UI structure; each step gets rebuilt to the new standard rather than ported verbatim.

## Critical recon findings (Phase 5.1)

The recon mapped the existing wizard end-to-end. Full findings live in the prior conversation history; the salient points:

- **Wizard root:** `web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx` — single-page, internal `useState<WizardStep>`, switch-based step rendering. No URL state, no React Context, no Zustand.
- **Seven steps:** (1) Input & Config, (2) Parse, (3) Quiz, (4) Settings, (5) Translate, (6) Validate, (7) Publish. Steps 3 and 5–6 conditionally skipped based on user choices.
- **The "draft model" is `ContentCreationSession`** — backend entity holding wizard-flow metadata (parsed sections JSON, quiz JSON, settings JSON, etc.) with a `outputTalkId` pointing at an in-progress `ToolboxTalk` row that gets created during parse. On publish, the session points at the talk that survives. **No separate `DraftToolboxTalk` table exists.** "Removing the session/draft duplication" means picking one source of truth and retiring the other.
- **Cold refresh loses the session ID** — it lives only in component state with no URL persistence. Hard refresh on any step = blank Step 1 = orphaned backend session. This is a real defect to eliminate in the new wizard, not a side concern.
- **Steps 5 and 6 already share components with Phase 3-4 work** — `ValidationSectionCard`, `FlaggedText`, `ValidationProgressPanel` are imported into the wizard's Translate/Validate steps and into `TranslationWorkflowPanel`. Same backend endpoints. The hooks above them differ (`useSessionValidationRun` vs `useWorkflowStates`). **The validation/translation rebuild is mostly hook-swapping, not UI work.**
- **Parallel state systems:** `ContentCreationSessionStatus` enum on the session tracks wizard-flow state; `TranslationWorkflowState` (Phase 3-4) tracks per-language workflow state. These overlap. The new wizard collapses them — the talk's workflow state is the source of truth.
- **No resume path exists in UI.** A user who closes mid-creation has data on the server but no way to come back to it. The new wizard adds a drafts/in-progress list.
- **The early steps (1-4) and Publish (7) are the heaviest rebuild work**, not the validation steps. Inverted from what one might expect from the §9 framing.
- **No Zod/RHF in the existing wizard.** The codebase has them available (per Phase 4.5b recon) but the wizard predates the adoption. New wizard uses them throughout.

The recon also confirmed the cutover surface is minimal — one "Create New" button in `ToolboxTalkList.tsx` at line 354 points users at the wizard. No other entry points; no "edit draft" links. Cutover changes one router.push destination.

## Phase 5 design decisions (settled)

1. **The new wizard eliminates `ContentCreationSession` as a source of truth.** The canonical `ToolboxTalk` row plus the workflow state machine is the source. The session table either goes away or becomes a thin pointer for wizard-step bookkeeping (decide during 5.2). Audit metadata (reviewer name, document ref, client name, audit purpose) migrates to the talk row.
2. **Wizard state lives in the URL.** Refresh-safe. Step is part of the URL or query params; the talk ID is in the URL once the talk row exists. Pre-talk state (Step 1's input mode and source selection) gets persisted to a thin session record or held in-memory only until the talk is created. Specifics to settle in 5.2.
3. **Every form step uses Zod + react-hook-form.** Matches the codebase's current standard (per Phase 4.5b recon).
4. **Every step is independently testable.** No monolithic `CreateWizard.tsx` holding all step state. Each step receives talk context via props or routing, manages its own form state, persists through dedicated hooks.
5. **Loading and error states are consistent throughout.** Every API call has explicit loading UI, explicit error UI. No silent failures.
6. **Accessibility is in scope.** Keyboard navigation, screen reader semantics on the step list, proper focus management on transitions.
7. **Mobile is considered but not the primary surface.** The wizard is an admin tool; desktop comes first. But it shouldn't break on mobile.
8. **Old wizard stays in place until cutover.** Parallel period bounded to a single Phase 5.6 chunk. The "Create New" button doesn't move until the new wizard is ready end-to-end.

## Phase 5 sub-chunk sketch

- **5.1 — Recon.** ✅ Complete. Findings summarised above.
- **5.2 — Standards doc + scaffolding.** Write `docs/PHASE_5_STANDARDS.md` first (the rubric every subsequent chunk holds itself to). Then scaffold the new wizard's route, navigation framework, URL-based step state, empty step containers. New file tree at `web/src/features/toolbox-talks/components/create-wizard-v2/` (or whatever name we land on). Route at `/admin/toolbox-talks/talks/new` or similar — *not* `/create` which the old one owns.
- **5.3 — Early steps rebuild (Input, Parse, Quiz, Settings).** Each step gets rebuilt to the standards doc. Talk-row created at Step 1 instead of Step 2 (to eliminate the pre-talk session window). Audit metadata migrated to talk row. Zod schemas, RHF, consistent loading/error UI throughout. The most file-volume chunk.
- **5.4 — Translate + Validate step rebuild.** Smaller than it sounds — components are already shared with Phase 3-4. Swap `useSessionValidationRun` for the Phase 3-4 workflow hooks. Read `TranslationWorkflowState` instead of session status. Drop SignalR or keep it depending on what Phase 3-4 offers (worth a small recon at chunk start).
- **5.5 — Publish step rebuild.** Rethink what "publish" means in the new model. Session → talk conversion logic is creation-specific and may or may not still be needed (depends on 5.2 decisions about whether the talk row exists from Step 1). Decide there.
- **5.6 — Cutover.** Re-point the "Create New" button. Old wizard still reachable via direct URL for safety, but no longer the default. Drafts list added for resume.
- **5.7 — Removal.** Delete old wizard files. Remove `ContentCreationSessionStatus` and the session columns it depended on if nothing else reads them. Migration for any orphan session data.

Possibly 5.3 splits further once we're inside it. Possibly 5.4 and 5.5 merge if they turn out small. Treat the sketch as orientation, not commitment.

## Phase 5 estimate

§9's original estimate was 14-20 days. Our actual pace through Phase 4 (one day for the entire phase, eight implementation commits) makes that wildly excessive. Realistic estimate at our pace: 4-7 working days, possibly less if 5.3 doesn't sprawl. The standards doc front-loads decision-making, which should keep individual chunks moving fast.

## Working pattern (continued from Phase 4)

- Recon-first before any implementation prompt
- Tight scoped Claude Code prompts with explicit out-of-scope items
- Agent returns structured reports (tests, build state, files, etc.)
- Verify diffs against code (not prose summaries) before committing
- One commit per logical unit; doc updates ship alongside code (paired commits or combined where natural)
- Push to both `origin/transval` and `company/transval`
- Use `findstr`, `Get-Content -LiteralPath` (PowerShell-quoted paths with brackets), `git diff > file.txt; type file.txt` for verification
- BACKLOG anything deferred; don't lose track of small follow-ups

## Open items inherited from Phase 4

- Phase 7 — workflow notification triggers. TODO comments scattered throughout Phase 4's code (Phase 4.1, 4.3, 4.4 service methods). Not blocking Phase 5.
- BACKLOG §5.6 — MailerSend resilience improvements.
- BACKLOG §1.2.12 — translation generate API: accept language codes instead of names.
- BACKLOG §1.2.13 — workflow event history: render structured payloads.
- BACKLOG §1.2.14 — workflow event history: show triggered-by user names.
- BACKLOG §10 — `ValidationStarted` event falls through to `Initial` state (pre-existing pre-Phase 4 quirk).
- The `LoadingSpinner` component is now duplicated across `DeleteConfirmationDialog` and `DeclineConfirmationDialog`. Worth consolidating into a shared component eventually. Not in Phase 5 scope unless it surfaces naturally.

## Repo invariants

- Branch: `transval`
- Both remotes (`origin` and `company`) carry the same history; every commit pushed to both
- HEAD at session end: `c0a8292`
- Two design docs: `docs/TRANSLATION_WORKFLOW_DESIGN.md` (architecture + phase status), `docs/LEARNING_LIFECYCLE.md` (process learnings)
- `BACKLOG.md` — deferred items
- `CLAUDE.md` — prompt conventions
- Working environment: PowerShell on Windows
- Tests: `tests/QuantumBuild.Tests.Integration/` (xUnit + FluentAssertions, custom WebApplicationFactory, FakeEmailService for Phase 4 work)
- Frontend: Next.js 15 App Router, Tailwind v4 (no config file, theme in `globals.css`), shadcn/ui components, react-hook-form + Zod, Sonner for toasts, React Query for server state

## First moves on resume

1. Read this file (you're doing it). Read the design doc §9. Skim the learning doc and the backlog.
2. Write `docs/PHASE_5_STANDARDS.md`. The standards doc is Phase 5's rubric — what enterprise-grade means concretely for this codebase. Write it before any code. Suggested sections:
   - Component / file structure conventions for the new wizard
   - Form handling: Zod + RHF patterns, validation rules location, error display
   - State management: URL persistence, refresh recovery, resume from draft list
   - API integration: loading state, error state, retry behaviour, optimistic UX
   - Accessibility checklist
   - Mobile responsiveness expectations
   - Testing: what's required per step
   - The defects from the old wizard explicitly being eliminated (so they don't sneak back in)
3. Once the standards doc is in place, commit it, then move to 5.2 scaffolding.

This kickoff doc becomes redundant once Phase 5 is underway — its content gets absorbed into the design doc's §9 Phase 5 Status lines as chunks ship. Delete it or fold it in at Phase 5 closure.