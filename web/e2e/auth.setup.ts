import { test as setup } from "@playwright/test";
import { mkdirSync } from "fs";
import path from "path";

const AUTH_DIR = path.join(__dirname, ".auth");
const AUTH_FILE = path.join(AUTH_DIR, "superuser.json");

const SUPERUSER_EMAIL =
  process.env.SEED_SUPERUSER_EMAIL || "superuser@certifiediq.ai";
const SUPERUSER_PASSWORD =
  process.env.SEED_SUPERUSER_PASSWORD || "SuperUser123!";

setup("authenticate as superuser", async ({ page }) => {
  mkdirSync(AUTH_DIR, { recursive: true });

  await page.goto("/login");

  await page.getByTestId("login-email-input").fill(SUPERUSER_EMAIL);
  await page.getByTestId("login-password-input").fill(SUPERUSER_PASSWORD);
  await page.getByTestId("login-submit-button").click();

  // SuperUser lands at /admin/tenants (getHomeRoute returns /admin/tenants for isSuperUser)
  await page.waitForURL("**/admin/tenants", { timeout: 15_000 });

  await page.context().storageState({ path: AUTH_FILE });
});
