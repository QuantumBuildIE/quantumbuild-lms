import { test, expect } from '../../fixtures/test-fixtures';
import { ToolboxTalkListPage, ContentCreationWizardPage } from '../../page-objects/toolbox-talks';

test.describe('Content Creation Wizard', () => {
  test('admin can navigate to the talks list and open the creation wizard', async ({ adminPage }) => {
    const listPage = new ToolboxTalkListPage(adminPage);
    const wizardPage = new ContentCreationWizardPage(adminPage);

    // Navigate to the talks list
    await listPage.navigateTo();

    // Verify the page loads — look for heading or table/empty state
    await expect(
      adminPage.locator('h1, h2, [data-testid="page-title"]').filter({ hasText: /toolbox|talk|learning/i })
    ).toBeVisible({ timeout: 10_000 });

    // Click the New Talk button
    await listPage.clickNewTalk();

    // Verify the wizard opens at Step 1
    await wizardPage.assertCurrentStep(1);
  });
});
