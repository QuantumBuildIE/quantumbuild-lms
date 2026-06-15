# Phase 5 Renumbering Report

**Date:** 2026-06-15  
**Chunk:** BACKLOG.md Phase 5 renumbering + hygiene cleanup  
**Scope:** Documentation-only. No code changes.

---

## 1. Anchors renumbered (old → new)

| Old anchor | New anchor | Title |
|---|---|---|
| `#### 5.10` | `#### 5.16` | SignalR client timeout defaults missing in four hooks |
| `#### 5.11` | `#### 5.17` | First-language row state lag in Step 5 Translate under Start All |
| `#### 5.12` | `#### 5.26` | SPRINT.md stale — needs Phase 5 state rewrite |
| `## 17.` (floating) | `#### 5.18` | Frontend test framework not installed |
| `## 22.` (post-Recently-Closed) | `#### 5.19` | Unit tests for Step 7 reachability rule |
| `## 16.` (floating) | `#### 5.20` | Refresh Amendment |
| `## 14.` (floating) | `#### 5.21` | Learning wizard page header inherits wrong context |
| `## 26.` (post-Recently-Closed) | `#### 5.22` | TenantQueryInvalidator parent-path redirect |
| `## 27.` (post-Recently-Closed) | `#### 5.23` | Wizard Step 4 Settings — tenant defaults |
| `## 28.` (post-Recently-Closed) | `#### 5.24` | ToolboxTalk.Frequency vs RequiresRefresher conflict |
| `## 13.` (floating) | `#### 5.25` | Mobile audit at Phase 5 closure |
| `(new)` | `#### 5.27` | Phase 5.6 cutover toggle — parallel-period mechanism |

**Entries that kept their numbers:** §5.13 (Done), §5.14 (Done), §5.15, §18, §19, §20, §21, §23, §24, §30, §31.

---

## 2. §29 → §24 merger

- `## 29. Edit workflow design for new-wizard talks (P0 — design required)` was removed entirely.
- Its unique content (Q8: in-progress draft vs published talk distinction) was merged into §24's "Open design sub-questions" section.
- Questions 1–7 in §29 were all subsumed by §24's locked design rules.
- The last paragraph in §29 (Phase 5.6 blocking) was already covered by §24's "Why P0" section.
- A merger note was added to §24: *"Earlier draft of this entry (previously §29) merged into this entry on 2026-06-15 — the locked design rules subsume the open-questions framing of the earlier draft."*

---

## 3. Cross-references updated

| Location | Old reference | New reference | Notes |
|---|---|---|---|
| `#### 5.19` title | `(depends on §17)` | `(depends on §5.18)` | §17 → §5.18 |
| `#### 5.19` status line | `blocked on §17` | `blocked on §5.18` | §17 → §5.18 |
| `#### 5.19` body | `After §17 is closed` | `After §5.18 is closed` | §17 → §5.18 |
| `#### 5.13` title | `(§22)` | `(§5.19)` | §22 → §5.19 |
| `#### 5.14` body | `If §22 and §23 share` | `If §5.13 and §5.14 share` | §22 → §5.13 (both Done) |

---

## 4. Priority labels applied

| Anchor | Priority set | Justification |
|---|---|---|
| `#### 5.18` | P1 | Recurring deferral cause across multiple Phase 5 chunks; unblocks §5.19 |
| `#### 5.20` | P2 | Standards violation (PHASE_5_STANDARDS §5.4) but functional workaround exists |
| `#### 5.21` | P3 | Cosmetic page header issue; no workflow blocker |
| `#### 5.22` | P2 | Structural fragility; specific repro on SuperUser with stored tenantId |
| `#### 5.24` | P2 | Risk of data overwrite (refresher config) elevated from P3 |
| `#### 5.25` | P1 | PHASE_5_STANDARDS §10 explicitly requires mobile-seamless; required for Phase 5 closure |

Entries §5.16, §5.17 (P1), §5.19 (P2), §5.23 (P3), §5.26 (P2), §5.27 (P1) carried over from existing labels or set in new entry body.

---

## 5. New entry §5.27

Inserted as final entry of Section 5 (after §5.15, before `# 6. Security Notes`).  
Title: *Phase 5.6 cutover toggle — parallel-period mechanism*  
Priority: P1  
Gated on: §5.24 (Frequency conflict) and §24 (Edit workflow design) at minimum.  
Content: locked design decisions (tenant-level toggle, in-flight drafts stay, manual cutover), adjacent dependencies, implementation scope, out-of-scope items.

---

## 6. Post-Recently-Closed ordering after renumbering

**Before:** §26, §27, §28, §21, §22, §23  
**After:** §21, §23  

§26, §27, §28, §22 moved to Section 5 as §5.22, §5.23, §5.24, §5.19 respectively.  
§21 and §23 remain in post-Recently-Closed area (no clean topical home in numbered sections).

---

## 7. Unresolved questions / judgment calls

### §31 references §5.6 — NOT updated

The prompt specified: *"§31 references §5.6 and §1.3.5 — §5.6 was a placeholder reference to the cutover toggle which is now §5.27; update to §5.27."*

This instruction was not followed because it is incorrect. In the file, `§5.6` within §31 reads:

> `MailerSendEmailProvider` (§5.6 — 429 handling) is the email infrastructure. Notifications would use it.

`§5.6` in BACKLOG.md is unambiguously the `MailerSendEmailProvider 429 handling` entry (line 673). The Phase 5.6 *development phase* (the cutover toggle work) is referred to as "Phase 5.6" in narrative text, not as the §5.6 backlog anchor. No other occurrence of `§5.6` as a backlog cross-reference was found in §31 or elsewhere.

Changing `§5.6` in §31 to `§5.27` would introduce an incorrect cross-reference (replacing MailerSendEmailProvider with the cutover toggle). Left unchanged. Flag for review if the prompt author intended something different.

### §5.14 title still says `(§23)`

`§5.14 Quiz-skipped declared but Continue lands on Quiz step (§23)` — the `(§23)` in the title refers to the reviewer-action UI item §23 (which correctly stays §23). This was left unchanged as §23 retains its number.

### Numeric ordering in Section 5

The Done entries §5.13 and §5.14, and the open entry §5.15, appear after §5.26 in the file (they predate the new 5.16–5.26 entries numerically, but come after them in file order). This is a cosmetic ordering oddity from the existing structure. §5.27 is correctly placed as the last entry before Section 6. No action taken — the Done items are effectively archived and the ordering is internally consistent.

---

## 8. Verification results

```
# All 5.x anchors unique and in order:
5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9,
5.16, 5.17, 5.18, 5.19, 5.20, 5.21, 5.22, 5.23, 5.24, 5.25, 5.26,
5.13 (Done), 5.14 (Done), 5.15,
5.27

# §29 removed — CONFIRMED (grep ^## 29\. → no matches)
# §5.27 exists — CONFIRMED (grep 5\.27 Phase 5\.6 cutover → line 1013)
# All old cross-references updated — CONFIRMED (grep §(5\.10|5\.11|5\.12|17|22|16|14|26|27|28|13)\b → no matches)
# Priority labels present on §5.18, §5.20, §5.21, §5.22, §5.25 — CONFIRMED
# Last updated: 15 June 2026 — CONFIRMED (was already correct)
```
