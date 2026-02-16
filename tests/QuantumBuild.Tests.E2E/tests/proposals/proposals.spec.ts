import { test, expect } from '../../fixtures/test-fixtures';
import {
  ProposalListPage,
  ProposalFormPage,
  ProposalDetailPage
} from '../../page-objects/proposals';
import { generateTestData, TAGS } from '../../fixtures/test-constants';

// TODO: Re-enable once proposals module E2E tests are updated to match current UI
test.describe.skip('Proposals @smoke', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('should display proposal list', async ({ page }) => {
    const proposalListPage = new ProposalListPage(page);
    await proposalListPage.goto();

    await expect(proposalListPage.pageTitle).toBeVisible();
    await expect(proposalListPage.table).toBeVisible();
  });

  test('should filter proposals by status', async ({ page }) => {
    const proposalListPage = new ProposalListPage(page);
    await proposalListPage.goto();

    await proposalListPage.filterByStatus('Draft');
    await proposalListPage.waitForPageLoad();
  });

  test('should navigate to create proposal', async ({ page }) => {
    const proposalListPage = new ProposalListPage(page);
    await proposalListPage.goto();
    await proposalListPage.clickCreate();

    await expect(page).toHaveURL(/\/proposals\/new/);
  });
});

// TODO: Re-enable once proposals module E2E tests are updated to match current UI
test.describe.skip('Proposal Creation @regression', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('should display proposal form', async ({ page }) => {
    const proposalFormPage = new ProposalFormPage(page);
    await proposalFormPage.goto();

    await expect(proposalFormPage.companySelect).toBeVisible();
    await expect(proposalFormPage.saveButton).toBeVisible();
  });

  test('should allow adding sections', async ({ page }) => {
    const proposalFormPage = new ProposalFormPage(page);
    await proposalFormPage.goto();

    await proposalFormPage.addSection('Test Section', 'Test Description');
    const sectionCount = await proposalFormPage.getSectionCount();
    expect(sectionCount).toBeGreaterThan(0);
  });
});

// TODO: Re-enable once proposals module E2E tests are updated to match current UI
test.describe.skip('Proposals - Office Staff', () => {
  test.use({ storageState: 'playwright/.auth/officestaff.json' });

  test('should be able to view proposals', async ({ page }) => {
    const proposalListPage = new ProposalListPage(page);
    await proposalListPage.goto();

    await expect(proposalListPage.table).toBeVisible();
  });

  test('should be able to create proposals', async ({ page }) => {
    const proposalListPage = new ProposalListPage(page);
    await proposalListPage.goto();

    await expect(proposalListPage.createButton).toBeVisible();
  });
});

// TODO: Re-enable once proposals module E2E tests are updated to match current UI
test.describe.skip('Proposals - Finance User', () => {
  test.use({ storageState: 'playwright/.auth/finance.json' });

  test('should be able to view proposals with costing', async ({ page }) => {
    const proposalListPage = new ProposalListPage(page);
    await proposalListPage.goto();

    await expect(proposalListPage.table).toBeVisible();
  });
});
