import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Proposal list page object
 */
export class ProposalListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly statusFilter: Locator;
  readonly companyFilter: Locator;
  readonly table: Locator;
  readonly pagination: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('a:has-text("New Proposal"), button:has-text("Create"), a[href*="/proposals/new"]');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.statusFilter = page.locator('[data-filter="status"], select[name="status"]');
    this.companyFilter = page.locator('[data-filter="company"], select[name="companyId"]');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to proposals list
   */
  async goto(): Promise<void> {
    await this.page.goto('/proposals/list');
    await this.waitForPageLoad();
  }

  /**
   * Click create new proposal
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/proposals\/new/);
  }

  /**
   * Search proposals
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  /**
   * Filter by status
   */
  async filterByStatus(status: string): Promise<void> {
    await this.selectOption(this.statusFilter, status);
    await this.waitForPageLoad();
  }

  /**
   * Filter by company
   */
  async filterByCompany(company: string): Promise<void> {
    await this.selectOption(this.companyFilter, company);
    await this.waitForPageLoad();
  }

  /**
   * Get proposal row by reference
   */
  getProposalRow(reference: string): Locator {
    return this.page.locator(`tr:has-text("${reference}")`);
  }

  /**
   * Click on proposal to view details
   */
  async clickProposal(reference: string): Promise<void> {
    await this.getProposalRow(reference).click();
  }

  /**
   * Get proposal status badge
   */
  getProposalStatus(reference: string): Locator {
    return this.getProposalRow(reference).locator('.badge, [data-status]');
  }

  /**
   * Edit a proposal
   */
  async editProposal(reference: string): Promise<void> {
    const row = this.getProposalRow(reference);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Delete a proposal
   */
  async deleteProposal(reference: string): Promise<void> {
    const row = this.getProposalRow(reference);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
  }

  /**
   * Get proposal count
   */
  async getProposalCount(): Promise<number> {
    return await this.getTableRowCount();
  }
}
