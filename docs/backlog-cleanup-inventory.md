# BACKLOG.md Cleanup — Pre-flight Anchor Inventory

**Date:** 2026-06-15  
**Purpose:** Audit trail for the cleanup chunk. Lists every section anchor with line numbers and identifies all conflicts.

---

## 1. Top-level Section Headers (`# N.`)

| Header text | Line | Status |
|---|---|---|
| `# 1. Content Creation & Translation` | 32 | OK |
| `# 2. QR Workstations & PINs` | 391 | OK |
| `# 3. Employee & User Management` | 499 | OK |
| `# 4. Tenant Management & Regulatory` | 587 | OK |
| `# 5. Infrastructure & Tooling` | 609 | OK |
| `# 6. Security Notes (Product Decisions)` | 769 | OK |
| `# 8. Design review: Auditor audience role on ContentCreationSession.` | 787 | **PROBLEM — wrong number (skips 7), wrong level for its content, should be demoted** |
| `# 7. Recently Closed` | ~1399 | **PROBLEM — appears after `# 8.`, out of order** |

Gap: No `# 7.` in natural ordering (Recently Closed is at the bottom, numbered 7 but physically after 8).  
Fix: Demote `# 8.` Auditor design review to a `#### 1.2.15` entry inside §1.2; no `# 8.` remains. Renumber `# 7. Recently Closed` to `# Recently Closed` (drop the number).

---

## 2. Sub-section Headers (`## N.M`) — Nested entries within #1-#6

These are correctly placed. Listed for completeness only.

