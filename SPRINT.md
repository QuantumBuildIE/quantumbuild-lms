# CertifiedIQ — Sprint (Current Focus)

**Last updated:** 2 June 2026
**Purpose:** Active prioritised list of next work. References item IDs from `BACKLOG.md` (the source of truth). When an item is closed here, mark it done in BACKLOG.md and remove from this list.

This document is dynamic — expect it to be reshuffled as priorities shift. The full set of items lives in BACKLOG.md.

---

## Sprint focus

**Translator UAT response is partially done — five issues shipped (the prompt-only fixes plus verbatim mode). Three P0s and four P1s remain.** Engineering quality has become a louder concern this week: the migration-creation process needs locking down to prevent recurrence of the Designer.cs trap, and the tenant-filter sweep needs doing. The Demo refresh is a substantial, well-scoped piece of work waiting for its slot.

---

## Now (this week)

### 1. Engineering quality — migration process lockdown

- **BACKLOG 5.2** — Migration creation process & Designer.cs guard. **P1 (escalated from P2).** Four instances of the same trap this session. Two parts: (a) add explicit directive to CLAUDE.md that all migrations must use `dotnet ef migrations add`, never hand-written; (b) build-time guard that fails the build if any `*.cs` migration file lacks a matching `*.Designer.cs`.
- **BACKLOG 5.3** — Migration forensic audit. **P2.** Scan the migrations folder for any remaining missing Designer.cs files, inline `[Migration]` attributes, or synthetic timestamps. Confidence that no more silent-skip surprises lurk.

**Why these first now:** the Designer.cs trap has cost ~3 hours of debugging across two sessions. The process change is a 10-line CLAUDE.md edit; the build guard is a small script in the build pipeline. The forensic audit is investigation-only. Together they're a half-day of work that prevents recurrence permanently.

---

### 2. UAT P0s — validation trust

The headline UAT issues. "Trust in the validation readout" is the headline differentiator. If these go unfixed, the next customer hits them in their first run.

- **BACKLOG 1.1.1** — Validation summary contradicts itself ("0/4 passed, score 100%"). Backend + frontend; pair with 1.1.12 (badge tint).
- **BACKLOG 1.1.2** — Final score 100% EN / 80% RU. Backend filter source-from-targets; frontend labelling on publish step. Smaller than it sounds.
- **BACKLOG 1.1.3** — No path to edit English source and re-validate. The biggest of the three — needs a new field (`editedOriginalText`), backend persistence, single-section re-validation job, propagation logic. Plan as its own work item.

**Why now:** these undermine the headline value of TransVal as a product differentiator. The first two are smaller; the third is real engineering work. Schedule (1) and (2) as a single PR; treat (3) as its own.

---

### 3. Tenant-filter sweep audit

- **BACKLOG 5.1** — Tenant-filter sweep audit. **P1.** Preventive sweep across every Hangfire job and every `[AllowAnonymous]` endpoint.

**Why now:** three instances of this exact bug have shipped to production already (bulk import, subtitle job, subtitle content). Each surfaced because a customer or tester hit the symptom — not because we found it preventively. Fourth instance is statistically likely. Cost of the sweep is hours; cost of finding the fourth in front of a customer is reputational. Do it once, completely.

---

## Next (after current set lands)

### 4. UAT P1s — workflow breakers

- **BACKLOG 1.1.6** — Continue button wedged after empty languages. Small.
- **BACKLOG 1.1.7** — Section content editable after parse. Adds inline body editor.
- **BACKLOG 1.1.8** — Back-nav from Parse preserves edits. State persistence.
- **BACKLOG 1.1.9** — Preview as Employee renders pending state. Small.

Schedule as a "Wizard navigation & editability" PR — they share UI surfaces.

### 5. UAT P2s — completeness items

- **BACKLOG 1.1.11** — .docx input support (extractor already exists; only wizard wiring needed).
- **BACKLOG 1.1.12** — Review badge tint conditional (pairs with 1.1.1).
- **BACKLOG 1.1.13** — Diff view on Validation Results.
- **BACKLOG 1.1.16** — Quiz delete undo.
- **BACKLOG 1.1.17** — Per-tenant Translator defaults.

### 6. Other engineering quality

