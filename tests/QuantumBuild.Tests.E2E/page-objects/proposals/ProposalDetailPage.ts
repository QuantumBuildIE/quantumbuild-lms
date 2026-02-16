import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Proposal detail page object
 */
export class ProposalDetailPage extends BasePage {
  readonly pageTitle: Locator;
  readonly reference: Locator;
  readonly version: Locator;
  readonly statusBadge: Locator;
  readonly companyName: Locator;
  readonly siteName: Locator;
  readonly projectName: Locator;
  readonly validUntil: Locator;
  readonly subtotal: Locator;
  readonly vatAmount: Locator;
  readonly grandTotal: Locator;
  readonly margin: Locator;
  readonly sections: Locator;
  readonly contacts: Locator;
  readonly editButton: Locator;
  readonly submitButton: Locator;
  readonly approveButton: Locator;
  readonly rejectButton: Locator;
  readonly winButton: Locator;
  readonly loseButton: Locator;
  readonly cancelButton: Locator;
  readonly reviseButton: Locator;
  readonly pdfButton: Locator;
  readonly convertToOrderButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.reference = page.locator('[data-testid="reference"], .proposal-reference');
    this.version = page.locator('[data-testid="version"], .proposal-version');
    this.statusBadge = page.locator('[data-testid="status-badge"], .badge');
    this.companyName = page.locator('[data-testid="company-name"], .company-name');
    this.siteName = page.locator('[data-testid="site-name"], .site-name');
    this.projectName = page.locator('[data-testid="project-name"], .project-name');
    this.validUntil = page.locator('[data-testid="valid-until"], .valid-until');
    this.subtotal = page.locator('[data-testid="subtotal"], .subtotal');
    this.vatAmount = page.locator('[data-testid="vat-amount"], .vat-amount');
    this.grandTotal = page.locator('[data-testid="grand-total"], .grand-total');
    this.margin = page.locator('[data-testid="margin"], .margin');
    this.sections = page.locator('[data-section], .proposal-section');
    this.contacts = page.locator('[data-contact], .proposal-contact');
    this.editButton = page.locator('button:has-text("Edit"), a:has-text("Edit")');
    this.submitButton = page.locator('button:has-text("Submit"), button:has-text("Submit for Approval")');
    this.approveButton = page.locator('button:has-text("Approve")');
    this.rejectButton = page.locator('button:has-text("Reject")');
    this.winButton = page.locator('button:has-text("Win"), button:has-text("Mark Won")');
    this.loseButton = page.locator('button:has-text("Lose"), button:has-text("Mark Lost")');
    this.cancelButton = page.locator('button:has-text("Cancel Proposal")');
    this.reviseButton = page.locator('button:has-text("Revise"), button:has-text("Create Revision")');
    this.pdfButton = page.locator('button:has-text("PDF"), button:has-text("Download PDF"), a:has-text("PDF")');
    this.convertToOrderButton = page.locator('button:has-text("Convert to Order"), button:has-text("Create Stock Order")');
  }

  /**
   * Navigate to proposal detail page
   */
  async goto(id: string): Promise<void> {
    await this.page.goto(`/proposals/${id}`);
    await this.waitForPageLoad();
  }

  /**
   * Get the current status
   */
  async getStatus(): Promise<string> {
    return await this.statusBadge.textContent() || '';
  }

  /**
   * Submit the proposal
   */
  async submit(): Promise<void> {
    await this.submitButton.click();
    await this.waitForToastSuccess();
  }

  /**
   * Approve the proposal
   */
  async approve(): Promise<void> {
    await this.approveButton.click();
    await this.waitForToastSuccess();
  }

  /**
   * Reject the proposal
   */
  async reject(reason?: string): Promise<void> {
    await this.rejectButton.click();
    if (reason) {
      const reasonInput = this.page.locator('[name="rejectionReason"], #rejectionReason, textarea');
      if (await reasonInput.isVisible()) {
        await reasonInput.fill(reason);
      }
    }
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Mark as won
   */
  async markWon(): Promise<void> {
    await this.winButton.click();
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Mark as lost
   */
  async markLost(reason?: string): Promise<void> {
    await this.loseButton.click();
    if (reason) {
      const reasonInput = this.page.locator('[name="lostReason"], #lostReason, textarea');
      if (await reasonInput.isVisible()) {
        await reasonInput.fill(reason);
      }
    }
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Cancel the proposal
   */
  async cancel(): Promise<void> {
    await this.cancelButton.click();
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Create a revision
   */
  async revise(): Promise<void> {
    await this.reviseButton.click();
    await this.waitForToastSuccess();
  }

  /**
   * Download PDF
   */
  async downloadPdf(internal: boolean = false): Promise<void> {
    if (internal) {
      await this.page.locator('button:has-text("Internal PDF")').click();
    } else {
      await this.pdfButton.click();
    }
  }

  /**
   * Convert to stock order
   */
  async convertToStockOrder(siteId: string): Promise<void> {
    await this.convertToOrderButton.click();

    // Select site in dialog
    const siteSelect = this.page.locator('[name="destinationSiteId"], select');
    if (await siteSelect.isVisible()) {
      await this.selectOption(siteSelect, siteId);
    }

    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Edit the proposal
   */
  async edit(): Promise<void> {
    await this.editButton.click();
    await this.page.waitForURL(/\/edit/);
  }

  /**
   * Get section count
   */
  async getSectionCount(): Promise<number> {
    return await this.sections.count();
  }

  /**
   * Get line items in a section
   */
  async getLineItemCount(sectionIndex: number): Promise<number> {
    return await this.page.locator(`[data-section="${sectionIndex}"] [data-line], [data-section="${sectionIndex}"] tr`).count();
  }

  /**
   * Get contact count
   */
  async getContactCount(): Promise<number> {
    return await this.contacts.count();
  }

  /**
   * Assert the proposal status
   */
  async assertStatus(status: string): Promise<void> {
    await expect(this.statusBadge).toContainText(status);
  }

  /**
   * Check if action button is visible
   */
  async isActionVisible(action: 'submit' | 'approve' | 'reject' | 'win' | 'lose' | 'cancel' | 'revise' | 'convert'): Promise<boolean> {
    const buttonMap = {
      submit: this.submitButton,
      approve: this.approveButton,
      reject: this.rejectButton,
      win: this.winButton,
      lose: this.loseButton,
      cancel: this.cancelButton,
      revise: this.reviseButton,
      convert: this.convertToOrderButton,
    };
    return await buttonMap[action].isVisible();
  }

  /**
   * Get grand total amount
   */
  async getGrandTotal(): Promise<string> {
    return await this.grandTotal.textContent() || '0';
  }
}
