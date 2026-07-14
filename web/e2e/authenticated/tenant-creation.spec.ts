import { test, expect } from "@playwright/test";

test.describe("Tenant creation", () => {
  test("SuperUser creates a tenant with contact details and sector", async ({
    page,
    request,
  }) => {
    const ts = Date.now();
    const tenantName = `E2E Tenant ${ts}`;
    const contactEmail = `e2e+${ts}@example.com`;
    const contactName = "E2E Admin";

    await page.goto("/admin/tenants/new");

    await page.getByLabel(/tenant name/i).fill(tenantName);
    await page.getByLabel(/contact name/i).fill(contactName);
    await page.getByLabel(/contact email/i).fill(contactEmail);

    // Sectors load asynchronously from GET /api/toolbox-talks/sectors.
    // shadcn Checkbox uses Radix UI and renders as <button role="checkbox">,
    // NOT <input type="checkbox"> — getByRole is the correct selector.
    const sectorCheckboxes = page.getByRole("checkbox");
    await expect(
      sectorCheckboxes.first(),
      "No sectors available — DataSeeder may not have run"
    ).toBeVisible();
    await sectorCheckboxes.first().click();

    // Set up response intercept BEFORE clicking so we can capture the new
    // tenant's ID from the POST /api/tenants response for the secondary assertion.
    const tenantPostPromise = page.waitForResponse(
      (resp) =>
        resp.url().includes("/api/tenants") &&
        resp.request().method() === "POST" &&
        !resp.url().includes("/sectors")
    );

    // Note: success toast is not asserted — toast.success() and router.push() fire
    // in the same JS tick, so the page may unmount before Playwright sees the toast.
    // URL change is the reliable signal. Toast assertions remain valid for ERROR cases
    // (no navigation occurs on error).
    await page.getByRole("button", { name: "Create Tenant" }).click();

    const tenantResponse = await tenantPostPromise;
    const tenantBody = await tenantResponse.json();
    // Result<TenantDetail> envelope: { success, data: { id, name, ... }, errors }
    const tenantId = tenantBody.data.id;

    await page.waitForURL("**/admin/tenants");
    await expect(page.getByText(tenantName)).toBeVisible();

    // Verify the admin user was created via API.
    // SuperUser scopes GET /api/users to the new tenant via X-Tenant-Id header.
    // Without the header, CurrentUserService returns TenantId = Guid.Empty and
    // UserService.GetPaginatedAsync returns no users (no bypass in Identity path).
    // Auth uses JWT Bearer stored in localStorage (not cookies), so the `request`
    // fixture does not carry it automatically — extract and pass explicitly.
    const token = await page.evaluate(() =>
      localStorage.getItem("accessToken")
    );
    const usersResponse = await request.get("http://localhost:5222/api/users", {
      params: { search: contactEmail },
      headers: {
        Authorization: `Bearer ${token!}`,
        "X-Tenant-Id": tenantId,
      },
    });

    expect(usersResponse.ok(), "GET /api/users must return 200").toBeTruthy();
    const usersBody = await usersResponse.json();
    // Result<PaginatedList<UserDto>> envelope: { success, data: { items, ... }, errors }
    expect(
      usersBody.data.items.length,
      `Expected admin user ${contactEmail} to exist in tenant ${tenantId}`
    ).toBeGreaterThan(0);
    expect(usersBody.data.items[0].email).toBe(contactEmail);
  });

  test("SuperUser creates a tenant without contact details", async ({
    page,
  }) => {
    const ts = Date.now();
    const tenantName = `E2E Tenant ${ts}`;

    await page.goto("/admin/tenants/new");

    await page.getByLabel(/tenant name/i).fill(tenantName);

    const sectorCheckboxes = page.getByRole("checkbox");
    await expect(
      sectorCheckboxes.first(),
      "No sectors available — DataSeeder may not have run"
    ).toBeVisible();
    await sectorCheckboxes.first().click();

    // Note: success toast not asserted — see first test for explanation.
    await page.getByRole("button", { name: "Create Tenant" }).click();
    await page.waitForURL("**/admin/tenants");

    await expect(page.getByText(tenantName)).toBeVisible();
  });

  test("Form rejects submission with missing required name", async ({
    page,
  }) => {
    await page.goto("/admin/tenants/new");

    // Wait for the form to fully render before attempting submission.
    await expect(page.getByLabel(/tenant name/i)).toBeVisible();

    await page.getByRole("button", { name: "Create Tenant" }).click();

    // No navigation should occur — must still be on the create page.
    await expect(page).toHaveURL(/\/admin\/tenants\/new/);

    // FormControl (shadcn/ui) spreads aria-invalid={!!error} via Radix Slot onto
    // the child Input, which passes it through to the native <input> element.
    // React renders aria-invalid={true} as aria-invalid="true" on the DOM node.
    const nameInput = page.getByLabel(/tenant name/i);
    await expect(nameInput).toHaveAttribute("aria-invalid", "true");
  });

  // TODO: missing-sector validation test — sector array validation fires via
  // form.setError("sectorIds") inside onSubmit (not Zod schema), and the sector
  // block only renders when availableSectors.length > 0. Confirm a reliable
  // Playwright assertion path before implementing.
  // See docs/playwright-tenant-creation-recon.md Q1 + Risk 1.
});
