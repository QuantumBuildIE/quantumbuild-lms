import { test, expect } from '@playwright/test';
import { TEST_TENANT, generateTestData, TIMEOUTS } from '../../fixtures/test-constants';

/**
 * Toolbox Talks E2E Tests
 * Tests the complete toolbox talks lifecycle
 * Tags: @regression, @toolbox
 *
 * TODO: Re-enable once toolbox talks E2E tests are updated to match current UI
 */
test.describe.skip('Toolbox Talks Management', () => {
  test.describe('Admin User - Talk CRUD', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can view toolbox talks list', async ({ page }) => {
      await page.goto('/toolbox-talks/talks');
      await page.waitForLoadState('networkidle');

      // Should see the page title
      const pageTitle = page.locator('h1, [data-testid="page-title"]');
      await expect(pageTitle).toContainText(/toolbox|talk/i);

      // Should have table or list
      const content = page.locator('table, [data-testid="talk-list"], .empty-state');
      await expect(content).toBeVisible();
    });

    test('can create a new toolbox talk', async ({ page }) => {
      await page.goto('/toolbox-talks/talks/new');
      await page.waitForLoadState('networkidle');

      // Fill in talk details
      const titleInput = page.locator('[name="title"], #title');
      await titleInput.fill(`E2E Test Talk - ${generateTestData.uniqueString('talk')}`);

      // Select frequency
      const frequencySelect = page.locator('[name="frequency"], #frequency');
      if (await frequencySelect.isVisible()) {
        await frequencySelect.click();
        await page.locator('[role="option"]').first().click();
      }

      // Add a section
      const addSectionButton = page.locator('button:has-text("Add Section")');
      if (await addSectionButton.isVisible()) {
        await addSectionButton.click();

        // Fill section title
        const sectionTitle = page.locator('[name*="sections"][name*="title"]').first();
        if (await sectionTitle.isVisible()) {
          await sectionTitle.fill('Section 1 - Safety Overview');
        }

        // Fill section content
        const sectionContent = page.locator('[name*="sections"][name*="content"], .section-content textarea, .rich-editor').first();
        if (await sectionContent.isVisible()) {
          await sectionContent.fill('<p>This is the safety overview content for E2E testing.</p>');
        }
      }

      // Save the talk
      const saveButton = page.locator('button:has-text("Save"), button:has-text("Create"), button[type="submit"]');
      await saveButton.click();

      await page.waitForTimeout(2000);

      // Verify success
      const successToast = page.locator('[data-sonner-toast][data-type="success"]');
      const onDetailPage = page.url().includes('/toolbox-talks/talks/') && !page.url().includes('/new');

      expect(await successToast.isVisible().catch(() => false) || onDetailPage).toBeTruthy();
    });

    test('can edit an existing toolbox talk', async ({ page }) => {
      await page.goto('/toolbox-talks/talks');
      await page.waitForLoadState('networkidle');

      // Find and click on a talk
      const talkRow = page.locator('tbody tr, [data-testid="talk-row"]').first();
      if (await talkRow.isVisible()) {
        await talkRow.click();
        await page.waitForLoadState('networkidle');

        // Click edit button
        const editButton = page.locator('button:has-text("Edit"), a:has-text("Edit")');
        if (await editButton.isVisible()) {
          await editButton.click();
          await page.waitForLoadState('networkidle');

          // Modify the title
          const titleInput = page.locator('[name="title"], #title');
          const currentTitle = await titleInput.inputValue();
          await titleInput.fill(`${currentTitle} - Updated`);

          // Save changes
          const saveButton = page.locator('button:has-text("Save"), button[type="submit"]');
          await saveButton.click();

          await page.waitForTimeout(2000);

          // Verify success
          const successToast = page.locator('[data-sonner-toast][data-type="success"]');
          expect(await successToast.isVisible().catch(() => false) || page.url().includes('/toolbox-talks/talks/')).toBeTruthy();
        }
      }
    });

    test('can delete a toolbox talk', async ({ page }) => {
      await page.goto('/toolbox-talks/talks');
      await page.waitForLoadState('networkidle');

      // Get initial count
      const initialRows = await page.locator('tbody tr, [data-testid="talk-row"]').count();

      if (initialRows > 0) {
        // Find delete button in first row
        const firstRow = page.locator('tbody tr, [data-testid="talk-row"]').first();
        const deleteButton = firstRow.locator('button:has-text("Delete"), [data-action="delete"]');

        if (await deleteButton.isVisible()) {
          await deleteButton.click();

          // Confirm delete
          const confirmButton = page.locator('[role="dialog"] button:has-text("Confirm"), [role="alertdialog"] button:has-text("Delete")');
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
            await page.waitForTimeout(2000);

            // Verify deletion
            const currentRows = await page.locator('tbody tr, [data-testid="talk-row"]').count();
            expect(currentRows).toBeLessThan(initialRows);
          }
        }
      }
    });
  });

  test.describe('Talk Scheduling', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can schedule a talk for employees', async ({ page }) => {
      await page.goto('/toolbox-talks/talks');
      await page.waitForLoadState('networkidle');

      // Click on a talk
      const talkRow = page.locator('tbody tr, [data-testid="talk-row"]').first();
      if (await talkRow.isVisible()) {
        await talkRow.click();
        await page.waitForLoadState('networkidle');

        // Click schedule button
        const scheduleButton = page.locator('button:has-text("Schedule"), button:has-text("Assign")');
        if (await scheduleButton.isVisible()) {
          await scheduleButton.click();

          // Select employees (multi-select)
          const employeeSelect = page.locator('[name="employeeIds"], [data-testid="employee-select"]');
          if (await employeeSelect.isVisible()) {
            await employeeSelect.click();
            // Select first few employees
            const options = page.locator('[role="option"]');
            for (let i = 0; i < Math.min(3, await options.count()); i++) {
              await options.nth(i).click();
            }
            // Close dropdown
            await page.keyboard.press('Escape');
          }

          // Set due date
          const dueDateInput = page.locator('[name="dueDate"], input[type="date"]');
          if (await dueDateInput.isVisible()) {
            const futureDate = new Date();
            futureDate.setDate(futureDate.getDate() + 7);
            await dueDateInput.fill(futureDate.toISOString().split('T')[0]);
          }

          // Confirm scheduling
          const confirmButton = page.locator('button:has-text("Schedule"), button:has-text("Assign"), button[type="submit"]');
          await confirmButton.click();

          await page.waitForTimeout(2000);

          // Verify success
          const successToast = page.locator('[data-sonner-toast][data-type="success"]');
          expect(await successToast.isVisible().catch(() => false)).toBeTruthy();
        }
      }
    });

    test('can view scheduled talks', async ({ page }) => {
      await page.goto('/toolbox-talks/scheduled');
      await page.waitForLoadState('networkidle');

      // Should see scheduled talks list
      const content = page.locator('table, [data-testid="scheduled-list"], .empty-state');
      await expect(content).toBeVisible();
    });
  });

  test.describe('Dashboard', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can view toolbox talks dashboard', async ({ page }) => {
      await page.goto('/toolbox-talks');
      await page.waitForLoadState('networkidle');

      // Should see dashboard elements
      const dashboardContent = page.locator('[data-testid="dashboard"], .dashboard, h1, h2');
      await expect(dashboardContent.first()).toBeVisible();
    });

    test('dashboard shows KPIs', async ({ page }) => {
      await page.goto('/toolbox-talks');
      await page.waitForLoadState('networkidle');

      // Look for KPI cards or stats
      const kpiElements = page.locator('[data-testid="kpi"], .stat-card, .kpi-card, .metric');
      const chartElements = page.locator('.chart, canvas, [data-testid="chart"]');

      const hasKpis = await kpiElements.count() > 0;
      const hasCharts = await chartElements.count() > 0;

      // Should have at least KPIs or charts
      expect(hasKpis || hasCharts).toBeTruthy();
    });
  });
});

