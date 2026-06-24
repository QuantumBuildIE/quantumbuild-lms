# `/my-operators` 400 — Investigation Recon

**Date:** 2026-06-24  
**Branch:** transval  
**Status:** Read-only investigation. No changes made.

---

## Summary

`GET /api/employees/my-operators` returns 400 when the calling user has no `employee_id` JWT claim. Admin and SuperUser accounts are not linked to employees, so they have no such claim. Multiple pages and components call this endpoint unconditionally — including admin-facing pages — which produces 400 noise in the Network tab on every load for those users.

The fix is a one-line backend change (return `200 []` instead of `400`) plus a small frontend guard in `ScheduleDialog`.

---

## Task 1 — The endpoint

**File:** [src/QuantumBuild.API/Controllers/EmployeesController.cs](../src/QuantumBuild.API/Controllers/EmployeesController.cs#L367-L381)

```
[HttpGet("my-operators")]
[Authorize(Policy = "Learnings.View")]
public async Task<IActionResult> GetMyOperators()
```

- **Route:** `GET /api/employees/my-operators`  
- **Auth policy:** `Learnings.View` — held by Operators, Supervisors, and (implicitly) Admins who have all permissions  
- **Returns:** `Result<List<SupervisorOperatorDto>>` envelope — frontend reads `response.data.data`  
- **400 branch:** line 372–373 — `GetCurrentEmployeeId()` parses the `employee_id` JWT claim; returns null when the claim is absent

---

## Task 2 — Frontend callers

`useMyOperators()` (hook in [web/src/lib/api/admin/use-supervisor-assignments.ts](../web/src/lib/api/admin/use-supervisor-assignments.ts#L74-L79)) is called from **six sites**:

| File | Surface | Role guard on the hook call? |
|---|---|---|
| [toolbox-talks/team/page.tsx](../web/src/app/(authenticated)/toolbox-talks/team/page.tsx#L25) | My Team page | No — redirect fires in `useEffect` *after* hooks run |
| [toolbox-talks/reports/page.tsx](../web/src/app/(authenticated)/toolbox-talks/reports/page.tsx#L35) | Team Reports landing | None |
| [toolbox-talks/reports/compliance/page.tsx](../web/src/app/(authenticated)/toolbox-talks/reports/compliance/page.tsx#L60) | Compliance report | None |
| [toolbox-talks/reports/overdue/page.tsx](../web/src/app/(authenticated)/toolbox-talks/reports/overdue/page.tsx#L48) | Overdue report | None |
| [toolbox-talks/reports/completions/page.tsx](../web/src/app/(authenticated)/toolbox-talks/reports/completions/page.tsx#L56) | Completions report | None |
| [features/toolbox-talks/components/ScheduleDialog.tsx](../web/src/features/toolbox-talks/components/ScheduleDialog.tsx#L130) | Schedule create/edit dialog | **None — called unconditionally; result only used when `isSupervisorOnly`** |

The `/toolbox-talks/reports/*` and `/toolbox-talks/team` pages are reached by Supervisors via the employee-portal nav. `ScheduleDialog` is used by the **admin** schedule pages (`/admin/toolbox-talks/schedules/*`, `/admin/toolbox-talks/talks/[id]`) — reachable by Admin users who never have an `employee_id` claim.

The layout guard ([toolbox-talks/layout.tsx](../web/src/app/(authenticated)/toolbox-talks/layout.tsx#L47-L51)) redirects Admin users without an `employeeId` to `/admin/toolbox-talks`. That redirect is a `useEffect` — client-side, fires after initial render — so every hook in the page fires once before the redirect. The 400 from the reports pages originates in that first-render window.

---

## Task 3 — Why 400 is returned

`GetCurrentEmployeeId()` ([line 454–459](../src/QuantumBuild.API/Controllers/EmployeesController.cs#L454-L459)) reads the `employee_id` claim from the JWT. Admin and SuperUser accounts have no linked employee record, so no `employee_id` claim is minted for them at login. `GetCurrentEmployeeId()` returns null, and the action immediately returns `BadRequest`.

The primary offending call site is **`ScheduleDialog`**, which fires on every schedule-management page load for Admin users. The five reports pages are secondary — they fire during the layout redirect's first-render race.

---

## Task 4 — Intended semantics

"No linked employee" is a **valid state** for the calling user in all cases where the 400 occurs:

- An Admin on a schedule management page has `Learnings.View` (via "all permissions") and is a legitimate caller, but has no employee record. The question is semantically "how many operators are assigned to me?" — and the correct answer is zero, not an error.
- A Supervisor *will* have an `employee_id` claim and will get a real list. That is the primary intended audience.
- An Operator has an `employee_id` but no supervisor assignments, so `GetAssignedOperatorsAsync` returns an empty list — `200 []`. That path already works correctly.

There is no case where the absence of an `employee_id` claim should be treated as a malformed request. `400 Bad Request` is semantically wrong here.

---

## Task 5 — Recommended fix

### Primary fix — Backend (one line)

Change the null-employee guard to return `200 OK` with an empty list instead of `400 Bad Request`.

**File:** [src/QuantumBuild.API/Controllers/EmployeesController.cs](../src/QuantumBuild.API/Controllers/EmployeesController.cs#L371-L373)

```csharp
// Before
var employeeId = GetCurrentEmployeeId();
if (employeeId == null)
    return BadRequest(new { message = "Current user is not linked to an employee" });

// After
var employeeId = GetCurrentEmployeeId();
if (employeeId == null)
    return Ok(Result<List<SupervisorOperatorDto>>.Ok([]));
```

This is the correct semantic: "valid query, no relevant data → empty list." It eliminates the 400 for all six callers in one change and requires no frontend coordination.

### Secondary fix — Frontend guard in ScheduleDialog

`ScheduleDialog` is the only caller that runs in a pure admin context where no redirect will eventually clean things up. Adding a `enabled` guard eliminates even the transient request.

**File:** [web/src/features/toolbox-talks/components/ScheduleDialog.tsx](../web/src/features/toolbox-talks/components/ScheduleDialog.tsx#L130)

```ts
// Before
const { data: myOperators, isLoading: myOperatorsLoading } = useMyOperators();

// After
const { data: myOperators, isLoading: myOperatorsLoading } = useMyOperators({ enabled: isSupervisorOnly });
```

This requires `useMyOperators` to accept and forward a `UseQueryOptions`-style `enabled` parameter — a small hook change.

**Recommended approach:** do both. The backend fix is the safety net (correct for all present and future callers); the frontend guard is the performance-correct behaviour (don't make a call whose result will be discarded).

---

## Files in scope for the fix

| File | Change |
|---|---|
| `src/QuantumBuild.API/Controllers/EmployeesController.cs` | Line 373: `BadRequest` → `Ok(Result<List<...>>.Ok([]))` |
| `web/src/lib/api/admin/use-supervisor-assignments.ts` | `useMyOperators` — add optional `enabled` param |
| `web/src/features/toolbox-talks/components/ScheduleDialog.tsx` | Pass `enabled: isSupervisorOnly` to `useMyOperators` |

No migration, no schema change, no new entities.
