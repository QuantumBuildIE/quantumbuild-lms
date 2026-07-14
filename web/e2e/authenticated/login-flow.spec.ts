import { test, expect } from "@playwright/test";

// storageState is injected by the authenticated project in playwright.config.ts
// — auth.setup.ts runs first and saves SuperUser session to e2e/.auth/superuser.json

const SUPERUSER_EMAIL =
  process.env.SEED_SUPERUSER_EMAIL || "superuser@certifiediq.ai";

test("superuser reaches tenants page via saved auth state", async ({ page }) => {
  await page.goto("/admin/tenants");
  await expect(
    page.getByRole("heading", { name: "Tenants", level: 1 })
  ).toBeVisible();

  // Prove the session belongs to SuperUser specifically — a wrong-user auth bug
  // would land on the correct page but fail here.
  const token = await page.evaluate(() => localStorage.getItem("accessToken"));
  expect(token, "accessToken must be present in localStorage").not.toBeNull();

  const response = await page.request.get(
    "http://localhost:5222/api/auth/me",
    { headers: { Authorization: `Bearer ${token!}` } }
  );
  expect(response.ok()).toBe(true);

  const body = await response.json();
  expect(body.email).toBe(SUPERUSER_EMAIL);
  expect(body.isSuperUser).toBe(true);
});
