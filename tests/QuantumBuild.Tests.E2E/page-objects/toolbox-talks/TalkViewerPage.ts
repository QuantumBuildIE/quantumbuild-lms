import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';
import { TIMEOUTS } from '../../fixtures/test-constants';

/**
 * Employee talk viewer at /toolbox-talks/[id]
 * Handles the full completion flow: Video → Sections → Quiz → Sign → Complete
 */
export class TalkViewerPage extends BasePage {
  readonly videoPlayer: Locator;
  readonly signatureCanvas: Locator;

  constructor(page: Page) {
    super(page);
    this.videoPlayer = page.locator('video, iframe[src*="youtube"], iframe[src*="vimeo"]');
    this.signatureCanvas = page.locator('canvas');
  }

  // ---------------------------------------------------------------------------
  // Navigation
  // ---------------------------------------------------------------------------

  async goto(talkId: string): Promise<void> {
    await this.page.goto(`/toolbox-talks/${talkId}`);
    await this.waitForPageLoad();
  }

  // ---------------------------------------------------------------------------
  // Step navigation
  // ---------------------------------------------------------------------------

  private getStepButton(label: string): Locator {
    return this.page.locator(`button:has-text("${label}")`);
  }

  async clickStep(step: 'Video' | 'Sections' | 'Quiz' | 'Sign' | 'Complete'): Promise<void> {
    await this.getStepButton(step).click();
  }

  // ---------------------------------------------------------------------------
  // Start / Video
  // ---------------------------------------------------------------------------

  async startTalk(): Promise<void> {
    // Click the first available action — either "Start" or "Continue to Sections"
    const startBtn = this.page.locator(
      'button:has-text("Start"), button:has-text("Continue to Sections"), button:has-text("Begin")'
    );
    await startBtn.first().click();
    await this.page.waitForTimeout(500);
  }

  async watchVideoIfPresent(durationMs: number = 3000): Promise<void> {
    if (await this.videoPlayer.isVisible()) {
      const playBtn = this.page.locator('button:has-text("Play"), [data-action="play"]');
      if (await playBtn.isVisible()) {
        await playBtn.click();
      }
      await this.page.waitForTimeout(durationMs);
    }
  }

  // ---------------------------------------------------------------------------
  // Sections
  // ---------------------------------------------------------------------------

  private get sectionNavigator(): Locator {
    return this.page.locator('nav:has-text("Sections"), [data-testid="section-navigator"]');
  }

  async readSection(index: number): Promise<void> {
    // Click section item in the sidebar navigator if visible (desktop)
    const sectionButtons = this.sectionNavigator.locator('button');
    const count = await sectionButtons.count();
    if (count > 0 && index < count) {
      await sectionButtons.nth(index).click();
      await this.page.waitForTimeout(300);
    }

    // Find and click the acknowledge / mark-read action for this section
    const acknowledgeBtn = this.page.locator(
      'button:has-text("Acknowledge"), button:has-text("Mark as Read"), button:has-text("I have read"), input[type="checkbox"]'
    ).first();
    if (await acknowledgeBtn.isVisible()) {
      await acknowledgeBtn.click();
      await this.page.waitForTimeout(300);
    }
  }

  async readAllSections(): Promise<void> {
    // Read through sections sequentially using Next button or sidebar
    const nextBtn = this.page.locator('button:has-text("Next")');
    const acknowledgeBtn = this.page.locator(
      'button:has-text("Acknowledge"), button:has-text("Mark as Read"), button:has-text("I have read"), input[type="checkbox"]'
    );

    let iteration = 0;
    const maxIterations = 50; // safety limit

    while (iteration < maxIterations) {
      // Acknowledge current section if possible
      if (await acknowledgeBtn.first().isVisible({ timeout: 1000 }).catch(() => false)) {
        await acknowledgeBtn.first().click();
        await this.page.waitForTimeout(300);
      }

      // Try to advance to next section
      if (await nextBtn.isVisible({ timeout: 1000 }).catch(() => false) && await nextBtn.isEnabled()) {
        await nextBtn.click();
        await this.page.waitForTimeout(500);
      } else {
        break;
      }
      iteration++;
    }
  }

