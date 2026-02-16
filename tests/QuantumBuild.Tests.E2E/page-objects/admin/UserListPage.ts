import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * User list page object
 */
export class UserListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly roleFilter: Locator;
  readonly table: Locator;
  readonly pagination: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('a:has-text("New User"), button:has-text("Create"), a[href*="/users/new"]');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.roleFilter = page.locator('[data-filter="role"], select[name="roleId"]');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to users list
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/users');
    await this.waitForPageLoad();
  }

  /**
   * Click create new user
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/admin\/users\/new/);
  }

  /**
   * Search users
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  /**
   * Filter by role
   */
  async filterByRole(role: string): Promise<void> {
    await this.selectOption(this.roleFilter, role);
    await this.waitForPageLoad();
  }

  /**
   * Get user row by email or name
   */
  getUserRow(emailOrName: string): Locator {
    return this.page.locator(`tr:has-text("${emailOrName}")`);
  }

  /**
   * Edit a user
   */
  async editUser(emailOrName: string): Promise<void> {
    const row = this.getUserRow(emailOrName);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Toggle user active status
   */
  async toggleUserActive(emailOrName: string): Promise<void> {
    const row = this.getUserRow(emailOrName);
    await row.locator('button:has-text("Toggle"), button:has-text("Activate"), button:has-text("Deactivate")').click();
    await this.waitForToastSuccess();
  }

  /**
   * Delete a user
   */
  async deleteUser(emailOrName: string): Promise<void> {
    const row = this.getUserRow(emailOrName);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
  }

  /**
   * Get user count
   */
  async getUserCount(): Promise<number> {
    return await this.getTableRowCount();
  }

  /**
   * Check if user is active
   */
  async isUserActive(emailOrName: string): Promise<boolean> {
    const row = this.getUserRow(emailOrName);
    const statusBadge = row.locator('.badge, [data-status]');
    const status = await statusBadge.textContent();
    return status?.toLowerCase().includes('active') || false;
  }
}
