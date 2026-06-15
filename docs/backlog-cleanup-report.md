# BACKLOG.md Cleanup Report

**Date:** 2026-06-15  
**Branch:** transval

---

## Anchors Renumbered

| Old anchor | New anchor | Title |
|---|---|---|
| `# 8.` (top-level) | `#### 1.2.15` | Design review: Auditor audience role on ContentCreationSession |
| `## 8.` (floating) | `#### 5.15` | Integration test suite — comprehensive review post-Phase 5 |
| `## 21.` (before Recently Closed) | `## 29.` | Edit workflow design for new-wizard talks |
| `## 22.` (before Recently Closed) | `## 30.` | External review user journey not characterized |
| `## 23.` + `## 25.` (merged) | `## 31.` | Translation completion notification gap |
| `## 18.` (after Recently Closed) | `## 26.` | TenantQueryInvalidator parent-path redirect |
| `## 19.` (after Recently Closed) | `## 27.` | Wizard Step 4 Settings — tenant defaults |
| `## 20.` (after Recently Closed) | `## 28.` | ToolboxTalk.Frequency vs RequiresRefresher conflict |

Anchors that kept their number: §18, §19, §20, §21, §22, §23, §24.

---

## Cross-references Updated

| Reference | Location | Old target | New target |
|---|---|---|---|
| `§8` | `## 12.` body, line ~958 ("see §8 history") | Integration test suite | `§5.15` |
| `§8` | `## 12.` body, line ~978 ("Deferred to post-Phase-5 comprehensive test review (§8)") | Integration test suite | `§5.15` |
| `§11` note | `## 21.` Post-pub mgmt UI body | "BACKLOG §11 is now stale — the backend was built in a later phase. Update or close that entry." | Updated to: "BACKLOG §11 has been updated — the backend is confirmed complete as of 2026-06-15." |

Cross-references verified as still-valid without change:
- `§23` in `## 20.` ("Superseded by §23") — §23 stays §23 ✓
- `§21` in `## 23.` body ("tracked separately as §21") — §21 stays §21 ✓
- `§17` in `## 22.` heading ("depends on §17") — §17 stays §17 ✓
- `§23` in `## 30.` body ("§23 explicitly punted...") — §23 stays §23 ✓

---

## Status Changes Applied

| Entry | Old status | New status |
|---|---|---|
| `## 11.` Cancel external review | "No backend implementation exists..." | Updated: backend endpoint now exists and works (per §21 / 5.5a gap-check); remaining gap is the frontend "Cancel external review" button |
| `## 23.` Reviewer-action UI missing (after Recently Closed) | `Open — pending product decision` | `✅ Done — 2026-06-15 — Strict review workflow ported with backend enforcement, auto-accept Pass, no-bypass design. Cache invalidation follow-up fix shipped 2026-06-15. Smoke verified Scenarios 1, 2, 3.` |

`## 20.` Per-section accept/reject: already showed `✅ Done — 2026-06-15` with "Superseded by §23". §23 reference remains valid after renumbering. No change needed.

---

## Structural Relocations

| What moved | From | To |
|---|---|---|
| Auditor design review content | `# 8.` top-level header (between §6 and the floating entries) | `#### 1.2.15` under `## 1.2 Translation Behaviour & Regulatory` |
| Integration test suite content | `## 8.` floating entry (same area) | `#### 5.15` under `# 5. Infrastructure & Tooling` |
| Recently Closed header | `# 7. Recently Closed` (numbered, out of order) | `# Recently Closed` (unnumbered) |

---

## Merges

**`## 23. Translation completion notification mechanism` + `## 25. Translation completion notification gap` → `## 31. Translation completion notification gap`**

- Entry A (§23 before Recently Closed) was a stakeholder-level description of the open investigation.
- Entry B (§25 before Recently Closed) was the completed investigation with confirmed findings.
- Merged result keeps Entry B's confirmed-gap framing with the investigation outcome, using Entry A's initial investigation questions as context where not redundant.
- §25 anchor is now vacant.

---

## Judgment Calls

1. **§24 retained as-is.** `## 24. Edit workflow for new-wizard talks (P0 — design specified)` is a different and more detailed entry from the renumbered `## 29.` (formerly §21 "Edit workflow design"). §24 has locked design rules; §29 has the original open questions. These are related but not duplicates. §24 kept at §24 without change.

2. **§25 anchor left vacant** after the merge into §31. Not reused to avoid confusion in any external references to §25 that may exist in git history, commit messages, or PR descriptions.

3. **`## 11.` not closed** — the prompt asked for a status update noting backend is complete. The backend is confirmed complete per §21 evidence; the frontend trigger remains unbuilt. Updated the entry body to reflect this split state. Did not change the entry's `Status:` tag (the entry has no structured Status field — it uses narrative prose).

4. **`#### 1.2.15` heading level** — The prompt said to move the Auditor design review "under §1.2 as a new entry". All other §1.2 entries use `####`. The content was adapted to use the same `####` level and structured format (`Priority`, `Origin`, `Status` bullets) consistent with other §1.2 entries.

---

## Unresolved Questions (none)

No ambiguities were encountered that required a judgment call not covered by the prompt's explicit instructions.

---

## Verification Commands Run

```powershell
# No duplicate ## N. anchors
Select-String -LiteralPath BACKLOG.md -Pattern '^## \d+\.'
# Result: all numbers unique ✓

# No # 7. or # 8. top-level headers
Select-String -LiteralPath BACKLOG.md -Pattern '^# [78]\.'
# Result: no matches ✓

# Last updated correct
Select-String -LiteralPath BACKLOG.md -Pattern 'Last updated:'
# Result: 15 June 2026 ✓
```
