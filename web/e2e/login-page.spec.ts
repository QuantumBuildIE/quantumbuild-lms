import { test, expect } from "@playwright/test";

test("login page renders", async ({ page }) => {
  await page.goto("/login");

  // Brand heading — confirms the page is the CertifiedIQ login screen, not a 404 or error
  await expect(page.getByText("CertifiedIQ").first()).toBeVisible();

  // Three core form controls
  await expect(page.getByLabel("Email")).toBeVisible();
  await expect(page.getByLabel("Password")).toBeVisible();
  await expect(page.getByRole("button", { name: "Sign In" })).toBeVisible();
});