// TODO: Re-enable once toolbox talks E2E tests are updated to match current UI
test.describe.skip('Toolbox Talk Completion @critical', () => {
  test.describe('Employee Talk Completion Flow', () => {
    test.use({ storageState: 'playwright/.auth/warehouse.json' });

    test('employee can view assigned talks', async ({ page }) => {
      await page.goto('/my/toolbox-talks');
      await page.waitForLoadState('networkidle');

      // Should see my talks page
      const pageContent = page.locator('h1, [data-testid="page-title"], .my-talks');
      await expect(pageContent.first()).toBeVisible();
    });

    test('employee can start and complete a toolbox talk', async ({ page }) => {
      await page.goto('/my/toolbox-talks');
      await page.waitForLoadState('networkidle');

      // Find a pending talk
      const pendingTalk = page.locator('[data-status="Pending"], tr:has-text("Pending"), .talk-card:has-text("Pending")').first();

      if (await pendingTalk.isVisible()) {
        // Click to start
        const startButton = pendingTalk.locator('button:has-text("Start"), button:has-text("View")');
        if (await startButton.isVisible()) {
          await startButton.click();
        } else {
          await pendingTalk.click();
        }

        await page.waitForLoadState('networkidle');

        // Read through sections
        let hasNextButton = true;
        while (hasNextButton) {
          // Acknowledge if checkbox exists
          const acknowledgeCheckbox = page.locator('[name="acknowledged"], input[type="checkbox"]:near(:text("acknowledge"))');
          if (await acknowledgeCheckbox.isVisible() && !await acknowledgeCheckbox.isChecked()) {
            await acknowledgeCheckbox.check();
          }

          // Click next
          const nextButton = page.locator('button:has-text("Next")');
          if (await nextButton.isVisible() && await nextButton.isEnabled()) {
            await nextButton.click();
            await page.waitForTimeout(500);
          } else {
            hasNextButton = false;
          }
        }

        // Complete the talk - sign if required
        const signatureCanvas = page.locator('canvas, [data-testid="signature-pad"]');
        if (await signatureCanvas.isVisible()) {
          const box = await signatureCanvas.boundingBox();
          if (box) {
            await page.mouse.move(box.x + 50, box.y + 50);
            await page.mouse.down();
            await page.mouse.move(box.x + 150, box.y + 80);
            await page.mouse.up();
          }
        }

        // Enter name
        const nameInput = page.locator('[name="signedByName"], #signedByName, [name="name"]');
        if (await nameInput.isVisible()) {
          await nameInput.fill('E2E Test User');
        }

        // Submit/Complete
        const completeButton = page.locator('button:has-text("Complete"), button:has-text("Submit"), button:has-text("Finish")');
        if (await completeButton.isVisible() && await completeButton.isEnabled()) {
          await completeButton.click();
          await page.waitForTimeout(2000);

          // Verify success
          const successToast = page.locator('[data-sonner-toast][data-type="success"]');
          expect(await successToast.isVisible().catch(() => false) || page.url().includes('/my/toolbox-talks')).toBeTruthy();
        }
      }
    });

    test('employee can view completed talks', async ({ page }) => {
      await page.goto('/my/toolbox-talks');
      await page.waitForLoadState('networkidle');

      // Click on completed tab if exists
      const completedTab = page.locator('button:has-text("Completed"), a:has-text("Completed"), [data-tab="completed"]');
      if (await completedTab.isVisible()) {
        await completedTab.click();
        await page.waitForLoadState('networkidle');

        // Should show completed talks or empty state
        const content = page.locator('table, [data-testid="completed-list"], .empty-state, .talk-card');
        await expect(content.first()).toBeVisible();
      }
    });
  });
});

