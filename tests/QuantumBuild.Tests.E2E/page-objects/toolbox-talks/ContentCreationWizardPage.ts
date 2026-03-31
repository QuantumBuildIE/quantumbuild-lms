import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';
import { TIMEOUTS } from '../../fixtures/test-constants';

const WIZARD_TIMEOUTS = {
  parse: 90_000,
  translation: 180_000,
  publish: 30_000,
};

/**
 * 6-step content creation wizard at /admin/toolbox-talks/create
 */
export class ContentCreationWizardPage extends BasePage {
  readonly stepNav: Locator;
  readonly backButton: Locator;
  readonly continueButton: Locator;
  constructor(page: Page) {
    super(page);
    this.stepNav = page.locator('nav[aria-label="Progress"]');
    this.backButton = page.locator('button:has-text("Back")');
    this.continueButton = page.locator('button:has-text("Continue")');
  }

  // ---------------------------------------------------------------------------
  // Step navigation helpers
  // ---------------------------------------------------------------------------

  private getStepButton(stepNumber: number): Locator {
    // Position-based — avoids text matching ambiguity across browsers
    return this.stepNav.locator('button').nth(stepNumber - 1);
  }

  async assertCurrentStep(stepNumber: number): Promise<void> {
    await expect(this.getStepButton(stepNumber)).toBeVisible({ timeout: TIMEOUTS.medium });
  }

  async clickNext(): Promise<void> {
    await this.continueButton.click();
  }

  async clickBack(): Promise<void> {
    await this.backButton.click();
  }

  // ---------------------------------------------------------------------------
  // Step 1 — Input & Config
  // ---------------------------------------------------------------------------

  async selectContentSource(mode: 'Text' | 'Document' | 'Video'): Promise<void> {
    // Mode buttons contain the label text inside the card-style button
    await this.page.locator(`button:has-text("${mode}")`).click();
  }

  async pasteSourceText(text: string): Promise<void> {
    await this.page.locator('#source-text').fill(text);
  }

  async uploadPdf(filePath: string): Promise<void> {
    await this.selectContentSource('Document');
    // The file input is hidden (className="hidden") with accept=".pdf"
    const fileInput = this.page.locator('input[type="file"][accept=".pdf"]');
    await fileInput.setInputFiles(filePath);
    // Wait for file name to appear in the dropzone (confirms file was accepted)
    await expect(this.page.locator('text=.pdf')).toBeVisible({ timeout: TIMEOUTS.medium });
  }

  async uploadVideo(filePath: string): Promise<void> {
    await this.selectContentSource('Video');
    const fileInput = this.page.locator('input[type="file"][accept=".mp4,.mov,.avi,.webm"]');
    await fileInput.setInputFiles(filePath);
    await expect(this.page.locator(':text("Uploading")')).not.toBeVisible({ timeout: TIMEOUTS.long });
  }

  async setVideoUrl(url: string): Promise<void> {
    await this.selectContentSource('Video');
    await this.page.locator('input[placeholder*="youtube"]').fill(url);
  }

  /**
   * Select one or more target languages from the MultiSelectCombobox.
   * Language names must match the lookup values (e.g. "Polish", "Spanish").
   */
  async selectTargetLanguages(languageNames: string[]): Promise<void> {
    // Open the multi-select combobox popover — trigger has placeholder text
    const trigger = this.page.locator('button:has-text("Select target languages")');
    await trigger.click();
    await this.page.waitForTimeout(300); // popover animation

    for (const lang of languageNames) {
      // Type in the search box to filter, then click the matching item
      const searchInput = this.page.locator('input[placeholder="Search languages..."]');
      await searchInput.fill(lang);
      await this.page.waitForTimeout(200);
      // Click the command item matching the language name
      await this.page.locator(`[cmdk-item]:has-text("${lang}")`).first().click();
    }

    // Close the popover by clicking outside
    await this.page.keyboard.press('Escape');
  }

  async selectOutputType(type: 'lesson' | 'course'): Promise<void> {
    // Output type selector appears on the Parse step after parsing
    const label = type === 'lesson' ? 'Lesson' : 'Course';
    await this.page.locator(`button:has-text("${label}")`).click();
  }

  async toggleIncludeQuiz(enabled: boolean): Promise<void> {
    const toggle = this.page.locator('#include-quiz');
    const isChecked = await toggle.isChecked();
    if (isChecked !== enabled) {
      await toggle.click();
    }
  }

  async clickContinueFromInputConfig(): Promise<void> {
    await this.page.getByRole('button', { name: /continue/i }).click();
  }

  // ---------------------------------------------------------------------------
  // Step 2 — Parse
  // ---------------------------------------------------------------------------

  async waitForParseComplete(timeoutMs = 180_000): Promise<void> {
    await this.page
      .getByText(/Sections \(\d+\)/)
      .waitFor({ state: 'visible', timeout: timeoutMs });
  }

  async getSectionCount(): Promise<number> {
    const el = this.page.getByText(/Sections \(\d+\)/);
    const text = await el.textContent();
    if (!text) return 0;
    const match = text.match(/\((\d+)\)/);
    return match ? parseInt(match[1], 10) : 0;
  }

  async clickRetryParse(): Promise<void> {
    await this.page.locator('button:has-text("Try Again")').click();
  }

  // ---------------------------------------------------------------------------
  // Step 3 — Quiz
  // ---------------------------------------------------------------------------

  async waitForQuizGeneration(timeoutMs: number = WIZARD_TIMEOUTS.parse): Promise<void> {
    // Wait for the "Questions (N)" heading — positive signal that generation finished
    await this.page
      .getByText(/Questions \(\d+\)/)
      .waitFor({ state: 'visible', timeout: timeoutMs });
  }

