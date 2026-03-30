import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';
import { ROUTES, TIMEOUTS } from '../../fixtures/test-constants';

/**
 * Admin toolbox talks list page at /admin/toolbox-talks/talks
 */
export class ToolboxTalkListPage extends BasePage {
  readonly searchInput: Locator;
  readonly createButton: Locator;
  readonly table: Locator;
  readonly emptyState: Locator;
  readonly statusFilter: Locator;

  constructor(page: Page) {
    super(page);
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.createButton = page.locator('a:has-text("New"), a:has-text("Create"), button:has-text("New"), button:has-text("Create")');
    this.table = page.locator('table');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state, :text("No learnings found")');
    this.statusFilter = page.locator('[data-filter="status"], button:has-text("Status")');
  }

  async navigateTo(): Promise<void> {
    await this.page.goto(ROUTES.toolboxTalks.talksList);
    await this.waitForPageLoad();
  }

  async clickNewTalk(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/admin\/toolbox-talks\/create/, { timeout: TIMEOUTS.navigation });
  }

  async searchFor(term: string): Promise<void> {
    await this.searchInput.fill(term);
    // Wait for debounced search to trigger and results to load
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  getTalkRowByTitle(title: string): Locator {
    return this.page.locator(`tr:has-text("${title}")`);
  }

  async clickTalkRow(title: string): Promise<void> {
    const row = this.getTalkRowByTitle(title);
    await row.click();
    await this.waitForNavigation();
  }

  async getTalkCount(): Promise<number> {
    return await this.page.locator('tbody tr').count();
  }

  async editTalk(title: string): Promise<void> {
    const row = this.getTalkRowByTitle(title);
    await row.locator('button:has-text("Edit"), a:has-text("Edit"), [data-action="edit"]').click();
    await this.waitForNavigation();
  }

  async deleteTalk(title: string): Promise<void> {
    const row = this.getTalkRowByTitle(title);
    await row.locator('button:has-text("Delete"), [data-action="delete"]').click();
    await this.confirmDialog();
  }

  async assertTalkVisible(title: string): Promise<void> {
    await expect(this.getTalkRowByTitle(title)).toBeVisible({ timeout: TIMEOUTS.medium });
  }

  async assertTalkNotVisible(title: string): Promise<void> {
    await expect(this.getTalkRowByTitle(title)).not.toBeVisible({ timeout: TIMEOUTS.short });
  }
}