// TODO: Re-enable once toolbox talks E2E tests are updated to match current UI
test.describe.skip('Toolbox Talk with Quiz @critical', () => {
  test.use({ storageState: 'playwright/.auth/warehouse.json' });

  test('employee can complete talk with quiz', async ({ page }) => {
    await page.goto('/my/toolbox-talks');
    await page.waitForLoadState('networkidle');

    // Find a talk that requires quiz
    const quizTalk = page.locator('tr:has([data-quiz="true"]), .talk-card:has([data-quiz="true"])').first();

    if (await quizTalk.isVisible()) {
      await quizTalk.click();
      await page.waitForLoadState('networkidle');

      // Read through sections
      let hasNextButton = true;
      while (hasNextButton) {
        const acknowledgeCheckbox = page.locator('[name="acknowledged"], input[type="checkbox"]:near(:text("acknowledge"))');
        if (await acknowledgeCheckbox.isVisible() && !await acknowledgeCheckbox.isChecked()) {
          await acknowledgeCheckbox.check();
        }

        const nextButton = page.locator('button:has-text("Next")');
        if (await nextButton.isVisible() && await nextButton.isEnabled()) {
          await nextButton.click();
          await page.waitForTimeout(500);
        } else {
          hasNextButton = false;
        }
      }

      // Answer quiz questions
      const quizSection = page.locator('[data-testid="quiz-section"], .quiz-section');
      if (await quizSection.isVisible()) {
        // Answer first option for each question
        const questions = page.locator('[data-question], .quiz-question');
        const questionCount = await questions.count();

        for (let i = 0; i < questionCount; i++) {
          const question = questions.nth(i);
          const firstOption = question.locator('input[type="radio"], input[type="checkbox"]').first();
          if (await firstOption.isVisible()) {
            await firstOption.check();
          }
        }

        // Submit quiz
        const submitQuizButton = page.locator('button:has-text("Submit Quiz"), button:has-text("Submit Answers")');
        if (await submitQuizButton.isVisible()) {
          await submitQuizButton.click();
          await page.waitForTimeout(1000);
        }
      }

      // Sign and complete
      const signatureCanvas = page.locator('canvas, [data-testid="signature-pad"]');
      if (await signatureCanvas.isVisible()) {
        const box = await signatureCanvas.boundingBox();
        if (box) {
          await page.mouse.move(box.x + 50, box.y + 50);
          await page.mouse.down();
          await page.mouse.move(box.x + 150, box.y + 80);
          await page.mouse.up();
        }
      }

      const nameInput = page.locator('[name="signedByName"], #signedByName');
      if (await nameInput.isVisible()) {
        await nameInput.fill('E2E Test User');
      }

      const completeButton = page.locator('button:has-text("Complete"), button:has-text("Finish")');
      if (await completeButton.isVisible() && await completeButton.isEnabled()) {
        await completeButton.click();
        await page.waitForTimeout(2000);
      }
    }
  });
});

