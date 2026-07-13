/**
 * PDF-based Learning Creation Wizard — happy path (new wizard, `?wizard=new`).
 *
 * ON-DEMAND ONLY — not wired into CI. This test drives the real backend
 * pipeline end to end: Claude (parse/quiz/translate), DeepL/Gemini/Claude
 * back-translation consensus, and a real Cloudflare R2 file upload. There is
 * no mocking layer, so every run costs real money (roughly $2-5 per run,
 * dominated by the multi-round back-translation consensus in Steps 5-6) and
 * takes real wall-clock time (~5-7 minutes with a single target language,
 * per docs/playwright-wizard-manual-run-observations.md).
 *
 * Run with: npm run e2e -- learning-wizard-pdf.spec.ts
 * (requires PostgreSQL running; the API and Next.js dev server are
 * auto-spawned by playwright.config.ts if not already running)
 *
 * Background reading:
 * - docs/playwright-wizard-e2e-recon.md — infra, wizard step signals, selector strategy
 * - docs/playwright-wizard-manual-run-observations.md — real timing, gotchas
 *   (isVisible vs expect().toBeVisible, listitem/button collisions, the
 *   SuperUser active-tenant prerequisite below)
 */

import { test, expect } from '@playwright/test';
import path from 'path';

// Timeout values below are set to the manual run's observed range plus a
// buffer for provider variance — not aspirational round numbers. If a step
// times out in practice, treat that as a real signal, not a config problem.
const TIMEOUTS = {
  submitStep1: 20_000, // observed 0.36s-2.2s (text mode) + small PDF upload latency
  parse: 60_000, // observed 12.3s-20.6s, synchronous for PDF/Text mode
  quizGenerate: 90_000, // observed 22.0s-26.2s, synchronous (confirmed non-Processing for PDF/Text)
  settingsSubmit: 15_000, // observed ~1.5s
  translateValidated: 8 * 60_000, // observed 3m12s for one language; do not add more languages (7x+ slower, unconfirmed completion)
  sectionAccept: 10_000, // observed 0.56s-0.57s (synchronous DB write)
  publish: 15_000, // single request/response
};

