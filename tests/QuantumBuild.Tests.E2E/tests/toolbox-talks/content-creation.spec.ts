import { test, expect } from '../../fixtures/test-fixtures';
import { Page, BrowserContext } from '@playwright/test';
import {
  ToolboxTalkListPage,
  ContentCreationWizardPage,
} from '../../page-objects/toolbox-talks';
import { ROUTES, TIMEOUTS, generateTestData } from '../../fixtures/test-constants';
import * as fs from 'fs';
import * as path from 'path';

// ---------------------------------------------------------------------------
// Minimal valid PDF fixture — generated once before the suite runs
// ---------------------------------------------------------------------------

const FIXTURES_DIR = path.resolve(__dirname, '../../fixtures/files');
const SAMPLE_PDF_PATH = path.join(FIXTURES_DIR, 'sample.pdf');

/**
 * Builds a minimal single-page PDF with safety-related text content.
 * This is a raw PDF 1.4 file — no external library needed.
 */
function buildMinimalPdf(): Buffer {
  const content = [
    'Workplace Safety Procedures',
    '',
    '1. Personal Protective Equipment (PPE)',
    'All workers must wear appropriate PPE at all times including hard hats,',
    'high-visibility vests, steel-toe boots, and safety glasses.',
    '',
    '2. Emergency Procedures',
    'In the event of an emergency, all personnel must evacuate via the nearest',
    'marked exit. Assembly points are clearly signposted on site.',
    '',
    '3. Hazard Reporting',
    'Report any hazards immediately to your supervisor. Do not attempt to fix',
    'electrical or structural hazards yourself.',
    '',
    '4. Manual Handling',
    'Always bend at the knees when lifting heavy objects. Never lift more than',
    '25kg without mechanical assistance or a second person.',
  ].join('\n');

  const streamContent = `BT\n/F1 12 Tf\n50 750 Td\n(${content.replace(/\(/g, '\\(').replace(/\)/g, '\\)').replace(/\n/g, ') Tj\n0 -14 Td\n(')}') Tj\nET`;

  const objects: string[] = [];

  // Object 1 — Catalog
  objects.push('1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj');
  // Object 2 — Pages
  objects.push('2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj');
  // Object 3 — Page
  objects.push(
    '3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj'
  );
  // Object 4 — Content stream
  objects.push(
    `4 0 obj\n<< /Length ${streamContent.length} >>\nstream\n${streamContent}\nendstream\nendobj`
  );
  // Object 5 — Font
  objects.push(
    '5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj'
  );

  let body = '';
  const offsets: number[] = [];
  const header = '%PDF-1.4\n';

  let pos = header.length;
  for (const obj of objects) {
    offsets.push(pos);
    const line = obj + '\n';
    body += line;
    pos += line.length;
  }

  const xrefOffset = pos;
  let xref = `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (const off of offsets) {
    xref += `${off.toString().padStart(10, '0')} 00000 n \n`;
  }

  const trailer = `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefOffset}\n%%EOF\n`;

  return Buffer.from(header + body + xref + trailer, 'utf-8');
}

// ---------------------------------------------------------------------------
// Suite setup — generate PDF fixture file
// ---------------------------------------------------------------------------

test.beforeAll(async () => {
  if (!fs.existsSync(FIXTURES_DIR)) {
    fs.mkdirSync(FIXTURES_DIR, { recursive: true });
  }
  if (!fs.existsSync(SAMPLE_PDF_PATH)) {
    fs.writeFileSync(SAMPLE_PDF_PATH, buildMinimalPdf());
  }
});

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Content Creation Wizard', () => {
  test.describe.configure({ mode: 'serial' });

  // --- Standalone tests (no AI dependency) ---

  test('admin can navigate to talks list and open creation wizard', async ({
    adminPage,
  }) => {
    const listPage = new ToolboxTalkListPage(adminPage);
    const wizardPage = new ContentCreationWizardPage(adminPage);

    await listPage.navigateTo();

    await expect(
      adminPage
        .locator('h1, h2, [data-testid="page-title"]')
        .filter({ hasText: /toolbox|talk|learning/i })
    ).toBeVisible({ timeout: TIMEOUTS.medium });

    await listPage.clickNewTalk();

    await wizardPage.assertCurrentStep(1);
    await expect(adminPage).toHaveURL(/\/admin\/toolbox-talks\/create/);
  });

  test('wizard Step 1 accepts a PDF upload and enables Continue', async ({
    adminPage,
  }) => {
    const wizardPage = new ContentCreationWizardPage(adminPage);

    await adminPage.goto(ROUTES.toolboxTalks.createTalk);
    await wizardPage.assertCurrentStep(1);

    await wizardPage.uploadPdf(SAMPLE_PDF_PATH);
    await wizardPage.selectTargetLanguages(['Polish']);

    await expect(wizardPage.continueButton).toBeEnabled({
      timeout: TIMEOUTS.medium,
    });
  });

  // --- AI-dependent tests: parse once in test 3, reuse page in tests 4-6 ---

  // Shared page persisted across serial tests within this describe block
  let sharedContext: BrowserContext;
  let sharedPage: Page;
  let wizardPage: ContentCreationWizardPage;

  test('parse step shows sections', async ({ browser }) => {
    test.setTimeout(300_000);

    // Create a long-lived authenticated admin page
    sharedContext = await browser.newContext({
      storageState: 'playwright/.auth/admin.json',
    });
    sharedPage = await sharedContext.newPage();
    wizardPage = new ContentCreationWizardPage(sharedPage);

    // Step 1 — upload PDF and trigger parse
    await sharedPage.goto(ROUTES.toolboxTalks.createTalk);
    await wizardPage.assertCurrentStep(1);
    await wizardPage.uploadPdf(SAMPLE_PDF_PATH);
    await wizardPage.selectTargetLanguages(['Polish']);

    // Click Continue — skip response interception, use DOM-based wait instead
    await sharedPage.getByRole('button', { name: /continue/i }).click();

    // Wait for parse to complete by watching for sections to appear
    // The "Sections (N)" heading appears once parsing finishes
    await sharedPage.locator(':text-matches("Sections \\\\(\\\\d+\\\\)")').waitFor({
      state: 'visible',
      timeout: 240_000,
    });

    // Wait for Continue button to be enabled (parse fully hydrated)
    await expect(sharedPage.getByRole('button', { name: /continue/i })).toBeEnabled({
      timeout: 15_000,
    });

    // Assert parse results
    const sectionCount = await wizardPage.getSectionCount();
    expect(sectionCount).toBeGreaterThanOrEqual(1);
  });

  test('Step 3 shows quiz questions', async () => {
    test.setTimeout(60_000);

    await wizardPage.clickNext();
    await wizardPage.waitForQuizGeneration(50_000);

    const questionCount = await wizardPage.getQuestionCount();
    expect(questionCount).toBeGreaterThanOrEqual(1);
    await expect(wizardPage.continueButton).toBeEnabled();
  });

  test('Step 4 settings can be configured', async () => {
    test.setTimeout(60_000);

    await wizardPage.clickNext();
    await wizardPage.assertCurrentStep(4);

    await wizardPage.fillSettingsTitle('E2E Test Talk - Settings');
    await wizardPage.fillSettingsDescription(
      'A test talk created by the E2E content creation suite.'
    );
    await wizardPage.setPassScore(70);
    await wizardPage.toggleCertificate(true);

    await expect(
      sharedPage.getByRole('button', { name: 'Translate & Validate', exact: true })
    ).toBeVisible();
  });

  test('full publish flow completes from settings', async () => {
    test.setTimeout(300_000);

    const talkTitle = `E2E Publish ${generateTestData.uniqueString('talk')}`;
    const listPage = new ToolboxTalkListPage(sharedPage);

    // Still on Settings — fill publish-specific title and trigger translate
    await wizardPage.fillSettingsTitle(talkTitle);
    await wizardPage.fillSettingsDescription('Full E2E publish flow test — automated.');
    await wizardPage.toggleCertificate(true);
    await wizardPage.clickTranslateAndValidate();

    // Step 5 — Translate & Validate
    await wizardPage.waitForTranslationComplete(180_000);
    await wizardPage.clickNext();

    // Step 6 — Publish
    await wizardPage.clickPublish();
    await wizardPage.waitForPublishSuccess(60_000);

    // Verify in list
    await listPage.navigateTo();
    await listPage.searchFor(talkTitle);
    await listPage.assertTalkVisible(talkTitle);

    const row = listPage.getTalkRowByTitle(talkTitle);
    await expect(row.locator(':text("Active")')).toBeVisible({
      timeout: TIMEOUTS.medium,
    });
  });

  test.afterAll(async () => {
    await sharedPage?.close();
    await sharedContext?.close();
  });
});
