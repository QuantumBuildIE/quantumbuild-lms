import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Talk viewer page object - for completing toolbox talks
 */
export class TalkViewerPage extends BasePage {
  readonly talkTitle: Locator;
  readonly progressIndicator: Locator;
  readonly sectionTitle: Locator;
  readonly sectionContent: Locator;
  readonly videoPlayer: Locator;
  readonly acknowledgeCheckbox: Locator;
  readonly nextButton: Locator;
  readonly previousButton: Locator;
  readonly completeButton: Locator;
  readonly signatureCanvas: Locator;
  readonly signedByNameInput: Locator;
  readonly quizSection: Locator;

  constructor(page: Page) {
    super(page);
    this.talkTitle = page.locator('h1, [data-testid="talk-title"]');
    this.progressIndicator = page.locator('[data-testid="progress"], .progress-bar, .progress');
    this.sectionTitle = page.locator('[data-testid="section-title"], h2');
    this.sectionContent = page.locator('[data-testid="section-content"], .section-content, article');
    this.videoPlayer = page.locator('video, iframe[src*="youtube"], iframe[src*="vimeo"]');
    this.acknowledgeCheckbox = page.locator('[name="acknowledged"], #acknowledged, input[type="checkbox"]:near(:text("acknowledge"))');
    this.nextButton = page.locator('button:has-text("Next"), [data-action="next"]');
    this.previousButton = page.locator('button:has-text("Previous"), button:has-text("Back"), [data-action="previous"]');
    this.completeButton = page.locator('button:has-text("Complete"), button:has-text("Finish"), button:has-text("Submit")');
    this.signatureCanvas = page.locator('canvas, [data-testid="signature-pad"]');
    this.signedByNameInput = page.locator('[name="signedByName"], #signedByName');
    this.quizSection = page.locator('[data-testid="quiz-section"], .quiz-section');
  }

  /**
   * Navigate to a specific talk
   */
  async goto(talkId: string): Promise<void> {
    await this.page.goto(`/toolbox-talks/talks/${talkId}/view`);
    await this.waitForPageLoad();
  }

  /**
   * Get current section number
   */
  async getCurrentSection(): Promise<number> {
    const progress = await this.progressIndicator.getAttribute('data-current');
    return progress ? parseInt(progress, 10) : 1;
  }

  /**
   * Get total sections count
   */
  async getTotalSections(): Promise<number> {
    const progress = await this.progressIndicator.getAttribute('data-total');
    return progress ? parseInt(progress, 10) : 1;
  }

  /**
   * Acknowledge the current section
   */
  async acknowledgeSection(): Promise<void> {
    const isVisible = await this.acknowledgeCheckbox.isVisible();
    if (isVisible) {
      await this.acknowledgeCheckbox.check();
    }
  }

  /**
   * Go to the next section
   */
  async goToNextSection(): Promise<void> {
    await this.acknowledgeSection();
    await this.nextButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Go to the previous section
   */
  async goToPreviousSection(): Promise<void> {
    await this.previousButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Read through all sections
   */
  async readAllSections(): Promise<void> {
    while (await this.nextButton.isVisible() && await this.nextButton.isEnabled()) {
      await this.goToNextSection();
    }
  }

  /**
   * Watch video if present (wait for video to be viewable)
   */
  async watchVideoIfPresent(durationMs: number = 3000): Promise<void> {
    if (await this.videoPlayer.isVisible()) {
      // Click play if there's a play button
      const playButton = this.page.locator('button:has-text("Play"), [data-action="play"]');
      if (await playButton.isVisible()) {
        await playButton.click();
      }
      // Wait for some of the video duration
      await this.page.waitForTimeout(durationMs);
    }
  }

  /**
   * Draw a signature on the canvas
   */
  async drawSignature(): Promise<void> {
    const canvas = this.signatureCanvas;
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

  /**
   * Sign with name
   */
  async sign(name: string): Promise<void> {
    await this.drawSignature();
    await this.signedByNameInput.fill(name);
  }

  /**
   * Complete the talk
   */
  async complete(): Promise<void> {
    await this.completeButton.click();
  }

  /**
   * Sign and complete the talk
   */
  async signAndComplete(name: string): Promise<void> {
    await this.sign(name);
    await this.complete();
    await this.waitForToastSuccess();
  }

  /**
   * Answer a quiz question
   */
  async answerQuestion(questionIndex: number, answer: string): Promise<void> {
    const question = this.page.locator(`[data-question="${questionIndex}"]`);

    // Try different answer input types
    const radioOption = question.locator(`input[type="radio"][value="${answer}"], label:has-text("${answer}") input[type="radio"]`);
    const textInput = question.locator('input[type="text"], textarea');

    if (await radioOption.isVisible()) {
      await radioOption.check();
    } else if (await textInput.isVisible()) {
      await textInput.fill(answer);
    }
  }

  /**
   * Answer all quiz questions
   */
  async answerAllQuestions(answers: { questionIndex: number; answer: string }[]): Promise<void> {
    for (const { questionIndex, answer } of answers) {
      await this.answerQuestion(questionIndex, answer);
    }
  }

  /**
   * Submit the quiz
   */
  async submitQuiz(): Promise<void> {
    const submitButton = this.page.locator('button:has-text("Submit Quiz"), button:has-text("Submit Answers")');
    await submitButton.click();
  }

  /**
   * Complete the entire talk including reading sections and quiz
   */
  async completeEntireTalk(name: string, quizAnswers?: { questionIndex: number; answer: string }[]): Promise<void> {
    await this.readAllSections();

    if (quizAnswers && await this.quizSection.isVisible()) {
      await this.answerAllQuestions(quizAnswers);
      await this.submitQuiz();
    }

    await this.signAndComplete(name);
  }
}
