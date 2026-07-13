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
 *
 * Diagnostics: this test logs step boundaries (▶/✓/⏳/❌ prefixes), every
 * request/response to /api/* and to the Cloudflare R2 upload host, and
 * browser console errors/warnings + uncaught page errors. This is deliberate
 * — a prior run hung for 15 minutes at the Step 1 `waitForResponse` with zero
 * diagnostic output, because that call had no explicit timeout and the
 * network listeners needed to see *why* it hung didn't exist yet. See the
 * end-of-chunk report for the leading hypothesis (the real-network Cloudflare
 * R2 upload PUT, which the manual-run doc explicitly never exercised — that
 * session's sandbox had no route to r2.cloudflarestorage.com).
 */

import { test, expect, type Page } from '@playwright/test';
import path from 'path';

// Timeout values below are set to the manual run's observed range plus a
// buffer for provider variance — not aspirational round numbers. If a step
// times out in practice, treat that as a real signal, not a config problem.
const TIMEOUTS = {
  // 20s -> 30s: the manual-run doc's "15s" and this file's original "20s"
  // both came from Text-mode timing (POST /initialise only). The real Pdf
  // path additionally does a full client-side upload (getPresignedUrl POST +
  // a real R2 PUT) inside onSubmit *before* /initialise ever fires — see
  // InputConfigStep.tsx:304-314 and useUploadSourceFile.ts. That upload was
  // never exercised in the manual-run session (no R2 network route in that
  // sandbox), so its real latency is unconfirmed. 30s buys margin for that
  // extra network round trip without masking a genuine hang for long.
  submitStep1: 30_000,
  parse: 60_000, // observed 12.3s-20.6s, synchronous for PDF/Text mode
  quizGenerate: 90_000, // observed 22.0s-26.2s, synchronous (confirmed non-Processing for PDF/Text)
  settingsSubmit: 15_000, // observed ~1.5s
  translateValidated: 8 * 60_000, // observed 3m12s for one language; do not add more languages (7x+ slower, unconfirmed completion)
  sectionAccept: 10_000, // observed 0.56s-0.57s (synchronous DB write)
  publish: 15_000, // single request/response
};

// ── Diagnostics ──────────────────────────────────────────────────────────
// Local logging helpers — kept inline in this one spec file, not extracted
// to a shared module (out of scope for this chunk).
const step = (msg: string) => console.log(`▶ ${msg}`);
const ok = (msg: string) => console.log(`✓ ${msg}`);
const waiting = (msg: string) => console.log(`⏳ ${msg}`);
const logUrl = (page: Page) => console.log('URL:', page.url());

// Include: page navigations, anything under /api/, and the Cloudflare R2
// upload host (public *.r2.dev and the presigned-PUT *.r2.cloudflarestorage.com
// host — see CLAUDE.md's R2 config block). Excludes _next/ assets, fonts,
// images, etc. by simply not matching any of the above.
function isSignalRequest(url: URL, resourceType: string): boolean {
  if (resourceType === 'document') return true;
  if (url.pathname.startsWith('/api/')) return true;
  if (/\.r2\.(cloudflarestorage\.com|dev)$/i.test(url.hostname)) return true;
  return false;
}

function attachDiagnostics(page: Page) {
  page.on('request', (req) => {
    let url: URL;
    try {
      url = new URL(req.url());
    } catch {
      return;
    }
    if (!isSignalRequest(url, req.resourceType())) return;
    console.log('→', req.method(), url.hostname + url.pathname);
  });

  page.on('response', async (res) => {
    let url: URL;
    try {
      url = new URL(res.url());
    } catch {
      return;
    }
    if (!isSignalRequest(url, res.request().resourceType())) return;
    console.log('←', res.status(), url.hostname + url.pathname);

    if (res.status() >= 400) {
      try {
        const body = await res.text();
        console.error(
          `❌ [${res.status()}] ${url.hostname}${url.pathname} response body:`,
          body.slice(0, 2000)
        );
      } catch (err) {
        console.error(
          `❌ [${res.status()}] ${url.hostname}${url.pathname} — could not read response body:`,
          err instanceof Error ? err.message : String(err)
        );
      }
    }
  });

  page.on('pageerror', (error) => {
    console.error('❌ Client error:', error.message);
  });

  page.on('console', (msg) => {
    if (msg.type() === 'error' || msg.type() === 'warning') {
      console.log(`[browser ${msg.type()}]`, msg.text());
    }
  });
}

