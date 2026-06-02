# CertifiedIQ — Sprint (Current Focus)

**Last updated:** 28 May 2026
**Purpose:** Active prioritised list of next work. References item IDs from `BACKLOG.md` (the source of truth). When an item is closed here, mark it done in BACKLOG.md and remove from this list.

This document is dynamic — expect it to be reshuffled as priorities shift. The full set of items lives in BACKLOG.md.

---

## Sprint focus

Three streams in parallel, in priority order:

**Stream 1 — Translator UAT response (Jessie Ryan).** Real customer feedback. This is what makes the next demo land properly. Priority because it's the only customer-validated set of issues currently on the list.

**Stream 2 — Preventive engineering quality.** The tenant-filter sweep audit — three production bugs from the same root cause this week. Fourth instance is statistically likely and finding it preventively is cheaper than finding it in customer use.

**Stream 3 — Roadmap: Asset Management.** Substantial new feature, but distinct from the above. Should be scoped as its own work-stream once the immediate UAT polish is done.

---

## Now (this week)

### 1. UAT — Prompt-only fixes (quickest wins, customer-validated)

Five issues, three files. Mostly prompt edits with one small frontend toggle. Estimated half-day to a day done properly with testing.

- **BACKLOG 1.1.4 + 1.1.5** — Slideshow `slideChanged` postMessage. P1. `SlideshowGenerationPrompts.cs` only. Fixes counter mismatch and Back button. *One prompt change, three near-identical blocks in the file.*
- **BACKLOG 1.1.10** — Verbatim parse mode. P2. `SectionGenerationPrompts.cs` + a Step 1 toggle on `InputConfigStep.tsx`. *Diff in original UAT brief.*
- **BACKLOG 1.1.14** — Slide WCAG contrast. P2. `SlideshowGenerationPrompts.cs` only. *Combine with 1.1.4 in same commit since same file.*
- **BACKLOG 1.1.15** — Audience-aware quiz. P2. `QuizGenerationPrompts.cs` only. Add `audienceRole` parameter; new rules block. *Diff in original UAT brief.*

**Why these first:** prompt-only changes are low risk, isolated to AI behaviour, no schema, no migrations, no DB drift potential. Customer-validated. Ship together as one logical "Translator polish" PR.

---

### 2. UAT P0s — Validation trust

The headline UAT issues. "Trust in the validation readout" is the headline differentiator. If these go unfixed, the next customer hits them in their first run.

- **BACKLOG 1.1.1** — Validation summary contradicts itself ("0/4 passed, score 100%"). P0. Backend + frontend; pair with 1.1.12 (badge tint).
- **BACKLOG 1.1.2** — Final score 100% EN / 80% RU. P0. Backend filter source-from-targets; frontend labelling on publish step. Smaller than it sounds.
- **BACKLOG 1.1.3** — No path to edit English source and re-validate. P0. The biggest of the three — needs a new field (`editedOriginalText`), backend persistence, single-section re-validation job, propagation logic. Plan as its own work item.

**Why next:** these undermine the headline value of TransVal as a product differentiator. The first two are smaller; the third is real engineering work. Schedule (1) and (2) as a single PR; treat (3) as its own.

---

### 3. Tenant-filter sweep audit

- **BACKLOG 5.1** — Tenant-filter sweep audit. P1. Preventive sweep across every Hangfire job and every `[AllowAnonymous]` endpoint.

**Why now:** three instances of this exact bug have shipped to production already (bulk import, subtitle job, subtitle content). Each surfaced because a customer or tester hit the symptom — not because we found it preventively. Fourth instance is statistically likely. Cost of the sweep is hours; cost of finding the fourth in a demo is reputational. Do it once, completely.

---

## Next (after current set lands)

### 4. UAT P1s — workflow breakers

The next tier of UAT items. Each is contained but addresses real customer friction.

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

### 6. Engineering quality / proper fixes

These close loose ends from this session's targeted patches:

- **BACKLOG 3.1** — Unify user creation on throwaway-password + invitation flow. P1. Proper fix for the welcome email gap fixed this session. Closes 3.2 (bulk import partial-row recovery) as a side effect.
- **BACKLOG 5.2** — Migration Designer.cs guard. P2. Prevents recurrence of the silent-migration-skip bug from this session.
- **BACKLOG 5.3** — Development DB drift sweep. P2. Identify any other drift like the `GlossaryCorrectionsJson` case.
- **BACKLOG 3.10** — Email send logging fix. P3. Small but caused real-world confusion this session.

---

## Then (medium-term)

### 7. Asset Management — major new feature

- **BACKLOG 2.2.1** — Asset Management. P1.
- Substantial work. Plan as its own dedicated sprint or multi-sprint stream.
- Resolve open questions first (taxonomy, scheduling-vs-scan-only, reporting surface) — these are short product decisions but blocking for build.
- Reuses Employee, Site, PIN, QR code, tenant settings infrastructure heavily. Worth a small spike upfront to confirm reuse scope before committing engineering time.

### 8. Translation behaviour clarification

- **BACKLOG 1.2.1** — Translation validation conditional on regulatory applicability. **PD — product decision required.** Don't start engineering until the always-on vs opt-in vs tenant-toggle question is settled. Conversation with boss + commercial stakeholders.
- **BACKLOG 1.2.2** — Surface regulatory applicability to user. P2. Buildable once 1.2.1 is decided.

### 9. Long-running job UX

- **BACKLOG 1.3.5** — Fire-and-notify pattern for long jobs. P2. Replaces polling. Touches bulk import, content generation, validation, corpus runs. Good piece of work to do in one go.

---

## Later / on radar

Pulled from BACKLOG; not prioritised yet but kept in view:

- Wizard UX cluster (1.3.1, 1.3.2, 1.3.3, 1.3.4) — could be a single "wizard polish" pass
- 2FA (3.9) — when enterprise customers ask
- AI Chat Assistant (5.8) — forward-looking
- Cross-section remediation (1.2.9) — quality-of-life for high-volume customers
- Training Evidence Pack extensions (3.5) — sector-specific work

---

## Decisions pending (not buildable until resolved)

- **BACKLOG 1.2.1** — Translation validation conditional model. Product / commercial decision.
- **BACKLOG 2.2.1 open questions** — Asset taxonomy, scheduling-vs-scan-only, reporting surface, asset bulk import format.

---

## How to use this file

- **When starting work on an item:** mark it `In Progress` here and in BACKLOG.md.
- **When closing an item:** mark it done in BACKLOG.md (move to "Recently Closed"), remove from SPRINT.md.
- **When adding a new urgent item:** add to BACKLOG.md first (gives it a stable ID), then pull into the right section here.
- **Re-prioritise freely.** This is a working document. The point is "what we're focusing on now," not "what we committed to a year ago."

---

*Source of truth: `BACKLOG.md`.*