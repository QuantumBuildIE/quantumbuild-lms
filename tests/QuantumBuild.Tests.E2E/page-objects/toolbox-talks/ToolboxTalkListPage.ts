import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Toolbox Talk list page object
 */
export class ToolboxTalkListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly statusFilter: Locator;
  readonly frequencyFilter: Locator;
  readonly table: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('button:has-text("Create"), a:has-text("New Talk"), a:has-text("Create")');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.statusFilter = page.locator('[data-filter="status"], select[name="status"]');
    this.frequencyFilter = page.locator('[data-filter="frequency"], select[name="frequency"]');
    this.table = page.locator('table, [data-testid="talk-list"]');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to the toolbox talks list
   */
  async goto(): Promise<void> {
    await this.page.goto('/toolbox-talks/talks');
    await this.waitForPageLoad();
  }

  /**
   * Click the create button to create a new talk
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/toolbox-talks\/talks\/new/);
  }

  /**
   * Search for talks by term
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500); // Wait for debounce
    await this.waitForPageLoad();
  }

  /**
   * Clear the search input
   */
  async clearSearch(): Promise<void> {
    await this.searchInput.clear();
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
   * Filter by frequency
   */
  async filterByFrequency(frequency: string): Promise<void> {
    await this.selectOption(this.frequencyFilter, frequency);
    await this.waitForPageLoad();
  }

  /**
   * Get a specific talk row by title or ID
   */
  getTalkRow(titleOrId: string): Locator {
    return this.page.locator(`tr:has-text("${titleOrId}"), [data-id="${titleOrId}"]`);
  }

  /**
   * Click on a talk to view details
   */
  async clickTalk(titleOrId: string): Promise<void> {
    await this.getTalkRow(titleOrId).click();
  }

  /**
   * Edit a talk
   */
  async editTalk(titleOrId: string): Promise<void> {
    const row = this.getTalkRow(titleOrId);
    await row.locator('button:has-text("Edit"), a:has-text("Edit"), [data-action="edit"]').click();
  }

  /**
   * Delete a talk
   */
  async deleteTalk(titleOrId: string): Promise<void> {
    const row = this.getTalkRow(titleOrId);
    await row.locator('button:has-text("Delete"), [data-action="delete"]').click();
  }

  /**
   * Confirm delete in dialog
   */
  async confirmDelete(): Promise<void> {
    await this.confirmDialog();
  }

  /**
   * Get the count of talks in the table
   */
  async getTalkCount(): Promise<number> {
    return await this.getTableRowCount();
  }

  /**
   * Check if empty state is displayed
   */
  async isEmptyStateDisplayed(): Promise<boolean> {
    return await this.emptyState.isVisible();
  }
}
