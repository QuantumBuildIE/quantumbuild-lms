import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Dashboard page object - module selector with charts
 */
export class DashboardPage extends BasePage {
  readonly pageTitle: Locator;
  readonly stockModuleCard: Locator;
  readonly proposalsModuleCard: Locator;
  readonly siteAttendanceModuleCard: Locator;
  readonly adminModuleCard: Locator;
  readonly userDropdown: Locator;
  readonly logoutButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.stockModuleCard = page.locator('[data-module="stock"], a[href*="/stock"]:has-text("Stock")');
    this.proposalsModuleCard = page.locator('[data-module="proposals"], a[href*="/proposals"]:has-text("Proposals")');
    this.siteAttendanceModuleCard = page.locator('[data-module="site-attendance"], a[href*="/site-attendance"]:has-text("Attendance")');
    this.adminModuleCard = page.locator('[data-module="admin"], a[href*="/admin"]:has-text("Admin")');
    this.userDropdown = page.locator('[data-testid="user-dropdown"], button:has(.avatar), [aria-label*="user"]');
    this.logoutButton = page.locator('button:has-text("Logout"), button:has-text("Sign out"), a:has-text("Logout")');
  }

  /**
   * Navigate to the dashboard
   */
  async goto(): Promise<void> {
    await this.page.goto('/dashboard');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Stock Management module
   */
  async goToStock(): Promise<void> {
    await this.stockModuleCard.click();
    await this.page.waitForURL(/\/stock/);
  }

  /**
   * Navigate to Proposals module
   */
  async goToProposals(): Promise<void> {
    await this.proposalsModuleCard.click();
    await this.page.waitForURL(/\/proposals/);
  }

  /**
   * Navigate to Site Attendance module
   */
  async goToSiteAttendance(): Promise<void> {
    await this.siteAttendanceModuleCard.click();
    await this.page.waitForURL(/\/site-attendance/);
  }

  /**
   * Navigate to Admin module
   */
  async goToAdmin(): Promise<void> {
    await this.adminModuleCard.click();
    await this.page.waitForURL(/\/admin/);
  }

  /**
   * Open user dropdown menu
   */
  async openUserDropdown(): Promise<void> {
    await this.userDropdown.click();
  }

  /**
   * Logout from the application
   */
  async logout(): Promise<void> {
    await this.openUserDropdown();
    await this.logoutButton.click();
    await this.page.waitForURL(/\/login/);
  }

  /**
   * Assert that we're on the dashboard
   */
  async assertOnDashboard(): Promise<void> {
    await this.assertUrl(/\/dashboard/);
    await expect(this.pageTitle).toBeVisible();
  }

  /**
   * Check if a module is visible/accessible
   */
  async isModuleVisible(module: 'stock' | 'proposals' | 'site-attendance' | 'admin'): Promise<boolean> {
    const moduleMap = {
      stock: this.stockModuleCard,
      proposals: this.proposalsModuleCard,
      'site-attendance': this.siteAttendanceModuleCard,
      admin: this.adminModuleCard,
    };
    return await moduleMap[module].isVisible();
  }
}