  // ---------------------------------------------------------------------------
  // Quiz
  // ---------------------------------------------------------------------------

  /**
   * Submit quiz with answers keyed by question text fragment or index.
   * For multiple choice / true-false, the value should match the option label text.
   * For short answer, the value is typed into the text input.
   */
  async submitQuizAnswers(answers: Record<string, string>): Promise<void> {
    for (const [questionKey, answer] of Object.entries(answers)) {
      // Find the question container — match by partial text in question heading
      const questionContainer = this.page.locator(`[data-question]:has-text("${questionKey}"), .question-card:has-text("${questionKey}")`).first();

      if (await questionContainer.isVisible().catch(() => false)) {
        // Try radio option first (MC / TF)
        const radio = questionContainer.locator(`label:has-text("${answer}") input[type="radio"], input[type="radio"][value="${answer}"]`);
        if (await radio.isVisible().catch(() => false)) {
          await radio.check();
          continue;
        }
        // Fall back to text input (short answer)
        const textInput = questionContainer.locator('input[type="text"], textarea');
        if (await textInput.isVisible().catch(() => false)) {
          await textInput.fill(answer);
        }
      }
    }

    // Click submit
    const submitBtn = this.page.locator('button:has-text("Submit Quiz"), button:has-text("Submit Answers"), button:has-text("Submit")');
    await submitBtn.first().click();
    await this.page.waitForTimeout(500);
  }

  async getQuizResult(): Promise<{ passed: boolean; score: number }> {
    // Wait for quiz result to appear
    const resultArea = this.page.locator(':text-matches("(passed|failed|score|result)", "i")').first();
    await expect(resultArea).toBeVisible({ timeout: TIMEOUTS.medium });

    const resultText = await this.page.locator('body').textContent() || '';

    const passed = /pass/i.test(resultText) && !/fail/i.test(resultText);
    const scoreMatch = resultText.match(/(\d+)\s*%/);
    const score = scoreMatch ? parseInt(scoreMatch[1], 10) : 0;

    return { passed, score };
  }

  // ---------------------------------------------------------------------------
  // Signature & Completion
  // ---------------------------------------------------------------------------

  async drawSignature(): Promise<void> {
    const canvas = this.signatureCanvas;
    await expect(canvas).toBeVisible({ timeout: TIMEOUTS.medium });
    const box = await canvas.boundingBox();
    if (box) {
      await this.page.mouse.move(box.x + 50, box.y + 50);
      await this.page.mouse.down();
      await this.page.mouse.move(box.x + 150, box.y + 80);
      await this.page.mouse.move(box.x + 100, box.y + 100);
      await this.page.mouse.move(box.x + 200, box.y + 60);
      await this.page.mouse.up();
    }
  }

  async signAndComplete(signatureDataUrl?: string): Promise<void> {
    // Draw on canvas (we can't inject a dataUrl via Playwright easily, so draw)
    await this.drawSignature();

    // Click complete / finish button
    const completeBtn = this.page.locator(
      'button:has-text("Complete"), button:has-text("Finish"), button:has-text("Submit")'
    );
    await completeBtn.first().click();
    await this.page.waitForTimeout(500);
  }

  async waitForCertificate(): Promise<void> {
    await expect(
      this.page.locator(':text("Certificate"), :text("certificate"), :text("completed successfully")')
    ).toBeVisible({ timeout: TIMEOUTS.long });
  }

  // ---------------------------------------------------------------------------
  // Full flow helper
  // ---------------------------------------------------------------------------

  /**
   * Completes the entire talk flow: start → read sections → quiz → sign.
   */
  async completeEntireTalk(options?: {
    quizAnswers?: Record<string, string>;
    signedByName?: string;
    watchVideoDurationMs?: number;
  }): Promise<void> {
    await this.startTalk();
    await this.watchVideoIfPresent(options?.watchVideoDurationMs);
    await this.readAllSections();

    if (options?.quizAnswers && Object.keys(options.quizAnswers).length > 0) {
      await this.submitQuizAnswers(options.quizAnswers);
    }

    await this.signAndComplete();
  }
}
