# Staff Department - Reconciliation Queries (manual step)

This document is the deliverable for the manual reconciliation step that follows
Department formalisation chunk 1. **None of these queries have been run as part of
this chunk**: they are provided for a human (with direct DB access) to run when
ready to define the canonical department list per tenant and assign employees to it.

Chunk 1 deliberately did **not**:
- auto-create `Department` rows from existing free-text values,
- auto-assign any employee's new `DepartmentId`,
- touch or drop the legacy free-text `Employees.Department` column.

All existing employees currently have `DepartmentId = null`. The free-text
`Department` column is untouched and still holds whatever value each employee had
before this chunk, that free text is the raw material for defining the canonical
list below.

---

## Step 1: See what free-text values exist today

Distinct existing free-text `Employee.Department` values per tenant, with employee
counts, including an explicit null/empty bucket:

```sql
SELECT
    "TenantId",
    COALESCE(NULLIF(TRIM("Department"), ''), '(none)') AS "DepartmentValue",
    COUNT(*) AS "EmployeeCount"
FROM "Employees"
WHERE "IsDeleted" = false
GROUP BY "TenantId", COALESCE(NULLIF(TRIM("Department"), ''), '(none)')
ORDER BY "TenantId", "EmployeeCount" DESC;
```

Per-tenant summary, how many employees have no department at all, and how many
distinct free-text values exist (use this to gauge how much manual normalisation a
tenant needs, e.g. 'Ops' vs 'Operations' vs 'operations'):

```sql
SELECT
    "TenantId",
    COUNT(*) AS "TotalEmployees",
    COUNT(*) FILTER (WHERE "Department" IS NULL OR TRIM("Department") = '') AS "NoDepartmentCount",
    COUNT(DISTINCT NULLIF(TRIM("Department"), '')) AS "DistinctDepartmentValues"
FROM "Employees"
WHERE "IsDeleted" = false
GROUP BY "TenantId"
ORDER BY "TenantId";
```

## Step 2: Cross-check against the (now-retired) Lookup-system values

Before this chunk, two tenant Employee forms offered a `LookupField` combobox for
category `Department` (custom values only, `AllowCustom = true`, no seeded
defaults). Those `TenantLookupValue` rows are disconnected from `Employee.Department`
free text (they were suggestions only, never enforced), but any tenant-authored
values are still a useful second input for defining the canonical list:

```sql
SELECT
    tlv."TenantId",
    tlv."Name" AS "LookupDepartmentName",
    tlv."IsEnabled"
FROM "TenantLookupValues" tlv
JOIN "LookupCategories" lc ON lc."Id" = tlv."CategoryId"
WHERE lc."Name" = 'Department' AND tlv."IsDeleted" = false
ORDER BY tlv."TenantId", tlv."Name";
```

This query is the 'harvest' of the old combobox's values. Its wiring has been
removed from the three employee forms (chunk 1, change 5), but the `LookupCategory`
named `Department` and any `TenantLookupValue` rows under it are left in the
database untouched. This query is how to read them back if useful for step 3.

## Step 3: What a human does with steps 1-2 (manual, not automated by this chunk)

1. For each tenant, look at the Step 1 output and decide a canonical department list
   (merge near-duplicate free-text spellings, decide which '(none)' employees should
   stay unassigned vs. get a real department).
2. Optionally cross-reference Step 2's Lookup values as a second signal for naming.
3. Create the canonical departments via `POST /api/departments` (tenant-scoped,
   requires `Core.ManageDepartments`), or `PUT /api/departments/{id}` to edit or
   deactivate afterwards.
4. Manually edit each employee (Employee edit form, Department select) to assign
   the correct `DepartmentId`. There is no bulk-assign tool in this chunk, and the
   employee list has no department filter yet either (that is chunk 2's scope, per
   the original chunk 1 instructions' NON-SCOPE section).
5. The legacy free-text `Employees.Department` column is left in place as a
   reference during this process. Dropping it is an explicit later follow-up, only
   after reconciliation is complete and confirmed.

## Known gap not covered by chunk 1 or this reconciliation step

Bulk employee import (CSV) still reads a free-text `department` column and writes
it straight to `CreateEmployeeDto.Department` (the legacy field); it has no
department-matching logic and does not populate `DepartmentId`. Employees created
via bulk import will need the same manual step-4 treatment above. Building
match-existing/create-new/reject logic for the bulk import path was out of scope
for chunk 1 (see CLAUDE.md backlog); note it here so it isn't missed when scoping
that follow-up.
