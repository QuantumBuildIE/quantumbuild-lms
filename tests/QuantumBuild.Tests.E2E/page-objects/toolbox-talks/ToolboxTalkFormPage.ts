import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Toolbox Talk form page object (create/edit)
 */
export class ToolboxTalkFormPage extends BasePage {
  readonly pageTitle: Locator;
  readonly titleInput: Locator;
  readonly descriptionInput: Locator;
  readonly frequencySelect: Locator;
  readonly categorySelect: Locator;
  readonly requiresQuizCheckbox: Locator;
  readonly passingScoreInput: Locator;
  readonly isActiveCheckbox: Locator;
  readonly addSectionButton: Locator;
  readonly addQuestionButton: Locator;
  readonly saveButton: Locator;
  readonly cancelButton: Locator;
  readonly deleteButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.titleInput = page.locator('[name="title"], #title');
    this.descriptionInput = page.locator('[name="description"], #description');
    this.frequencySelect = page.locator('[name="frequency"], #frequency');
    this.categorySelect = page.locator('[name="categoryId"], #categoryId');
    this.requiresQuizCheckbox = page.locator('[name="requiresQuiz"], #requiresQuiz');
    this.passingScoreInput = page.locator('[name="passingScore"], #passingScore');
    this.isActiveCheckbox = page.locator('[name="isActive"], #isActive');
    this.addSectionButton = page.locator('button:has-text("Add Section")');
    this.addQuestionButton = page.locator('button:has-text("Add Question")');
    this.saveButton = page.locator('button:has-text("Save"), button[type="submit"]');
    this.cancelButton = page.locator('button:has-text("Cancel"), a:has-text("Cancel")');
    this.deleteButton = page.locator('button:has-text("Delete")');
  }

  /**
   * Navigate to create new talk page
   */
  async goto(): Promise<void> {
    await this.page.goto('/toolbox-talks/talks/new');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to edit talk page
   */
  async gotoEdit(id: string): Promise<void> {
    await this.page.goto(`/toolbox-talks/talks/${id}/edit`);
    await this.waitForPageLoad();
  }

  /**
   * Fill the title field
   */
  async fillTitle(title: string): Promise<void> {
    await this.titleInput.fill(title);
  }

  /**
   * Fill the description field
   */
  async fillDescription(description: string): Promise<void> {
    await this.descriptionInput.fill(description);
  }

  /**
   * Select the frequency
   */
  async selectFrequency(frequency: string): Promise<void> {
    await this.selectOption(this.frequencySelect, frequency);
  }

  /**
   * Select the category
   */
  async selectCategory(category: string): Promise<void> {
    await this.selectOption(this.categorySelect, category);
  }

  /**
   * Enable/disable quiz requirement
   */
  async setRequiresQuiz(requires: boolean): Promise<void> {
    if (requires) {
      await this.requiresQuizCheckbox.check();
    } else {
      await this.requiresQuizCheckbox.uncheck();
    }
  }

  /**
   * Set the passing score
   */
  async setPassingScore(score: number): Promise<void> {
    await this.passingScoreInput.fill(score.toString());
  }

  /**
   * Add a content section
   */
  async addSection(data: { title: string; content: string }): Promise<void> {
    await this.addSectionButton.click();
    const sections = this.page.locator('[data-section]');
    const sectionCount = await sections.count();
    const sectionIndex = sectionCount - 1;

    await this.page.locator(`[name="sections.${sectionIndex}.title"]`).fill(data.title);

    // Handle both textarea and rich text editor
    const contentInput = this.page.locator(`[name="sections.${sectionIndex}.content"]`);
    if (await contentInput.isVisible()) {
      await contentInput.fill(data.content);
    } else {
      // Rich text editor fallback
      const editor = this.page.locator(`[data-section="${sectionIndex}"] .ql-editor, [data-section="${sectionIndex}"] [contenteditable="true"]`);
      await editor.fill(data.content);
    }
  }

  /**
   * Remove a section by index
   */
  async removeSection(index: number): Promise<void> {
    await this.page.locator(`[data-section="${index}"] button:has-text("Remove"), [data-section="${index}"] [data-action="remove"]`).click();
  }

  /**
   * Add a quiz question
   */
  async addQuestion(data: {
    text: string;
    type: 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer';
    options?: string[];
    correctAnswer: string;
  }): Promise<void> {
    await this.addQuestionButton.click();
    const questions = this.page.locator('[data-question]');
    const questionCount = await questions.count();
    const questionIndex = questionCount - 1;

    await this.page.locator(`[name="questions.${questionIndex}.questionText"]`).fill(data.text);
    await this.selectOption(
      this.page.locator(`[name="questions.${questionIndex}.questionType"]`),
      data.type
    );

    if (data.options && data.type === 'MultipleChoice') {
      for (let i = 0; i < data.options.length; i++) {
        const addOptionBtn = this.page.locator(`[data-question="${questionIndex}"] button:has-text("Add Option")`);
        if (await addOptionBtn.isVisible()) {
          await addOptionBtn.click();
        }
        await this.page.locator(`[name="questions.${questionIndex}.options.${i}"]`).fill(data.options[i]);
      }
    }

    await this.page.locator(`[name="questions.${questionIndex}.correctAnswer"]`).fill(data.correctAnswer);
  }

  /**
   * Remove a question by index
   */
  async removeQuestion(index: number): Promise<void> {
    await this.page.locator(`[data-question="${index}"] button:has-text("Remove"), [data-question="${index}"] [data-action="remove"]`).click();
  }

  /**
   * Save the form
   */
  async save(): Promise<void> {
    await this.saveButton.click();
  }

  /**
   * Save and wait for success toast
   */
  async saveAndWaitForSuccess(): Promise<void> {
    await this.save();
    await this.waitForToastSuccess();
  }

  /**
   * Cancel and go back
   */
  async cancel(): Promise<void> {
    await this.cancelButton.click();
  }

  /**
   * Delete the talk (edit mode only)
   */
  async delete(): Promise<void> {
    await this.deleteButton.click();
    await this.confirmDialog();
  }

  /**
   * Fill the complete form with all required fields
   */
  async fillForm(data: {
    title: string;
    description?: string;
    frequency: string;
    category?: string;
    requiresQuiz?: boolean;
    passingScore?: number;
  }): Promise<void> {
    await this.fillTitle(data.title);
    if (data.description) {
      await this.fillDescription(data.description);
    }
    await this.selectFrequency(data.frequency);
    if (data.category) {
      await this.selectCategory(data.category);
    }
    if (data.requiresQuiz !== undefined) {
      await this.setRequiresQuiz(data.requiresQuiz);
      if (data.requiresQuiz && data.passingScore) {
        await this.setPassingScore(data.passingScore);
      }
    }
  }
}
