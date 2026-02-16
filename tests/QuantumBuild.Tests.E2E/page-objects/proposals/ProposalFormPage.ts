import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Proposal form page object (create/edit)
 */
export class ProposalFormPage extends BasePage {
  readonly pageTitle: Locator;
  readonly companySelect: Locator;
  readonly siteSelect: Locator;
  readonly projectNameInput: Locator;
  readonly validUntilInput: Locator;
  readonly vatRateInput: Locator;
  readonly notesInput: Locator;
  readonly addSectionButton: Locator;
  readonly addFromKitButton: Locator;
  readonly sections: Locator;
  readonly saveButton: Locator;
  readonly submitButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.companySelect = page.locator('[name="companyId"], #companyId');
    this.siteSelect = page.locator('[name="siteId"], #siteId');
    this.projectNameInput = page.locator('[name="projectName"], #projectName');
    this.validUntilInput = page.locator('[name="validUntil"], #validUntil');
    this.vatRateInput = page.locator('[name="vatRate"], #vatRate');
    this.notesInput = page.locator('[name="notes"], #notes');
    this.addSectionButton = page.locator('button:has-text("Add Section")');
    this.addFromKitButton = page.locator('button:has-text("Add from Kit"), button:has-text("Insert Kit")');
    this.sections = page.locator('[data-section], .proposal-section');
    this.saveButton = page.locator('button:has-text("Save"), button[type="submit"]');
    this.submitButton = page.locator('button:has-text("Submit"), button:has-text("Submit for Approval")');
    this.cancelButton = page.locator('button:has-text("Cancel"), a:has-text("Cancel")');
  }

  /**
   * Navigate to create new proposal
   */
  async goto(): Promise<void> {
    await this.page.goto('/proposals/new');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to edit proposal
   */
  async gotoEdit(id: string): Promise<void> {
    await this.page.goto(`/proposals/${id}/edit`);
    await this.waitForPageLoad();
  }

  /**
   * Fill basic proposal details
   */
  async fillBasicDetails(data: {
    companyId: string;
    siteId?: string;
    projectName?: string;
    validUntil?: string;
    vatRate?: number;
    notes?: string;
  }): Promise<void> {
    await this.selectOption(this.companySelect, data.companyId);

    if (data.siteId) {
      await this.selectOption(this.siteSelect, data.siteId);
    }
    if (data.projectName) {
      await this.projectNameInput.fill(data.projectName);
    }
    if (data.validUntil) {
      await this.validUntilInput.fill(data.validUntil);
    }
    if (data.vatRate !== undefined) {
      await this.vatRateInput.fill(data.vatRate.toString());
    }
    if (data.notes) {
      await this.notesInput.fill(data.notes);
    }
  }

  /**
   * Add a new section
   */
  async addSection(name: string, description?: string): Promise<void> {
    await this.addSectionButton.click();
    const sectionCount = await this.sections.count();
    const sectionIndex = sectionCount - 1;

    await this.page.locator(`[name="sections.${sectionIndex}.name"]`).fill(name);
    if (description) {
      await this.page.locator(`[name="sections.${sectionIndex}.description"]`).fill(description);
    }
  }

  /**
   * Add section from product kit
   */
  async addSectionFromKit(kitName: string): Promise<void> {
    await this.addFromKitButton.click();

    // Select kit from dialog
    await this.page.locator(`[data-kit-name="${kitName}"], button:has-text("${kitName}")`).click();

    // Confirm selection
    const insertButton = this.page.locator('button:has-text("Insert"), button:has-text("Add")');
    if (await insertButton.isVisible()) {
      await insertButton.click();
    }
  }

  /**
   * Add a line item to a section
   */
  async addLineItem(
    sectionIndex: number,
    data: {
      productId?: string;
      description: string;
      quantity: number;
      unitPrice: number;
      discountPercentage?: number;
    }
  ): Promise<void> {
    const section = this.page.locator(`[data-section="${sectionIndex}"]`);
    await section.locator('button:has-text("Add Line"), button:has-text("Add Item")').click();

    const lineCount = await section.locator('[data-line]').count();
    const lineIndex = lineCount - 1;

    if (data.productId) {
      await this.selectOption(
        section.locator(`[name="lines.${lineIndex}.productId"]`),
        data.productId
      );
    }

    await section.locator(`[name="lines.${lineIndex}.description"]`).fill(data.description);
    await section.locator(`[name="lines.${lineIndex}.quantity"]`).fill(data.quantity.toString());
    await section.locator(`[name="lines.${lineIndex}.unitPrice"]`).fill(data.unitPrice.toString());

    if (data.discountPercentage !== undefined) {
      await section.locator(`[name="lines.${lineIndex}.discountPercentage"]`).fill(data.discountPercentage.toString());
    }
  }

  /**
   * Remove a section
   */
  async removeSection(index: number): Promise<void> {
    await this.page.locator(`[data-section="${index}"] button:has-text("Remove Section"), [data-section="${index}"] [data-action="remove-section"]`).click();
  }

  /**
   * Remove a line item
   */
  async removeLineItem(sectionIndex: number, lineIndex: number): Promise<void> {
    await this.page.locator(`[data-section="${sectionIndex}"] [data-line="${lineIndex}"] button:has-text("Remove"), [data-section="${sectionIndex}"] [data-line="${lineIndex}"] [data-action="remove"]`).click();
  }

  /**
   * Get section count
   */
  async getSectionCount(): Promise<number> {
    return await this.sections.count();
  }

  /**
   * Get line item count in a section
   */
  async getLineItemCount(sectionIndex: number): Promise<number> {
    return await this.page.locator(`[data-section="${sectionIndex}"] [data-line]`).count();
  }

  /**
   * Save the proposal
   */
  async save(): Promise<void> {
    await this.saveButton.click();
  }

  /**
   * Save and wait for success
   */
  async saveAndWaitForSuccess(): Promise<void> {
    await this.save();
    await this.waitForToastSuccess();
  }

  /**
   * Submit the proposal
   */
  async submit(): Promise<void> {
    await this.submitButton.click();
  }

  /**
   * Submit and wait for success
   */
  async submitAndWaitForSuccess(): Promise<void> {
    await this.submit();
    await this.waitForToastSuccess();
  }

  /**
   * Cancel form
   */
  async cancel(): Promise<void> {
    await this.cancelButton.click();
  }
}