- **BACKLOG 3.1** — Unify user creation on throwaway-password + invitation flow. **P1.** Proper fix for the welcome email gap fixed earlier. Closes 3.2 (bulk import partial-row recovery) as a side effect.
- **BACKLOG 5.4** — Development DB drift sweep. P2. Identify any other drift like the `GlossaryCorrectionsJson` case.
- **BACKLOG 3.10** — Email send logging fix. P3. Small but caused real-world confusion.

---

## Then (medium-term)

### 7. Demo environment refresh + three-tier workflow

- **BACKLOG 5.7** — Demo environment refresh. **P1.** Substantial piece of work — substantial sprint card in BACKLOG with 20 ordered tasks across 6 phases, env-var checklist (9 blocks), migration risk assessment, smoke test checklist. Pre-requisite decision: R2 shared bucket vs separate (recommendation: Option A shared for initial bring-up).
- **Establishes ongoing workflow:** Development for build/test → Demo for business sign-off and prospect demos → Production for customer release. Each environment promoted by `git merge transval → demo` or `→ main`.

**Why "Then" not "Now":** big piece of work, no urgent customer pressure, depends on having UAT and engineering-quality items mostly cleared so the Demo represents a stable view of the product. Worth doing properly when it gets attention, not rushed.

### 8. Asset Management — major new feature

- **BACKLOG 2.2.1** — Asset Management. **P1.**
- Substantial work. Plan as its own dedicated sprint or multi-sprint stream.
- Resolve open questions first (taxonomy, scheduling-vs-scan-only, reporting surface) — these are short product decisions but blocking for build.
- Reuses Employee, Site, PIN, QR code, tenant settings infrastructure heavily. Worth a small spike upfront to confirm reuse scope before committing engineering time.

### 9. Translation behaviour clarification

- **BACKLOG 1.2.1** — Translation validation conditional on regulatory applicability. **PD — product decision required.** Don't start engineering until the always-on vs opt-in vs tenant-toggle question is settled. Conversation with boss + commercial stakeholders.
- **BACKLOG 1.2.2** — Surface regulatory applicability to user. P2. Buildable once 1.2.1 is decided.

### 10. Long-running job UX

- **BACKLOG 1.3.5** — Fire-and-notify pattern for long jobs. P2. Replaces polling. Touches bulk import, content generation, validation, corpus runs. Good piece of work to do in one go.

---

## Later / on radar

Pulled from BACKLOG; not prioritised yet but kept in view:

- Wizard UX cluster (1.3.1, 1.3.2, 1.3.3, 1.3.4) — could be a single "wizard polish" pass
- 2FA (3.9) — when enterprise customers ask
- AI Chat Assistant (5.9) — forward-looking
- Cross-section remediation (1.2.9) — quality-of-life for high-volume customers
- Training Evidence Pack extensions (3.5) — sector-specific work
- File upload size display (1.3.8) — small visual fix

---

## Decisions pending (not buildable until resolved)

- **BACKLOG 1.2.1** — Translation validation conditional model. Product / commercial decision.
- **BACKLOG 2.2.1 open questions** — Asset taxonomy, scheduling-vs-scan-only, reporting surface, asset bulk import format.
- **BACKLOG 5.7** — Demo R2 bucket model (shared vs separate). Quick decision needed before Demo refresh starts.

---

## Recently closed (last sprint)

**2 June 2026 — Translator polish batch shipped to Production:**
- 1.1.4 / 1.1.5 — Slideshow counter + Back button
- 1.1.10 — Verbatim parse mode
- 1.1.14 — Slide WCAG contrast
- 1.1.15 — Audience-aware quiz generation
- Section divider styling improvement (wizard)
- Four Designer.cs files restored (three drift cases + AudienceRole)

See BACKLOG.md Section 7 for full closed history.

---

## How to use this file

- **When starting work on an item:** mark it `In Progress` here and in BACKLOG.md.
- **When closing an item:** mark it done in BACKLOG.md (move to "Recently Closed"), remove from SPRINT.md.
- **When adding a new urgent item:** add to BACKLOG.md first (gives it a stable ID), then pull into the right section here.
- **Re-prioritise freely.** This is a working document. The point is "what we're focusing on now," not "what we committed to a year ago."

---

*Source of truth: `BACKLOG.md`.*