  async getQuestionCount(): Promise<number> {
    const el = this.page.getByText(/Questions \(\d+\)/);
    const text = await el.textContent();
    if (!text) return 0;
    const match = text.match(/\((\d+)\)/);
    return match ? parseInt(match[1], 10) : 0;
  }

  async clickRegenerateQuiz(): Promise<void> {
    await this.page.locator('button:has-text("Regenerate All")').click();
  }

  // ---------------------------------------------------------------------------
  // Step 4 — Settings
  // ---------------------------------------------------------------------------

  async fillSettingsTitle(title: string): Promise<void> {
    await this.page.locator('#settings-title').fill(title);
  }

  async fillSettingsDescription(description: string): Promise<void> {
    await this.page.locator('#settings-description').fill(description);
  }

  async setPassScore(score: number): Promise<void> {
    // Pass score may be a select or input depending on the quiz settings panel
    const input = this.page.locator('#passingScore, input[name="passingScore"]');
    if (await input.isVisible()) {
      await input.fill(score.toString());
    }
  }

  async toggleCertificate(enabled: boolean): Promise<void> {
    const toggle = this.page.locator('#generate-certificate');
    const isChecked = await toggle.isChecked();
    if (isChecked !== enabled) {
      await toggle.click();
    }
  }

  async toggleAutoAssign(enabled: boolean): Promise<void> {
    const toggle = this.page.locator('#auto-assign');
    const isChecked = await toggle.isChecked();
    if (isChecked !== enabled) {
      await toggle.click();
    }
  }

  async clickTranslateAndValidate(): Promise<void> {
    // Target the action button, not the step nav button
    await this.page.getByRole('button', { name: 'Translate & Validate', exact: true }).click();
  }

  // ---------------------------------------------------------------------------
  // Step 5 — Translate & Validate
  // ---------------------------------------------------------------------------

  async waitForTranslationComplete(timeoutMs: number = WIZARD_TIMEOUTS.translation): Promise<void> {
    // Wait until the continue button becomes enabled (session status = 'Validated')
    await expect(this.continueButton).toBeEnabled({ timeout: timeoutMs });
  }

  async getValidationSectionCount(): Promise<{ completed: number; total: number }> {
    const text = await this.page.locator(':text-matches("\\\\d+/\\\\d+ sections")').first().textContent();
    if (!text) return { completed: 0, total: 0 };
    const match = text.match(/(\d+)\/(\d+)/);
    return match ? { completed: parseInt(match[1], 10), total: parseInt(match[2], 10) } : { completed: 0, total: 0 };
  }

  // ---------------------------------------------------------------------------
  // Step 6 — Publish
  // ---------------------------------------------------------------------------

  async clickPublish(): Promise<void> {
    // Target the action button, not the step nav button
    await this.page.getByRole('button', { name: 'Publish', exact: true }).click();
  }

  async waitForPublishSuccess(timeoutMs: number = WIZARD_TIMEOUTS.publish): Promise<void> {
    // Success heading is "Toolbox Talk Published" or "Course Published"
    await expect(this.page.getByText(/Published/)).toBeVisible({ timeout: timeoutMs });
  }

  async clickViewTalk(): Promise<void> {
    await this.page.locator('button:has-text("View Toolbox Talk"), button:has-text("View Talk")').click();
    await this.waitForNavigation();
  }

  async clickViewCourseList(): Promise<void> {
    await this.page.locator('button:has-text("View Course List")').click();
    await this.waitForNavigation();
  }

  async clickCreateAnother(): Promise<void> {
    await this.page.locator('button:has-text("Create Another")').click();
  }

  // ---------------------------------------------------------------------------
  // Full flow helpers
  // ---------------------------------------------------------------------------

  /**
   * Runs the complete wizard from PDF upload through to publish.
   * Steps 2, 3, and 5 wait for async AI operations to complete.
   */
  async runFullPdfWizard(options: {
    pdfFilePath: string;
    title: string;
    description?: string;
    includeQuiz?: boolean;
    certificate?: boolean;
    targetLanguages?: string[];
    parseTimeoutMs?: number;
    translationTimeoutMs?: number;
  }): Promise<void> {
    // Step 1 — Upload PDF and select target languages
    await this.uploadPdf(options.pdfFilePath);
    await this.selectTargetLanguages(options.targetLanguages ?? ['Polish']);
    if (options.includeQuiz !== undefined) {
      await this.toggleIncludeQuiz(options.includeQuiz);
    }
    await this.clickContinueFromInputConfig();

    // Step 2 — Parse
    await this.waitForParseComplete(options.parseTimeoutMs);
    await this.clickNext();

    // Step 3 — Quiz (if enabled, otherwise auto-skipped)
    const quizVisible = await this.page.locator(':text("Questions")').isVisible();
    if (quizVisible) {
      await this.waitForQuizGeneration();
      await this.clickNext();
    }

    // Step 4 — Settings
    await this.fillSettingsTitle(options.title);
    if (options.description) {
      await this.fillSettingsDescription(options.description);
    }
    if (options.certificate !== undefined) {
      await this.toggleCertificate(options.certificate);
    }
    await this.clickTranslateAndValidate();

    // Step 5 — Translate & Validate
    await this.waitForTranslationComplete(options.translationTimeoutMs);
    await this.clickNext();

    // Step 6 — Publish
    await this.clickPublish();
    await this.waitForPublishSuccess();
  }
}
