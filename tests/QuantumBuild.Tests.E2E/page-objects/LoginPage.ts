import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';
import { TIMEOUTS } from '../fixtures/test-constants';

/**
 * Login page object
 */
export class LoginPage extends BasePage {
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;
  readonly errorMessage: Locator;
  readonly keepMeLoggedInCheckbox: Locator;
  readonly forgotPasswordLink: Locator;

  constructor(page: Page) {
    super(page);
    this.emailInput = page.locator('[name="email"], #email, input[type="email"]');
    this.passwordInput = page.locator('[name="password"], #password, input[type="password"]');
    this.submitButton = page.locator('button[type="submit"]');
    this.errorMessage = page.locator('.error-message, .alert-danger, [role="alert"]');
    this.keepMeLoggedInCheckbox = page.locator('[name="keepMeLoggedIn"], #keepMeLoggedIn, input[type="checkbox"]');
    this.forgotPasswordLink = page.locator('a:has-text("Forgot"), a:has-text("forgot")');
  }

  /**
   * Navigate to the login page
   */
  async goto(): Promise<void> {
    await this.page.goto('/login');
    await this.waitForPageLoad();
  }

  /**
   * Fill in the login form without submitting
   */
  async fillLoginForm(email: string, password: string): Promise<void> {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
  }

  /**
   * Submit the login form
   */
  async submit(): Promise<void> {
    await this.submitButton.click();
  }

  /**
   * Perform login and wait for redirect
   */
  async login(email: string, password: string): Promise<void> {
    await this.fillLoginForm(email, password);
    await this.submit();
  }

  /**
   * Perform login and wait for successful redirect to authenticated area
   */
  async loginAndWaitForSuccess(email: string, password: string): Promise<void> {
    await this.login(email, password);
    await this.page.waitForURL(/\/(dashboard|home|stock|admin|proposals|site-attendance)/, {
      timeout: TIMEOUTS.navigation,
    });
  }

  /**
   * Perform login with "Keep me logged in" option
   */
  async loginWithPersistence(email: string, password: string): Promise<void> {
    await this.fillLoginForm(email, password);
    await this.keepMeLoggedInCheckbox.check();
    await this.submit();
    await this.page.waitForURL(/\/(dashboard|home|stock)/, { timeout: TIMEOUTS.navigation });
  }

  /**
   * Assert that we're on the login page
   */
  async assertOnLoginPage(): Promise<void> {
    await expect(this.emailInput).toBeVisible();
    await expect(this.passwordInput).toBeVisible();
    await expect(this.submitButton).toBeVisible();
  }

  /**
   * Assert that an error message is displayed
   */
  async assertErrorDisplayed(message?: string): Promise<void> {
    await expect(this.errorMessage).toBeVisible({ timeout: TIMEOUTS.short });
    if (message) {
      await expect(this.errorMessage).toContainText(message);
    }
  }

  /**
   * Assert login form is empty
   */
  async assertFormEmpty(): Promise<void> {
    await expect(this.emailInput).toHaveValue('');
    await expect(this.passwordInput).toHaveValue('');
  }

  /**
   * Check if the submit button is disabled
   */
  async isSubmitDisabled(): Promise<boolean> {
    return await this.submitButton.isDisabled();
  }
}