| Section | Entries |
|---|---|
| `## 1.1 Translator UAT` | `#### 1.1.1` – `#### 1.1.18` |
| `## 1.2 Translation Behaviour & Regulatory` | `#### 1.2.1` – `#### 1.2.14` (last entry) |
| `## 1.3 Wizard / Create Content UX` | `#### 1.3.1` – `#### 1.3.8` |
| `## 2.1 QR Code Management` | `#### 2.1.1` – `#### 2.1.2` |
| `## 2.2 Assets` | `#### 2.2.1` – `#### 2.2.2` |
| `## 3.x` | `#### 3.1` – `#### 3.13` |
| `## 4.x` | `#### 4.1` – `#### 4.3` |
| `## 5.x` (nested within #5) | `#### 5.1` – `#### 5.14` |
| `## 6.x` | `#### 6.1` – `#### 6.2` |

---

## 3. Flat Floating Entries (`## N.`) — Between #6 Security Notes and #7 Recently Closed

These appear after `# 6.` and before `# 7. Recently Closed`.

| Anchor | Line (approx) | Title | Status |
|---|---|---|---|
| `# 8.` | 787 | Design review: Auditor audience role on ContentCreationSession | **Conflict — top-level header, wrong** |
| `## 8.` | 806 | Integration test suite — comprehensive review post-Phase 5 | **Duplicate §8** |
| `## 9.` | 850 | Hardcoded English assumptions in translation pipeline | OK |
| `## 10.` | 901 | ValidationStarted → Initial state mapping gap | OK |
| `## 11.` | 948 | Cancel external review — end-to-end | OK — needs status update |
| `## 12.` | 972 | Seeder/JWT user representation divergence | OK |
| `## 13.` | 1007 | Mobile audit at Phase 5 closure | OK |
| `## 14.` | 1024 | Learning wizard page header inherits wrong context | OK |
| `## 15.` | 1047 | InputMode column added in wrong migration | OK |
| `## 16.` | 1066 | Refresh Amendment | OK |
| `## 17.` | 1077 | Frontend test framework not installed | OK |
| `## 18.` | 1101 | Post-publish translation editing gap — AwaitingThirdParty languages | **KEEP as §18** |
| `## 19.` | ~1123 | Learning list page shows "Inactive" for draft talks instead of "Draft" | **KEEP as §19** |
| `## 20.` | 1160 | Per-section accept/reject actions missing on Validate step [PRIORITY] | **KEEP as §20 — already Done/Superseded by §23** |
| `## 21.` | 1227 | Edit workflow design for new-wizard talks (P0 — design required) | **RENUMBER → §29** |
| `## 22.` | 1254 | External review user journey not characterized (P1) | **RENUMBER → §30** |
| `## 23.` | 1282 | Translation completion notification mechanism (P1) | **MERGE with §25 → §31** |
| `## 24.` | 1308 | Edit workflow for new-wizard talks (P0 — design specified) | **KEEP as §24** |
| `## 25.` | 1355 | Translation completion notification gap (P1) | **MERGE with §23 → §31** |

---

## 4. Flat Floating Entries — After `# 7. Recently Closed` (duplicates)

These appear after the Recently Closed section, at the bottom of the file. They have conflicting numbers with entries in section 3 above.

| Anchor | Line (approx) | Title | Conflict with | Resolution |
|---|---|---|---|---|
| `## 18.` | 1462 | TenantQueryInvalidator parent-path redirect | §18 in §3 above | **RENUMBER → §26** |
| `## 19.` | 1508 | Wizard Step 4 Settings — tenant defaults | §19 in §3 above | **RENUMBER → §27** |
| `## 20.` | 1536 | ToolboxTalk.Frequency vs RequiresRefresher conflict | §20 in §3 above | **RENUMBER → §28** |
| `## 21.` | 1564 | Post-publish translation management UI — AwaitingThirdParty languages | §21 in §3 above | **KEEP AS §21** (older, canonical) |
| `## 22.` | 1583 | Unit tests for Step 7 reachability rule (depends on §17) | §22 in §3 above | **KEEP AS §22** (older, canonical) |
| `## 23.` | 1610 | Reviewer-action UI missing on Validate step (consolidated) | §23 in §3 above | **KEEP AS §23** — update status to Done |

---

## 5. Cross-reference Inventory

Every `§N` reference found in the file:

| Reference | In section | Refers to | Valid after cleanup? |
|---|---|---|---|
| `§8` | §12 ("Deferred to post-Phase-5 review (§8)") | Integration test suite | Update to `§5.15` |
| `§8` | §12 ("seeder/JWT reconciliation (§12)") | self-ref | OK |
| `§10.2` | §13 ("per §10.2") | PHASE_5_STANDARDS §10.2 — NOT a BACKLOG entry | OK (different namespace) |
| `§11` | §21-post-pub ("BACKLOG §11 tracks") | Cancel external review | Update when §11 status updated |
| `§12` | §8/Integration test ("the seeder/JWT reconciliation (§12)") | Seeder/JWT divergence | OK — §12 stays §12 |
| `§17` | §22-unit-tests heading ("depends on §17") | Frontend test framework | OK — §17 stays §17 |
| `§17` | §22-unit-tests body | Frontend test framework | OK |
| `§1.3.3` | §13 ("per §10.2 + BACKLOG §1.3.3") | Drag-to-reorder discoverability | OK — §1.3.3 stays |
| `§20` | §23-reviewer heading ("Supersedes: §20") | Per-section accept/reject gap | OK — §20 stays §20 |
| `§21` | §23-reviewer body ("tracked separately as §21") | Post-pub translation mgmt UI | OK — §21 stays §21 (it's the correct canonical §21 after cleanup) |
| `§22` | §17 ("add the five test cases in...`stepOrder.test.ts`") | (no explicit §22 ref in §17) | N/A |
| `§23` | §20 heading ("Status: Superseded by §23") | Reviewer-action UI | OK — §23 stays §23 |
| `§23` | §22-unit-tests body | Reviewer-action UI | OK |

---

## 6. Renumbering Map

| Old anchor | New anchor | Title |
|---|---|---|
| `# 8.` Auditor design review (top-level) | `#### 1.2.15` | Design review: Auditor audience role on ContentCreationSession |
| `## 8.` Integration test suite | `#### 5.15` | Integration test suite — comprehensive review post-Phase 5 |
| `## 18.` TenantQueryInvalidator (after Recently Closed) | `## 26.` | TenantQueryInvalidator parent-path redirect |
| `## 19.` Wizard Step 4 Settings (after Recently Closed) | `## 27.` | Wizard Step 4 Settings — tenant defaults |
| `## 20.` Frequency conflict (after Recently Closed) | `## 28.` | ToolboxTalk.Frequency vs RequiresRefresher conflict |
| `## 21.` Edit workflow design (before Recently Closed) | `## 29.` | Edit workflow design for new-wizard talks |
| `## 22.` External review journey (before Recently Closed) | `## 30.` | External review user journey not characterized |
| `## 23.` + `## 25.` notification entries (merged) | `## 31.` | Translation completion notification (merged) |

Anchors that KEEP their number: §18 Post-pub gap, §19 Inactive label, §20 Per-section (Done), §21 Post-pub mgmt UI, §22 Unit tests Step 7, §23 Reviewer-action UI (update to Done), §24 Edit workflow design specified.

---

## 7. Status Updates Required

| Entry | Current status | New status |
|---|---|---|
| `§11` Cancel external review | "No backend implementation exists" | Update: backend endpoint now exists and works (per §21 / 5.5a gap-check); frontend UI gap remains |
| `§20` Per-section accept/reject | ✅ Done — 2026-06-15 (Superseded by §23) | Verify §23 reference still correct after renumbering. §23 stays §23, so reference valid. No change needed. |
| `§23` Reviewer-action UI missing | Open — pending product decision | ✅ Done — 2026-06-15 — Strict review workflow ported with backend enforcement, auto-accept Pass, no-bypass design. Cache invalidation follow-up fix shipped 2026-06-15. Smoke verified across Scenarios 1, 2, 3 (see `docs/phase-5/reports/strict-review-workflow-port.md`). |
