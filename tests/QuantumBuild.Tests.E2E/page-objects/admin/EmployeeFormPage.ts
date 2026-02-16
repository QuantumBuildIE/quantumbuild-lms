import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Employee form page object (create/edit)
 */
export class EmployeeFormPage extends BasePage {
  readonly pageTitle: Locator;
  readonly firstNameInput: Locator;
  readonly lastNameInput: Locator;
  readonly emailInput: Locator;
  readonly phoneInput: Locator;
  readonly siteSelect: Locator;
  readonly jobTitleInput: Locator;
  readonly employeeNumberInput: Locator;
  readonly startDateInput: Locator;
  readonly isActiveCheckbox: Locator;
  readonly saveButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.firstNameInput = page.locator('[name="firstName"], #firstName');
    this.lastNameInput = page.locator('[name="lastName"], #lastName');
    this.emailInput = page.locator('[name="email"], #email');
    this.phoneInput = page.locator('[name="phone"], #phone');
    this.siteSelect = page.locator('[name="siteId"], #siteId');
    this.jobTitleInput = page.locator('[name="jobTitle"], #jobTitle');
    this.employeeNumberInput = page.locator('[name="employeeNumber"], #employeeNumber');
    this.startDateInput = page.locator('[name="startDate"], #startDate');
    this.isActiveCheckbox = page.locator('[name="isActive"], #isActive');
    this.saveButton = page.locator('button[type="submit"], button:has-text("Save")');
    this.cancelButton = page.locator('button:has-text("Cancel"), a:has-text("Cancel")');
  }

  /**
   * Navigate to create new employee
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/employees/new');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to edit employee
   */
  async gotoEdit(id: string): Promise<void> {
    await this.page.goto(`/admin/employees/${id}/edit`);
    await this.waitForPageLoad();
  }

  /**
   * Fill the employee form
   */
  async fillForm(data: {
    firstName: string;
    lastName: string;
    email?: string;
    phone?: string;
    siteId?: string;
    jobTitle?: string;
    employeeNumber?: string;
    startDate?: string;
    isActive?: boolean;
  }): Promise<void> {
    await this.firstNameInput.fill(data.firstName);
    await this.lastNameInput.fill(data.lastName);

    if (data.email) {
      await this.emailInput.fill(data.email);
    }
    if (data.phone) {
      await this.phoneInput.fill(data.phone);
    }
    if (data.siteId) {
      await this.selectOption(this.siteSelect, data.siteId);
    }
    if (data.jobTitle) {
      await this.jobTitleInput.fill(data.jobTitle);
    }
    if (data.employeeNumber) {
      await this.employeeNumberInput.fill(data.employeeNumber);
    }
    if (data.startDate) {
      await this.startDateInput.fill(data.startDate);
    }
    if (data.isActive !== undefined) {
      if (data.isActive) {
        await this.isActiveCheckbox.check();
      } else {
        await this.isActiveCheckbox.uncheck();
      }
    }
  }

  /**
   * Save the form
   */
  async save(): Promise<void> {
    await this.saveButton.click();
  }

  /**
   * Save and wait for success
   */
  async saveAndWaitForSuccess(): Promise<void> {
    await this.save();
    await this.waitForToastSuccess();
  }

  /**
   * Cancel form
   */
  async cancel(): Promise<void> {
    await this.cancelButton.click();
  }
}
