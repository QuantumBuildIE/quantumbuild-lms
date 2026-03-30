import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';
import { TIMEOUTS } from '../../fixtures/test-constants';

const WIZARD_TIMEOUTS = {
  parse: 90_000,
  translation: 180_000,
  publish: 30_000,
};

/**
 * 6-step content creation wizard at /admin/toolbox-talks/talks/new
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

  private getStepButton(stepName: string): Locator {
    return this.stepNav.locator(`button:has-text("${stepName}")`);
  }

  async assertCurrentStep(stepNumber: number): Promise<void> {
    const stepNames = ['Input & Config', 'Parse', 'Quiz', 'Settings', 'Translate & Validate', 'Publish'];
    const name = stepNames[stepNumber - 1];
    // The active step button should be visually distinct (primary colour)
    await expect(this.getStepButton(name)).toBeVisible({ timeout: TIMEOUTS.medium });
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
    await this.page.locator(`button:has-text("${mode}")`).click();
  }

  async setTitle(title: string): Promise<void> {
    // Title is set in Step 4 (Settings). In Step 1 there is no title field.
    // Kept for convenience — delegates to fillSettingsTitle on the Settings step.
    throw new Error('Title is set in SettingsStep (step 4). Use fillSettingsTitle() after navigating there.');
  }

  async pasteSourceText(text: string): Promise<void> {
    await this.page.locator('#source-text').fill(text);
  }

  async uploadPdf(filePath: string): Promise<void> {
    await this.selectContentSource('Document');
    const fileInput = this.page.locator('input[type="file"][accept*=".pdf"]');
    await fileInput.setInputFiles(filePath);
    // Wait for upload to finish
    await this.page.locator('button:has-text("Continue")').waitFor({ state: 'visible', timeout: TIMEOUTS.long });
    await expect(this.page.locator(':text("Uploading")')).not.toBeVisible({ timeout: TIMEOUTS.long });
  }

  async uploadVideo(filePath: string): Promise<void> {
    await this.selectContentSource('Video');
    const fileInput = this.page.locator('input[type="file"][accept*=".mp4"]');
    await fileInput.setInputFiles(filePath);
    await expect(this.page.locator(':text("Uploading")')).not.toBeVisible({ timeout: TIMEOUTS.long });
  }

  async setVideoUrl(url: string): Promise<void> {
    await this.selectContentSource('Video');
    await this.page.locator('input[placeholder*="youtube"], input[placeholder*="video URL"]').fill(url);
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
    await this.page.locator('button:has-text("Continue")').click();
    // Wait for navigation away from step 1
    await this.page.waitForTimeout(500);
  }

  // ---------------------------------------------------------------------------
  // Step 2 — Parse
  // ---------------------------------------------------------------------------

  async waitForParseComplete(timeoutMs: number = WIZARD_TIMEOUTS.parse): Promise<void> {
    // Wait for the parse spinner/generating state to disappear and sections to appear
    await expect(this.page.locator(':text("Parse complete"), :text("sections")')).toBeVisible({ timeout: timeoutMs });
    // Ensure no active spinner remains
    await expect(this.page.locator('.animate-spin').first()).not.toBeVisible({ timeout: TIMEOUTS.medium });
  }

  async getSectionCount(): Promise<number> {
    // Section count shown as "{n} section(s)" text
    const text = await this.page.locator(':text-matches("\\\\d+ sections?")').first().textContent();
    if (!text) return 0;
    const match = text.match(/(\d+)\s+section/);
    return match ? parseInt(match[1], 10) : 0;
  }

  async clickRetryParse(): Promise<void> {
    await this.page.locator('button:has-text("Try Again")').click();
  }

  // ---------------------------------------------------------------------------
  // Step 3 — Quiz
  // ---------------------------------------------------------------------------

  async waitForQuizGeneration(timeoutMs: number = WIZARD_TIMEOUTS.parse): Promise<void> {
    await expect(this.page.locator(':text("Generating quiz questions")')).not.toBeVisible({ timeout: timeoutMs });
  }

  async getQuestionCount(): Promise<number> {
    const heading = this.page.locator(':text-matches("Questions \\\\(\\\\d+\\\\)")');
    const text = await heading.textContent();
    if (!text) return 0;
    const match = text.match(/Questions\s*\((\d+)\)/);
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
    await this.page.locator('button:has-text("Translate & Validate")').click();
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
    await this.page.locator('button:has-text("Publish")').click();
  }

  async waitForPublishSuccess(timeoutMs: number = WIZARD_TIMEOUTS.publish): Promise<void> {
    // Success state shows "Published" text with a check icon
    await expect(this.page.locator(':text("Published")')).toBeVisible({ timeout: timeoutMs });
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
    parseTimeoutMs?: number;
    translationTimeoutMs?: number;
  }): Promise<void> {
    // Step 1 — Upload PDF
    await this.uploadPdf(options.pdfFilePath);
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
