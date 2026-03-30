import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';
import { ROUTES, TIMEOUTS } from '../../fixtures/test-constants';

/**
 * Employee portal page at /toolbox-talks
 * Shows My Learnings with tabs: Pending, In Progress, Overdue, Completed, All
 */
export class EmployeePortalPage extends BasePage {
  readonly heading: Locator;
  readonly pendingTab: Locator;
  readonly inProgressTab: Locator;
  readonly overdueTab: Locator;
  readonly completedTab: Locator;
  readonly allTab: Locator;

  constructor(page: Page) {
    super(page);
    this.heading = page.locator('h1:has-text("My Learnings"), h1:has-text("Learnings")');
    this.pendingTab = page.locator('[role="tab"]:has-text("Pending"), button:has-text("Pending")');
    this.inProgressTab = page.locator('[role="tab"]:has-text("In Progress"), button:has-text("In Progress")');
    this.overdueTab = page.locator('[role="tab"]:has-text("Overdue"), button:has-text("Overdue")');
    this.completedTab = page.locator('[role="tab"]:has-text("Completed"), button:has-text("Completed")');
    this.allTab = page.locator('[role="tab"]:has-text("All"), button:has-text("All")');
  }

  async navigateTo(): Promise<void> {
    await this.page.goto(ROUTES.toolboxTalks.employeePortal);
    await this.waitForPageLoad();
  }

  // ---------------------------------------------------------------------------
  // Tab navigation
  // ---------------------------------------------------------------------------

  async selectTab(tab: 'Pending' | 'In Progress' | 'Overdue' | 'Completed' | 'All'): Promise<void> {
    const tabMap: Record<string, Locator> = {
      'Pending': this.pendingTab,
      'In Progress': this.inProgressTab,
      'Overdue': this.overdueTab,
      'Completed': this.completedTab,
      'All': this.allTab,
    };
    await tabMap[tab].click();
    await this.page.waitForTimeout(300);
  }

  // ---------------------------------------------------------------------------
  // Pending talks
  // ---------------------------------------------------------------------------

  async getPendingTalkCount(): Promise<number> {
    await this.selectTab('Pending');
    // Count the talk cards/rows in the current tab panel
    const cards = this.page.locator('[role="tabpanel"] a, [role="tabpanel"] [data-testid="talk-card"], [role="tabpanel"] tr');
    const count = await cards.count();
    // If tabpanel is empty, also check for badge count on tab itself
    if (count === 0) {
      const badge = this.pendingTab.locator('span');
      const badgeText = await badge.textContent().catch(() => '0');
      const num = parseInt(badgeText || '0', 10);
      return isNaN(num) ? 0 : num;
    }
    return count;
  }

  async clickFirstPendingTalk(): Promise<void> {
    await this.selectTab('Pending');
    const firstCard = this.page.locator(
      '[role="tabpanel"] a, [role="tabpanel"] [data-testid="talk-card"], [role="tabpanel"] tr'
    ).first();
    await expect(firstCard).toBeVisible({ timeout: TIMEOUTS.medium });
    await firstCard.click();
    await this.waitForNavigation();
  }

  async clickTalkByTitle(title: string): Promise<void> {
    await this.page.locator(`a:has-text("${title}"), [data-testid="talk-card"]:has-text("${title}")`).first().click();
    await this.waitForNavigation();
  }

  // ---------------------------------------------------------------------------
  // Completed talks
  // ---------------------------------------------------------------------------

  async getCompletedTalkTitles(): Promise<string[]> {
    await this.selectTab('Completed');
    await this.page.waitForTimeout(500);

    const cards = this.page.locator(
      '[role="tabpanel"] a, [role="tabpanel"] [data-testid="talk-card"], [role="tabpanel"] tr'
    );
    const count = await cards.count();
    const titles: string[] = [];

    for (let i = 0; i < count; i++) {
      const text = await cards.nth(i).textContent();
      if (text) {
        // Extract the title — typically the first meaningful text in the card
        const trimmed = text.trim().split('\n')[0].trim();
        if (trimmed) titles.push(trimmed);
      }
    }

    return titles;
  }

  // ---------------------------------------------------------------------------
  // Assertions
  // ---------------------------------------------------------------------------

  async assertOnPortal(): Promise<void> {
    await expect(this.heading).toBeVisible({ timeout: TIMEOUTS.medium });
  }

  async assertNoPendingTalks(): Promise<void> {
    await this.selectTab('Pending');
    const emptyMsg = this.page.locator(':text("No pending"), :text("no learnings"), :text("all caught up")');
    await expect(emptyMsg).toBeVisible({ timeout: TIMEOUTS.medium });
  }
}