test.describe('Learning wizard — PDF happy path', () => {
  test('admin can create, translate, validate, and publish a PDF-based learning via the new wizard', async ({
    page,
  }) => {
    test.setTimeout(15 * 60 * 1000);

    const ts = Date.now();
    const title = `E2E PDF Learning ${ts}`;

    await test.step('Setup — select an active tenant (SuperUser prerequisite)', async () => {
      // AdminLayout redirects any tenant-scoped page — including every wizard
      // URL — back to /admin/tenants when a SuperUser has no active tenant
      // selected. This isn't documented in the recon; it was only discovered
      // during the manual run. Select the first real tenant via the
      // TenantSwitcher in the top nav before navigating anywhere else.
      await page.goto('/admin/tenants');
      await page.locator('header').getByRole('combobox').click();
      await page
        .getByRole('option')
        .filter({ hasNotText: 'All Tenants' })
        .first()
        .click();
    });

    await test.step('Navigate to wizard Step 1 — Input & Config', async () => {
      // ?wizard=new is a one-shot URL override (CLAUDE.md Note 29) — avoids
      // depending on the tenant's UseNewWizard setting.
      await page.goto('/admin/toolbox-talks/learnings/new?wizard=new');

      // The form's aria-label is unambiguous to Step 1 specifically (the
      // page's <h1> reads "New Learning" on every step, so it can't be used
      // to distinguish Step 1 from the others).
      await expect(
        page.getByRole('form', {
          name: 'Learning wizard step 1 — input and configuration',
        })
      ).toBeVisible();
    });

    let talkId = '';

    await test.step('Step 1 — Input & Config', async () => {
      await page.getByRole('textbox', { name: 'Title' }).fill(title);

      // Pdf input mode is labelled "Document" in the UI. Its accessible name is
      // the button's label + description text concatenated ("Document Upload a
      // PDF document" — see INPUT_MODE_OPTIONS in InputConfigStep.tsx), and
      // Playwright's default name match is substring-based, so a bare
      // { name: 'Document' } also matches the Docx mode button ("Word Document
      // Upload a Word document (.docx)") since "Document" is a substring of
      // "Word Document...". Anchor to the start of the name so only the Pdf
      // mode button (whose name *starts* with "Document") matches.
      await page.getByRole('button', { name: /^Document\b/ }).click();

      const fixturePath = path.join(
        __dirname,
        '../fixtures/sample-toolbox-talk.pdf'
      );
      await page.getByLabel('Select PDF file').setInputFiles(fixturePath);

      // Target languages: the trigger button carries aria-label="Target
      // languages" directly (not via FormLabel's htmlFor, which can't reach
      // the nested Popover/Button — see multi-select-combobox.tsx's own
      // comment). getByRole with that name is the reliable selector;
      // getByLabel does not find it (confirmed in the manual run).
      await page
        .getByRole('combobox', { name: 'Target languages' })
        .click();

      // Clear any employee-language auto-derivation first so exactly one
      // language is selected explicitly, regardless of which languages (if
      // any) the active tenant's seeded employees happen to speak — the
      // task requires a manual, not auto-derived, selection.
      const clearAllButton = page.getByRole('button', { name: /^Clear all/ });
      if ((await clearAllButton.count()) > 0) {
        await clearAllButton.click();
      }
      await page.getByRole('option').first().click();
      await page.keyboard.press('Escape'); // close the popover

      const initialisePromise = page.waitForResponse(
        (resp) =>
          resp.url().includes('/initialise') &&
          resp.request().method() === 'POST'
      );
      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      await initialisePromise;

      await page.waitForURL(/\/admin\/toolbox-talks\/learnings\/[^/]+\/parse$/, {
        timeout: TIMEOUTS.submitStep1,
      });

      const match = new URL(page.url()).pathname.match(
        /\/learnings\/([^/]+)\/parse$/
      );
      talkId = match![1];
      expect(talkId).toBeTruthy();
    });

    await test.step('Step 2 — Parse (synchronous for PDF mode)', async () => {
      // Section cards each expose a stable, per-section accessible name via
      // their title-edit button ("Edit section N title: ..."). Positive
      // "sections rendered" signal, not "spinner gone" — PDF parse is
      // synchronous and the spinner window may be too narrow to catch.
      const sectionTitleButtons = page.getByRole('button', {
        name: /^Edit section \d+ title:/,
      });
      await expect(sectionTitleButtons.first()).toBeVisible({
        timeout: TIMEOUTS.parse,
      });
      const sectionCount = await sectionTitleButtons.count();
      expect(sectionCount).toBeGreaterThanOrEqual(2);

      await page.getByRole('button', { name: 'Save & Continue' }).click();
      await page.waitForURL(/\/quiz$/, { timeout: TIMEOUTS.submitStep1 });
    });

    await test.step('Step 3 — Quiz', async () => {
      // "Edit question" is repeated identically per QuestionCard (not
      // indexed) — its count equals the number of generated questions.
      const editQuestionButtons = page.getByRole('button', {
        name: 'Edit question',
      });
      await expect(editQuestionButtons.first()).toBeVisible({
        timeout: TIMEOUTS.quizGenerate,
      });
      const questionCount = await editQuestionButtons.count();
      expect(questionCount).toBeGreaterThan(0);

      await page.getByRole('button', { name: 'Save & Continue' }).click();
      await page.waitForURL(/\/settings$/, { timeout: TIMEOUTS.submitStep1 });
    });

    await test.step('Step 4 — Settings (accept defaults)', async () => {
      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      await page.waitForURL(/\/translate$/, {
        timeout: TIMEOUTS.settingsSubmit,
      });
    });

    await test.step('Step 5 — Translate', async () => {
      await page.getByRole('button', { name: 'Start All' }).click();

      // Single target language selected in Step 1, so a page-wide search for
      // the "Validated" state badge is unambiguous. Must use
      // expect().toBeVisible({ timeout }), which polls — NOT
      // locator.isVisible({ timeout }), which does a single immediate check
      // and does not wait. This exact mistake cost 25+ minutes in the manual
      // run session.
      await expect(page.getByText('Validated', { exact: true })).toBeVisible({
        timeout: TIMEOUTS.translateValidated,
      });

      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      await page.waitForURL(/\/validate$/, { timeout: TIMEOUTS.submitStep1 });
    });

    await test.step('Step 6 — Validate', async () => {
      // Section header buttons carry a stable "L01", "L02", ... prefix (the
      // sectionLabel Badge is the first text node in the header's accessible
      // name). Their `disabled` state tracks `!result` exactly, so waiting
      // for all of them to be enabled is a genuine "every section has a
      // validation result" signal.
      const sectionHeaders = page.getByRole('button', { name: /^L\d{2}/ });
      await expect(sectionHeaders.first()).toBeVisible({
        timeout: TIMEOUTS.sectionAccept,
      });
      const sectionCount = await sectionHeaders.count();
      expect(sectionCount).toBeGreaterThanOrEqual(2);
      for (let i = 0; i < sectionCount; i++) {
        await expect(sectionHeaders.nth(i)).toBeEnabled({
          timeout: TIMEOUTS.sectionAccept,
        });
      }

      // Structural check: one outcome pill (Pass/Review/Fail) per section —
      // not asserting a specific mix, since that varies run to run.
      await expect(page.getByText(/^(Pass|Review|Fail)$/)).toHaveCount(
        sectionCount,
        { timeout: TIMEOUTS.sectionAccept }
      );

      // Accept every section explicitly (accepting a Pass section is a
      // harmless no-op decision-wise; this guarantees hasPendingDecisions
      // clears regardless of the outcome mix, without branching on outcome).
      // Each card must be expanded first — the Accept/Edit/Retry buttons
      // live in the collapsed-by-default body, not the header.
      for (let i = 0; i < sectionCount; i++) {
        const header = sectionHeaders.nth(i);
        const card = header.locator('xpath=..');
        await header.click();
        await card.getByRole('button', { name: 'Accept' }).click();
        await expect(card.getByText('Accepted', { exact: true })).toBeVisible({
          timeout: TIMEOUTS.sectionAccept,
        });
      }

      await expect(page.getByText('Ready to publish')).toBeVisible({
        timeout: TIMEOUTS.sectionAccept,
      });

      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      await page.waitForURL(/\/publish$/, { timeout: TIMEOUTS.submitStep1 });
    });

    await test.step('Step 7 — Publish', async () => {
      await page.getByRole('button', { name: 'Publish', exact: true }).click();

      // Success signal, per established codebase convention: URL navigation
      // to the talk detail page, not a toast (toast.success() and
      // router.push() fire in the same tick — the page may unmount before
      // Playwright observes the toast).
      await page.waitForURL(/\/admin\/toolbox-talks\/talks\/[^/]+$/, {
        timeout: TIMEOUTS.publish,
      });
      expect(page.url()).toContain(talkId);

      // The detail page does not render the talk's Draft/Processing/
      // ReadyForReview/Published status as visible text anywhere (confirmed
      // by reading ToolboxTalkDetail.tsx — it only shows an isActive-derived
      // "Active"/"Inactive" badge, which is a different field). The honest
      // completion signal here is: we landed on the correct talk's detail
      // page (URL contains talkId) and its title rendered (proves the talk
      // loaded, not a 404/error state) — not a status badge that doesn't
      // exist in the UI.
      await expect(page.getByRole('heading', { name: title })).toBeVisible();
    });
  });
});
