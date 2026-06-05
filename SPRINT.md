# CertifiedIQ — Sprint (Current Focus)

**Last updated:** 3 June 2026
**Purpose:** Active prioritised list of next work. References item IDs from `BACKLOG.md` (the source of truth). When an item is closed here, mark it done in BACKLOG.md and remove from this list.

This document is dynamic — expect it to be reshuffled as priorities shift. The full set of items lives in BACKLOG.md.

---

## Sprint focus

**Three UAT P0s are now closed. The engineering quality preventive work is done. The next tier is the UAT P1s — workflow breakers from real customer feedback — plus the proper unification of the user-creation flow.** Demo refresh and Asset Management remain as larger pieces of work for dedicated sprint time.

---

## Next (after current set lands)

### 3. UAT P2s — completeness items

- **BACKLOG 1.1.11** — .docx input support (extractor already exists; only wizard wiring needed).
- **BACKLOG 1.1.13** — Diff view on Validation Results (`diff-words` library).
- **BACKLOG 1.1.16** — Quiz delete undo (toast Undo via sonner).
- **BACKLOG 1.1.17** — Per-tenant Translator defaults.

### 4. Rich-text editor for section editing

- **BACKLOG 1.1.18** — Rich-text editor for section source/translation editing. **P2.**
  - The proper long-term fix for the formatting-loss limitation disclosed in 1.1.3.
  - ProseMirror recommended over TipTap (no commercial pressure, stable API, smaller bundle for narrow scope).
  - Estimated 4-5 days. Plan as dedicated focus, not interleaved with smaller items.
  - Closes the amber warning currently shown on source edits.

### 5. Other engineering quality

- **BACKLOG 5.4** — Development DB drift sweep. Identify any other drift like the `GlossaryCorrectionsJson` case. Bigger scope now we know the codebase has been routinely hand-writing migrations.
- **BACKLOG 3.10** — Email send logging fix. P3. Small but caused real-world confusion earlier in the session.

---

## Then (medium-term)

### 6. Demo environment refresh + three-tier workflow

- **BACKLOG 5.7** — Demo environment refresh. **P1.** Substantial sprint card in BACKLOG with 20 ordered tasks across 6 phases, env-var checklist (9 blocks), migration risk assessment, smoke test checklist. Pre-requisite decision: R2 shared bucket vs separate (recommendation: Option A shared for initial bring-up).
- Establishes the ongoing three-tier workflow: Development for build/test → Demo for business sign-off and prospect demos → Production for customer release. Each environment promoted by `git merge transval → demo` or `→ main`.

**Why "Then" not "Now":** big piece of work, no urgent customer pressure, depends on having UAT and engineering-quality items mostly cleared so the Demo represents a stable view of the product. Worth doing properly when it gets attention.

### 7. Asset Management — major new feature

- **BACKLOG 2.2.1** — Asset Management. **P1.**
- Substantial work. Plan as its own dedicated sprint or multi-sprint stream.
- Resolve open questions first (taxonomy, scheduling-vs-scan-only, reporting surface) — these are short product decisions but blocking for build.
- Reuses Employee, Site, PIN, QR code, tenant settings infrastructure heavily. Worth a small spike upfront to confirm reuse scope before committing engineering time.

### 8. Translation behaviour clarification

- **BACKLOG 1.2.1** — Translation validation conditional on regulatory applicability. **PD — product decision required.** Don't start engineering until the always-on vs opt-in vs tenant-toggle question is settled.
- **BACKLOG 1.2.2** — Surface regulatory applicability to user. P2. Buildable once 1.2.1 is decided.

### 9. Long-running job UX

- **BACKLOG 1.3.5** — Fire-and-notify pattern for long jobs. P2. Replaces polling. Touches bulk import, content generation, validation, corpus runs.

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

**5 June 2026 — User-creation unification (shipped to Production):**
- BACKLOG 3.1 — UI user-create, tenant onboarding, bulk import now unified on throwaway-password + invitation-email pattern; admin no longer sets passwords for new users; new tenant admins go through set-password flow on first login

**4 June 2026 — Wizard cascade-reset hardening + UAT P1 batch (shipped to Production):**
- UAT 1.1.6, 1.1.7, 1.1.8, 1.1.9 — wizard navigation, editability, preview pending state
- CONTENT-LIFECYCLE §6.4, §6.5, §6.10, §6.11 closed; §6.2 partially closed (8 sites)
- New `SetAuditFields` interceptor sharp edge (§6.12) documented — system-wide blast radius
- Lifecycle map document introduced and consolidated

**3 June 2026 — UAT P0s + tenant-filter sweep batch:**
- 1.1.1 — Validation summary twin-metric display + conditional badge tinting (closes 1.1.12)
- 1.1.2 — EN-from-targets filter + labelled scores + index-misalignment fix
- 1.1.3 — Edit English source and re-validate (with disclosed formatting-loss limitation tracked as 1.1.18)
- AuthService silent tenant-filter bug closed
- Note 21 interface-enqueue fix
- Tenant-filter forensic audit complete (5.1)
- Migration forensic audit complete (5.3)
- Build-time migration guard shipped (5.2)
- CLAUDE.md hygiene — Note 28 + archive

**2 June 2026 — "Translator polish" batch:**
- Slideshow postMessage + bridge removal (1.1.4 / 1.1.5)
- Verbatim parse mode (1.1.10)
- Slide WCAG contrast (1.1.14)
- Audience-aware quiz generation (1.1.15)
- Section divider styling
- Four Designer.cs files restored

See BACKLOG.md Section 7 for full closed history.

---

## How to use this file

- **When starting work on an item:** mark it `In Progress` here and in BACKLOG.md.
- **When closing an item:** mark it done in BACKLOG.md (move to "Recently Closed"), remove from SPRINT.md.
- **When adding a new urgent item:** add to BACKLOG.md first (gives it a stable ID), then pull into the right section here.
- **Re-prioritise freely.** This is a working document. The point is "what we're focusing on now," not "what we committed to a year ago."

---

*Source of truth: `BACKLOG.md`.*