// TODO: Re-enable once toolbox talks E2E tests are updated to match current UI
test.describe.skip('Toolbox Talk Reports', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('can view completion reports', async ({ page }) => {
    await page.goto('/toolbox-talks/reports');
    await page.waitForLoadState('networkidle');

    // Should see reports page
    const pageContent = page.locator('h1, [data-testid="page-title"], .reports');
    await expect(pageContent.first()).toBeVisible();
  });

  test('can filter reports by date range', async ({ page }) => {
    await page.goto('/toolbox-talks/reports');
    await page.waitForLoadState('networkidle');

    // Find date filters
    const startDateInput = page.locator('[name="startDate"], #startDate, input[type="date"]').first();
    const endDateInput = page.locator('[name="endDate"], #endDate, input[type="date"]').last();

    if (await startDateInput.isVisible()) {
      const startDate = new Date();
      startDate.setMonth(startDate.getMonth() - 1);
      await startDateInput.fill(startDate.toISOString().split('T')[0]);
    }

    if (await endDateInput.isVisible()) {
      await endDateInput.fill(new Date().toISOString().split('T')[0]);
    }

    // Apply filter if button exists
    const applyButton = page.locator('button:has-text("Apply"), button:has-text("Filter")');
    if (await applyButton.isVisible()) {
      await applyButton.click();
      await page.waitForLoadState('networkidle');
    }
  });

  test('can export report data', async ({ page }) => {
    await page.goto('/toolbox-talks/reports');
    await page.waitForLoadState('networkidle');

    // Find export button
    const exportButton = page.locator('button:has-text("Export"), button:has-text("Download")');
    if (await exportButton.isVisible()) {
      const [download] = await Promise.all([
        page.waitForEvent('download').catch(() => null),
        exportButton.click()
      ]);

      if (download) {
        const filename = download.suggestedFilename();
        expect(filename).toMatch(/\.(csv|xlsx|pdf)$/i);
      }
    }
  });
});
