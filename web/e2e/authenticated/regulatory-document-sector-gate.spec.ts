import { test, expect } from "@playwright/test";

// storageState is injected by the authenticated project in playwright.config.ts
// — auth.setup.ts runs first and saves SuperUser session to e2e/.auth/superuser.json

// Verifies Chunk A: attaching a sector and ingesting are one coherent flow, and a
// document cannot be ingested without an actually-attached RegulatoryProfile. Creates
// a fresh document (no profile) so the "nothing attached yet" state is genuinely exercised.
test("ingest is gated on an attached sector, not on picker selection", async ({
  page,
}) => {
  await page.goto("/admin/regulatory/system");
  await expect(
    page.getByRole("heading", { name: "Regulatory Documents" })
  ).toBeVisible();

  await page.getByRole("button", { name: "Add Document" }).click();

  const dialog = page.getByRole("dialog");
  await dialog.getByRole("combobox", { name: /Regulatory Body/ }).click();
  await page.getByRole("option").first().click();

  const uniqueTitle = `E2E Sector Gate Test ${Date.now()}`;
  await dialog.getByLabel("Title *").fill(uniqueTitle);
  await dialog.getByLabel("Version *").fill("1.0");
  await dialog.getByRole("button", { name: "Create Regulation" }).click();

  // Dialog's onCreated navigates to the new document's detail page.
  await expect(page.getByRole("heading", { name: uniqueTitle })).toBeVisible();

  // Satisfy the pre-existing, unrelated Source URL gate up front so this test
  // isolates the sector-attachment gate under test.
  await page
    .getByPlaceholder("https://example.com/document.pdf")
    .fill("https://example.com/e2e-sector-gate-test.pdf");

  const ingestButton = page.getByRole("button", { name: "Ingest Requirements" });
  const attachButton = page.getByRole("button", { name: "Attach sector" });

  // No sector attached yet: Ingest is disabled and the reason is shown.
  await expect(ingestButton).toBeDisabled();
  await expect(page.getByText("Attach a sector before ingesting")).toBeVisible();
  await expect(page.getByText("No sectors attached yet.")).toBeVisible();

  // There is no longer a standalone "Sectors" card — the section lives inside
  // the ingestion panel under the "Sector" label.
  await expect(
    page.getByRole("heading", { name: "Sectors", exact: true })
  ).toHaveCount(0);

  // Selecting a sector in the picker alone must NOT enable Ingest.
  // The sector Select has no associated <label> (unlike the dialog's Regulatory
  // Body field), so its accessible name isn't reliably exposed — target the
  // trigger by its visible placeholder text instead of an ARIA combobox role/name.
  await page.getByText("Select a sector to attach").click();
  await page.getByRole("option").first().click();
  await expect(ingestButton).toBeDisabled();

  // Attaching fires the profile POST; the badge list updates and Ingest unlocks.
  await attachButton.click();
  await expect(page.getByText("Sector attached")).toBeVisible();
  await expect(page.getByText("No sectors attached yet.")).toHaveCount(0);
  await expect(page.getByText("Attach a sector before ingesting")).toHaveCount(0);
  await expect(ingestButton).toBeEnabled();
});