test.describe('Learning wizard — PDF happy path', () => {
  test('admin can create, translate, validate, and publish a PDF-based learning via the new wizard', async ({
    page,
  }) => {
    test.setTimeout(15 * 60 * 1000);

    attachDiagnostics(page);

    const ts = Date.now();
    const title = `E2E PDF Learning ${ts}`;

    await test.step('Setup — select an active tenant (SuperUser prerequisite)', async () => {
      // AdminLayout redirects any tenant-scoped page — including every wizard
      // URL — back to /admin/tenants when a SuperUser has no active tenant
      // selected. This isn't documented in the recon; it was only discovered
      // during the manual run. Select the first real tenant via the
      // TenantSwitcher in the top nav before navigating anywhere else.
      step('Navigating to /admin/tenants to select an active tenant');
      await page.goto('/admin/tenants');
      await page.locator('header').getByRole('combobox').click();
      await page
        .getByRole('option')
        .filter({ hasNotText: 'All Tenants' })
        .first()
        .click();
      ok('Active tenant selected');
    });

    await test.step('Navigate to wizard Step 1 — Input & Config', async () => {
      // ?wizard=new is a one-shot URL override (CLAUDE.md Note 29) — avoids
      // depending on the tenant's UseNewWizard setting.
      step('Navigating to wizard Step 1 (?wizard=new)');
      await page.goto('/admin/toolbox-talks/learnings/new?wizard=new');

      // The form's aria-label is unambiguous to Step 1 specifically (the
      // page's <h1> reads "New Learning" on every step, so it can't be used
      // to distinguish Step 1 from the others).
      await expect(
        page.getByRole('form', {
          name: 'Learning wizard step 1 — input and configuration',
        })
      ).toBeVisible();
      ok('Step 1 form visible');
      logUrl(page);
    });

    let talkId = '';

    await test.step('Step 1 — Input & Config', async () => {
      step('Filling Title');
      await page.getByRole('textbox', { name: 'Title' }).fill(title);

      // Pdf input mode is labelled "Document" in the UI. Its accessible name is
      // the button's label + description text concatenated ("Document Upload a
      // PDF document" — see INPUT_MODE_OPTIONS in InputConfigStep.tsx), and
      // Playwright's default name match is substring-based, so a bare
      // { name: 'Document' } also matches the Docx mode button ("Word Document
      // Upload a Word document (.docx)") since "Document" is a substring of
      // "Word Document...". Anchor to the start of the name so only the Pdf
      // mode button (whose name *starts* with "Document") matches.
      step('Selecting input mode: Document (Pdf)');
      await page.getByRole('button', { name: /^Document\b/ }).click();

      const fixturePath = path.join(
        __dirname,
        '../fixtures/sample-toolbox-talk.pdf'
      );
      step(`Selecting PDF file: ${fixturePath}`);
      await page.getByLabel('Select PDF file').setInputFiles(fixturePath);
      ok('PDF file attached (client-side — not yet uploaded to R2; that happens on submit)');

      // Target languages: the trigger button carries aria-label="Target
      // languages" directly (not via FormLabel's htmlFor, which can't reach
      // the nested Popover/Button — see multi-select-combobox.tsx's own
      // comment). getByRole with that name is the reliable selector;
      // getByLabel does not find it (confirmed in the manual run).
      step('Opening Target languages combobox');
      await page
        .getByRole('combobox', { name: 'Target languages' })
        .click();

      // Clear any employee-language auto-derivation first so exactly one
      // language is selected explicitly, regardless of which languages (if
      // any) the active tenant's seeded employees happen to speak — the
      // task requires a manual, not auto-derived, selection.
      const clearAllButton = page.getByRole('button', { name: /^Clear all/ });
      if ((await clearAllButton.count()) > 0) {
        step('Clearing auto-derived target languages');
        await clearAllButton.click();
      }
      await page.getByRole('option').first().click();
      await page.keyboard.press('Escape'); // close the popover
      ok('Target language selected');

      // Audit Purpose shares a defect with Sector: Radix's
      // <button role="combobox"> SelectTrigger has no accessible name
      // (InputConfigStep.tsx — FormControl's id/aria-describedby target the
      // non-DOM Radix Select.Root and never reach the trigger button; same
      // gap already documented for MultiSelectCombobox in the recon doc).
      // A name-based getByRole('combobox', { name: ... }) query therefore
      // always resolves to zero elements. Scope by the surrounding
      // form-item container instead of the trigger's own (nonexistent)
      // accessible name. Confirmed against InputConfigStep.tsx: no other
      // field's FormLabel text equals "Audit purpose", so the container
      // match is unambiguous.
      const groupByLabel = (labelPattern: string | RegExp) =>
        page.locator('div[data-slot="form-item"]', {
          has: page.getByText(labelPattern),
        });

      // Sector is optional in both inputConfigSchema.ts and
      // InitialiseToolboxTalkCommandValidator.cs, and its DOM automation
      // (three rendering branches depending on tenant sector count) proved
      // too brittle for repeated runs — deferred to BACKLOG. This test's
      // real value is downstream (Parse, Quiz, Translate, Validate,
      // Publish), so Sector selection is skipped entirely.
      console.log('▶ Skipping Sector (optional, DOM automation deferred to BACKLOG)');

      // Audit purpose — also NOT required by validation (same schema file,
      // `auditPurpose: z.string().max(500).optional()`). Always rendered as
      // a preset Select at form-init (no auto-populate effect, no
      // alternate/static state like Sector), so the group and its combobox
      // should always be present.
      step('Selecting Audit Purpose');
      const auditGroup = groupByLabel('Audit purpose');
      const auditTrigger = auditGroup.getByRole('combobox');
      await auditTrigger.click();
      await expect(auditTrigger).toHaveAttribute('aria-expanded', 'true');
      await page.getByRole('option', { name: 'Regulatory Compliance' }).click();
      ok('Audit Purpose selected');

      // Reviewer Name / Role are pre-populated from the JWT user profile
      // (InputConfigStep.tsx:178-188) and are optional in validation — left
      // untouched per the chunk brief. Client and Document reference are
      // also optional in validation (confirmed: Client has no
      // company-existence-based requirement in the schema either way) — left
      // untouched; Client's SelectTrigger has the same missing-accessible-
      // name gap as Sector if a future chunk wants to fill it
      // (getByRole('combobox', { name: /select client/i })).

      const initialisePromise = page.waitForResponse(
        (resp) =>
          resp.url().includes('/initialise') &&
          resp.request().method() === 'POST',
        { timeout: TIMEOUTS.submitStep1 }
      );
      step('Clicking Continue (submits Step 1 — may first upload the PDF to R2)');
      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      waiting(`waiting for POST .../initialise (timeout ${TIMEOUTS.submitStep1}ms)`);
      await initialisePromise;
      ok('POST /initialise resolved');

      await page.waitForURL(/\/admin\/toolbox-talks\/learnings\/[^/]+\/parse$/, {
        timeout: TIMEOUTS.submitStep1,
      });
      logUrl(page);

      const match = new URL(page.url()).pathname.match(
        /\/learnings\/([^/]+)\/parse$/
      );
      talkId = match![1];
      expect(talkId).toBeTruthy();
      ok(`Talk created: ${talkId}`);
    });

    await test.step('Step 2 — Parse (synchronous for PDF mode)', async () => {
      // Section cards each expose a stable, per-section accessible name via
      // their title-edit button ("Edit section N title: ..."). Positive
      // "sections rendered" signal, not "spinner gone" — PDF parse is
      // synchronous and the spinner window may be too narrow to catch.
      const sectionTitleButtons = page.getByRole('button', {
        name: /^Edit section \d+ title:/,
      });
      waiting(`waiting for parsed sections (timeout ${TIMEOUTS.parse}ms)`);
      await expect(sectionTitleButtons.first()).toBeVisible({
        timeout: TIMEOUTS.parse,
      });
      const sectionCount = await sectionTitleButtons.count();
      ok(`Parse complete — ${sectionCount} sections found`);
      expect(sectionCount).toBeGreaterThanOrEqual(2);

      step('Clicking Save & Continue');
      await page.getByRole('button', { name: 'Save & Continue' }).click();
      await page.waitForURL(/\/quiz$/, { timeout: TIMEOUTS.submitStep1 });
      logUrl(page);
    });

    await test.step('Step 3 — Quiz', async () => {
      // "Edit question" is repeated identically per QuestionCard (not
      // indexed) — its count equals the number of generated questions.
      const editQuestionButtons = page.getByRole('button', {
        name: 'Edit question',
      });
      waiting(`waiting for generated quiz questions (timeout ${TIMEOUTS.quizGenerate}ms)`);
      await expect(editQuestionButtons.first()).toBeVisible({
        timeout: TIMEOUTS.quizGenerate,
      });
      const questionCount = await editQuestionButtons.count();
      ok(`Quiz generation complete — ${questionCount} questions found`);
      expect(questionCount).toBeGreaterThan(0);

      step('Clicking Save & Continue');
      await page.getByRole('button', { name: 'Save & Continue' }).click();
      await page.waitForURL(/\/settings$/, { timeout: TIMEOUTS.submitStep1 });
      logUrl(page);
    });

    await test.step('Step 4 — Settings (accept defaults)', async () => {
      step('Clicking Continue (accepting default settings)');
      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      waiting(`waiting for navigation to /translate (timeout ${TIMEOUTS.settingsSubmit}ms)`);
      await page.waitForURL(/\/translate$/, {
        timeout: TIMEOUTS.settingsSubmit,
      });
      ok('Settings saved');
      logUrl(page);
    });

    await test.step('Step 5 — Translate', async () => {
      step('Clicking Start All');
      await page.getByRole('button', { name: 'Start All' }).click();

      // Single target language selected in Step 1, so a page-wide search for
      // the "Validated" state badge is unambiguous. Must use
      // expect().toBeVisible({ timeout }), which polls — NOT
      // locator.isVisible({ timeout }), which does a single immediate check
      // and does not wait. This exact mistake cost 25+ minutes in the manual
      // run session.
      waiting(
        `waiting for target language to reach "Validated" (timeout ${TIMEOUTS.translateValidated}ms — this is a real multi-round back-translation consensus run, not a fixed wait)`
      );
      await expect(page.getByText('Validated', { exact: true })).toBeVisible({
        timeout: TIMEOUTS.translateValidated,
      });
      ok('Target language: Validated');

      step('Clicking Continue');
      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      await page.waitForURL(/\/validate$/, { timeout: TIMEOUTS.submitStep1 });
      logUrl(page);
    });

    await test.step('Step 6 — Validate', async () => {
      // Section header buttons carry a stable "L01", "L02", ... prefix (the
      // sectionLabel Badge is the first text node in the header's accessible
      // name). Their `disabled` state tracks `!result` exactly, so waiting
      // for all of them to be enabled is a genuine "every section has a
      // validation result" signal.
      const sectionHeaders = page.getByRole('button', { name: /^L\d{2}/ });
      waiting('waiting for validation section headers to appear');
      await expect(sectionHeaders.first()).toBeVisible({
        timeout: TIMEOUTS.sectionAccept,
      });
      const sectionCount = await sectionHeaders.count();
      ok(`${sectionCount} validation section headers found`);
      expect(sectionCount).toBeGreaterThanOrEqual(2);
      for (let i = 0; i < sectionCount; i++) {
        await expect(sectionHeaders.nth(i)).toBeEnabled({
          timeout: TIMEOUTS.sectionAccept,
        });
      }
      ok('All sections have a validation result (headers enabled)');

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
        step(`Accepting section ${i + 1}/${sectionCount}`);
        await header.click();
        await card.getByRole('button', { name: 'Accept' }).click();
        await expect(card.getByText('Accepted', { exact: true })).toBeVisible({
          timeout: TIMEOUTS.sectionAccept,
        });
        ok(`Section ${i + 1}/${sectionCount} accepted`);
      }

      await expect(page.getByText('Ready to publish')).toBeVisible({
        timeout: TIMEOUTS.sectionAccept,
      });
      ok('Ready to publish');

      step('Clicking Continue');
      await page.getByRole('button', { name: 'Continue', exact: true }).click();
      await page.waitForURL(/\/publish$/, { timeout: TIMEOUTS.submitStep1 });
      logUrl(page);
    });

    await test.step('Step 7 — Publish', async () => {
      step('Clicking Publish');
      await page.getByRole('button', { name: 'Publish', exact: true }).click();

      // Success signal, per established codebase convention: URL navigation
      // to the talk detail page, not a toast (toast.success() and
      // router.push() fire in the same tick — the page may unmount before
      // Playwright observes the toast).
      waiting(`waiting for navigation to talk detail page (timeout ${TIMEOUTS.publish}ms)`);
      await page.waitForURL(/\/admin\/toolbox-talks\/talks\/[^/]+$/, {
        timeout: TIMEOUTS.publish,
      });
      logUrl(page);
      expect(page.url()).toContain(talkId);
      ok('Navigated to talk detail page');

      // The detail page does not render the talk's Draft/Processing/
      // ReadyForReview/Published status as visible text anywhere (confirmed
      // by reading ToolboxTalkDetail.tsx — it only shows an isActive-derived
      // "Active"/"Inactive" badge, which is a different field). The honest
      // completion signal here is: we landed on the correct talk's detail
      // page (URL contains talkId) and its title rendered (proves the talk
      // loaded, not a 404/error state) — not a status badge that doesn't
      // exist in the UI.
      await expect(page.getByRole('heading', { name: title })).toBeVisible();
      ok(`Published — "${title}" heading visible on detail page`);
    });
  });
});
