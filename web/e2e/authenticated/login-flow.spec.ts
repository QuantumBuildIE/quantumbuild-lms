import { test, expect } from "@playwright/test";

// storageState is injected by the authenticated project in playwright.config.ts
// — auth.setup.ts runs first and saves SuperUser session to e2e/.auth/superuser.json

test("superuser reaches tenants page via saved auth state", async ({ page }) => {
  await page.goto("/admin/tenants");
  await expect(
    page.getByRole("heading", { name: "Tenants", level: 1 })
  ).toBeVisible();
});
