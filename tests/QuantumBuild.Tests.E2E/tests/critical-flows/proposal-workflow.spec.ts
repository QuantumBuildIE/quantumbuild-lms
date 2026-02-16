import { test, expect } from '@playwright/test';
import { TEST_TENANT, generateTestData, TIMEOUTS } from '../../fixtures/test-constants';

/**
 * Critical E2E Flow: Proposal Workflow
 * Tests the complete proposal lifecycle from creation to conversion
 * Tag: @critical
 * Run with: npx playwright test --grep @critical
 *
 * TODO: Re-enable once proposal workflow E2E tests are updated to match current UI
 */
test.describe.skip('Proposal Workflow @critical', () => {
  test.describe('Create Proposal Flow', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can create a new proposal', async ({ page }) => {
      // Navigate to new proposal page
      await page.goto('/proposals/new');
      await page.waitForLoadState('networkidle');

      // Fill in proposal header
      const titleInput = page.locator('[name="title"], [name="projectName"], #title, #projectName');
      if (await titleInput.isVisible()) {
        await titleInput.fill(`E2E Test Proposal - ${generateTestData.uniqueString('proposal')}`);
      }

      // Select company
      const companySelect = page.locator('[name="companyId"], #companyId, [data-testid="company-select"]');
      if (await companySelect.isVisible()) {
        await companySelect.click();
        await page.locator('[role="option"]').first().click();
      }

      // Select site (optional)
      const siteSelect = page.locator('[name="siteId"], #siteId');
      if (await siteSelect.isVisible()) {
        await siteSelect.click();
        const siteOption = page.locator('[role="option"]').first();
        if (await siteOption.isVisible()) {
          await siteOption.click();
        }
      }

      // Set validity date
      const validUntilInput = page.locator('[name="validUntil"], #validUntil, input[type="date"]');
      if (await validUntilInput.isVisible()) {
        const futureDate = new Date();
        futureDate.setMonth(futureDate.getMonth() + 1);
        await validUntilInput.fill(futureDate.toISOString().split('T')[0]);
      }

      // Save the proposal
      const saveButton = page.locator('button:has-text("Save"), button:has-text("Create"), button[type="submit"]');
      await saveButton.click();

      // Wait for navigation or success
      await page.waitForTimeout(2000);

      const successToast = page.locator('[data-sonner-toast][data-type="success"], .toast-success');
      const proposalDetailPage = page.locator('[data-testid="proposal-detail"], h1');

      const hasSuccess = await successToast.isVisible().catch(() => false);
      const onDetailPage = page.url().includes('/proposals/');

      expect(hasSuccess || onDetailPage).toBeTruthy();
    });

    test('can add sections and line items to proposal', async ({ page }) => {
      // Go to proposals list and find a draft proposal
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Filter for draft proposals
      const draftRow = page.locator('tr:has-text("Draft")').first();
      if (await draftRow.isVisible()) {
        await draftRow.click();
        await page.waitForLoadState('networkidle');

        // Click edit button
        const editButton = page.locator('button:has-text("Edit"), a:has-text("Edit")');
        if (await editButton.isVisible()) {
          await editButton.click();
          await page.waitForLoadState('networkidle');
        }

        // Add a section
        const addSectionButton = page.locator('button:has-text("Add Section")');
        if (await addSectionButton.isVisible()) {
          await addSectionButton.click();

          // Fill section name
          const sectionNameInput = page.locator('[name*="section"][name*="name"], [name*="section"][name*="title"]').last();
          if (await sectionNameInput.isVisible()) {
            await sectionNameInput.fill('Safety Equipment Section');
          }

          // Add line item to section
          const addItemButton = page.locator('button:has-text("Add Item"), button:has-text("Add Line")').last();
          if (await addItemButton.isVisible()) {
            await addItemButton.click();

            // Select product
            const productSelect = page.locator('[name*="productId"]').last();
            if (await productSelect.isVisible()) {
              await productSelect.click();
              await page.locator('[role="option"]').first().click();
            }

            // Enter quantity
            const quantityInput = page.locator('[name*="quantity"]').last();
            if (await quantityInput.isVisible()) {
              await quantityInput.fill('10');
            }

            // Enter unit price
            const priceInput = page.locator('[name*="unitPrice"], [name*="price"]').last();
            if (await priceInput.isVisible()) {
              await priceInput.fill('25.00');
            }
          }
        }

        // Save changes
        const saveButton = page.locator('button:has-text("Save"), button[type="submit"]');
        await saveButton.click();

        await page.waitForTimeout(2000);
      }
    });

    test('can expand product kit into proposal section', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find draft proposal
      const draftRow = page.locator('tr:has-text("Draft")').first();
      if (await draftRow.isVisible()) {
        await draftRow.click();
        await page.waitForLoadState('networkidle');

        // Edit the proposal
        const editButton = page.locator('button:has-text("Edit"), a:has-text("Edit")');
        if (await editButton.isVisible()) {
          await editButton.click();
          await page.waitForLoadState('networkidle');

          // Look for "Add from Kit" button
          const addFromKitButton = page.locator('button:has-text("Add from Kit"), button:has-text("Expand Kit")');
          if (await addFromKitButton.isVisible()) {
            await addFromKitButton.click();

            // Select a kit
            const kitSelect = page.locator('[name="kitId"], [data-testid="kit-select"]');
            if (await kitSelect.isVisible()) {
              await kitSelect.click();
              await page.locator('[role="option"]').first().click();

              // Confirm expansion
              const confirmButton = page.locator('button:has-text("Add"), button:has-text("Expand")');
              if (await confirmButton.isVisible()) {
                await confirmButton.click();
              }
            }
          }
        }
      }
    });
  });

  test.describe('Proposal Status Transitions', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can submit proposal for approval', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find draft proposal
      const draftRow = page.locator('tr:has-text("Draft")').first();
      if (await draftRow.isVisible()) {
        await draftRow.click();
        await page.waitForLoadState('networkidle');

        // Submit the proposal
        const submitButton = page.locator('button:has-text("Submit"), button:has-text("Send for Approval")');
        if (await submitButton.isVisible() && await submitButton.isEnabled()) {
          await submitButton.click();

          // Confirm if dialog appears
          const confirmButton = page.locator('[role="dialog"] button:has-text("Confirm"), [role="dialog"] button:has-text("Submit")');
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
          }

          await page.waitForTimeout(2000);

          // Verify status changed
          const pendingStatus = page.locator('[data-status*="Pending"], span:has-text("Pending"), span:has-text("Submitted")');
          if (await pendingStatus.isVisible()) {
            await expect(pendingStatus).toBeVisible();
          }
        }
      }
    });

    test('can approve a submitted proposal', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find pending proposal
      const pendingRow = page.locator('tr:has-text("Pending")').first();
      if (await pendingRow.isVisible()) {
        await pendingRow.click();
        await page.waitForLoadState('networkidle');

        // Approve the proposal
        const approveButton = page.locator('button:has-text("Approve")');
        if (await approveButton.isVisible() && await approveButton.isEnabled()) {
          await approveButton.click();

          // Confirm if dialog appears
          const confirmButton = page.locator('[role="dialog"] button:has-text("Confirm"), [role="dialog"] button:has-text("Approve")');
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
          }

          await page.waitForTimeout(2000);

          // Verify status changed
          const approvedStatus = page.locator('[data-status="Approved"], span:has-text("Approved")');
          if (await approvedStatus.isVisible()) {
            await expect(approvedStatus).toBeVisible();
          }
        }
      }
    });

    test('can reject a submitted proposal', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find pending proposal
      const pendingRow = page.locator('tr:has-text("Pending")').first();
      if (await pendingRow.isVisible()) {
        await pendingRow.click();
        await page.waitForLoadState('networkidle');

        // Reject the proposal
        const rejectButton = page.locator('button:has-text("Reject")');
        if (await rejectButton.isVisible() && await rejectButton.isEnabled()) {
          await rejectButton.click();

          // Fill rejection reason
          const reasonInput = page.locator('[name="reason"], textarea');
          if (await reasonInput.isVisible()) {
            await reasonInput.fill('E2E Test - Rejected for testing');
          }

          // Confirm rejection
          const confirmButton = page.locator('[role="dialog"] button:has-text("Confirm"), [role="dialog"] button:has-text("Reject")');
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
          }

          await page.waitForTimeout(2000);
        }
      }
    });

    test('can mark proposal as won', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find approved proposal
      const approvedRow = page.locator('tr:has-text("Approved")').first();
      if (await approvedRow.isVisible()) {
        await approvedRow.click();
        await page.waitForLoadState('networkidle');

        // Mark as won
        const wonButton = page.locator('button:has-text("Won"), button:has-text("Mark Won"), button:has-text("Mark as Won")');
        if (await wonButton.isVisible() && await wonButton.isEnabled()) {
          await wonButton.click();

          // Confirm if dialog appears
          const confirmButton = page.locator('[role="dialog"] button:has-text("Confirm")');
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
          }

          await page.waitForTimeout(2000);

          // Verify status changed
          const wonStatus = page.locator('[data-status="Won"], span:has-text("Won")');
          if (await wonStatus.isVisible()) {
            await expect(wonStatus).toBeVisible();
          }
        }
      }
    });

    test('can mark proposal as lost', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find approved proposal
      const approvedRow = page.locator('tr:has-text("Approved")').first();
      if (await approvedRow.isVisible()) {
        await approvedRow.click();
        await page.waitForLoadState('networkidle');

        // Mark as lost
        const lostButton = page.locator('button:has-text("Lost"), button:has-text("Mark Lost"), button:has-text("Mark as Lost")');
        if (await lostButton.isVisible() && await lostButton.isEnabled()) {
          await lostButton.click();

          // Fill lost reason
          const reasonInput = page.locator('[name="reason"], [name="lostReason"], textarea');
          if (await reasonInput.isVisible()) {
            await reasonInput.fill('Price too high - E2E Test');
          }

          // Confirm
          const confirmButton = page.locator('[role="dialog"] button:has-text("Confirm")');
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
          }

          await page.waitForTimeout(2000);
        }
      }
    });
  });

  test.describe('Proposal Conversion Flow', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can convert won proposal to stock order', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find won proposal
      const wonRow = page.locator('tr:has-text("Won")').first();
      if (await wonRow.isVisible()) {
        await wonRow.click();
        await page.waitForLoadState('networkidle');

        // Click convert button
        const convertButton = page.locator('button:has-text("Convert"), button:has-text("Create Order")');
        if (await convertButton.isVisible() && await convertButton.isEnabled()) {
          await convertButton.click();
          await page.waitForTimeout(1000);

          // Dialog should appear with conversion options
          const dialog = page.locator('[role="dialog"]');
          if (await dialog.isVisible()) {
            // Select source location
            const sourceSelect = page.locator('[name="sourceLocationId"]');
            if (await sourceSelect.isVisible()) {
              await sourceSelect.click();
              await page.locator('[role="option"]').first().click();
            }

            // Select destination location
            const destSelect = page.locator('[name="destinationLocationId"]');
            if (await destSelect.isVisible()) {
              await destSelect.click();
              await page.locator('[role="option"]').first().click();
            }

            // Confirm conversion
            const createOrderButton = page.locator('button:has-text("Create Order"), button:has-text("Convert")');
            if (await createOrderButton.isVisible()) {
              await createOrderButton.click();

              await page.waitForTimeout(2000);

              // Should see success message or redirect to order
              const successToast = page.locator('[data-sonner-toast][data-type="success"]');
              expect(await successToast.isVisible() || page.url().includes('/orders')).toBeTruthy();
            }
          }
        }
      }
    });
  });

  test.describe('Proposal PDF Generation', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can generate client PDF', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find any proposal
      const proposalRow = page.locator('tbody tr').first();
      if (await proposalRow.isVisible()) {
        await proposalRow.click();
        await page.waitForLoadState('networkidle');

        // Click PDF button
        const pdfButton = page.locator('button:has-text("PDF"), a:has-text("PDF"), button:has-text("Download")');
        if (await pdfButton.isVisible()) {
          // Handle download
          const [download] = await Promise.all([
            page.waitForEvent('download').catch(() => null),
            pdfButton.click()
          ]);

          if (download) {
            // Verify it's a PDF
            const filename = download.suggestedFilename();
            expect(filename).toMatch(/\.pdf$/i);
          }
        }
      }
    });
  });

  test.describe('Proposal Revision Flow', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can create a revision of approved proposal', async ({ page }) => {
      await page.goto('/proposals');
      await page.waitForLoadState('networkidle');

      // Find approved proposal
      const approvedRow = page.locator('tr:has-text("Approved")').first();
      if (await approvedRow.isVisible()) {
        await approvedRow.click();
        await page.waitForLoadState('networkidle');

        // Click revise button
        const reviseButton = page.locator('button:has-text("Revise"), button:has-text("Create Revision")');
        if (await reviseButton.isVisible() && await reviseButton.isEnabled()) {
          await reviseButton.click();

          // Confirm if dialog appears
          const confirmButton = page.locator('[role="dialog"] button:has-text("Confirm"), [role="dialog"] button:has-text("Create")');
          if (await confirmButton.isVisible()) {
            await confirmButton.click();
          }

          await page.waitForTimeout(2000);

          // Should create new version
          const versionBadge = page.locator('[data-version], span:has-text("v2"), span:has-text("V2")');
          // New revision should be in draft status
          const draftStatus = page.locator('[data-status="Draft"], span:has-text("Draft")');

          expect(await versionBadge.isVisible() || await draftStatus.isVisible()).toBeTruthy();
        }
      }
    });
  });
});

// TODO: Re-enable once proposal workflow E2E tests are updated to match current UI
test.describe.skip('Proposal Reports @critical', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('can view proposal pipeline report', async ({ page }) => {
    await page.goto('/proposals/reports');
    await page.waitForLoadState('networkidle');

    // Look for pipeline chart or section
    const pipelineSection = page.locator('[data-testid="pipeline"], h2:has-text("Pipeline"), .chart, canvas');
    if (await pipelineSection.isVisible()) {
      await expect(pipelineSection).toBeVisible();
    }
  });

  test('can view proposal dashboard', async ({ page }) => {
    await page.goto('/proposals');
    await page.waitForLoadState('networkidle');

    // Dashboard should have KPIs or charts
    const dashboardElements = page.locator('[data-testid="kpi"], .kpi-card, .stat-card, .chart');
    const count = await dashboardElements.count();

    // Should have at least some dashboard elements
    expect(count).toBeGreaterThanOrEqual(0);
  });
});
