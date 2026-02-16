import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * User form page object (create/edit)
 */
export class UserFormPage extends BasePage {
  readonly pageTitle: Locator;
  readonly firstNameInput: Locator;
  readonly lastNameInput: Locator;
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly confirmPasswordInput: Locator;
  readonly rolesSelect: Locator;
  readonly employeeSelect: Locator;
  readonly isActiveCheckbox: Locator;
  readonly saveButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.firstNameInput = page.locator('[name="firstName"], #firstName');
    this.lastNameInput = page.locator('[name="lastName"], #lastName');
    this.emailInput = page.locator('[name="email"], #email');
    this.passwordInput = page.locator('[name="password"], #password');
    this.confirmPasswordInput = page.locator('[name="confirmPassword"], #confirmPassword');
    this.rolesSelect = page.locator('[name="roles"], #roles');
    this.employeeSelect = page.locator('[name="employeeId"], #employeeId');
    this.isActiveCheckbox = page.locator('[name="isActive"], #isActive');
    this.saveButton = page.locator('button[type="submit"], button:has-text("Save")');
    this.cancelButton = page.locator('button:has-text("Cancel"), a:has-text("Cancel")');
  }

  /**
   * Navigate to create new user
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/users/new');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to edit user
   */
  async gotoEdit(id: string): Promise<void> {
    await this.page.goto(`/admin/users/${id}/edit`);
    await this.waitForPageLoad();
  }

  /**
   * Fill the user form
   */
  async fillForm(data: {
    firstName: string;
    lastName: string;
    email: string;
    password?: string;
    confirmPassword?: string;
    roles?: string[];
    employeeId?: string;
    isActive?: boolean;
  }): Promise<void> {
    await this.firstNameInput.fill(data.firstName);
    await this.lastNameInput.fill(data.lastName);
    await this.emailInput.fill(data.email);

    if (data.password) {
      await this.passwordInput.fill(data.password);
    }
    if (data.confirmPassword) {
      await this.confirmPasswordInput.fill(data.confirmPassword);
    }
    if (data.roles) {
      for (const role of data.roles) {
        await this.selectRole(role);
      }
    }
    if (data.employeeId) {
      await this.selectOption(this.employeeSelect, data.employeeId);
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
   * Select a role (handles multi-select)
   */
  async selectRole(role: string): Promise<void> {
    // Handle multi-select component
    const roleCheckbox = this.page.locator(`[name="roles"] input[value="${role}"], label:has-text("${role}") input[type="checkbox"]`);
    if (await roleCheckbox.isVisible()) {
      await roleCheckbox.check();
    } else {
      // Fallback to select option
      await this.selectOption(this.rolesSelect, role);
    }
  }

  /**
   * Deselect a role
   */
  async deselectRole(role: string): Promise<void> {
    const roleCheckbox = this.page.locator(`[name="roles"] input[value="${role}"], label:has-text("${role}") input[type="checkbox"]`);
    if (await roleCheckbox.isVisible()) {
      await roleCheckbox.uncheck();
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
