import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Site form page object (create/edit)
 */
export class SiteFormPage extends BasePage {
  readonly pageTitle: Locator;
  readonly nameInput: Locator;
  readonly addressInput: Locator;
  readonly cityInput: Locator;
  readonly countyInput: Locator;
  readonly postcodeInput: Locator;
  readonly latitudeInput: Locator;
  readonly longitudeInput: Locator;
  readonly geofenceRadiusInput: Locator;
  readonly isActiveCheckbox: Locator;
  readonly saveButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.nameInput = page.locator('[name="name"], #name');
    this.addressInput = page.locator('[name="address"], #address');
    this.cityInput = page.locator('[name="city"], #city');
    this.countyInput = page.locator('[name="county"], #county');
    this.postcodeInput = page.locator('[name="postcode"], #postcode');
    this.latitudeInput = page.locator('[name="latitude"], #latitude');
    this.longitudeInput = page.locator('[name="longitude"], #longitude');
    this.geofenceRadiusInput = page.locator('[name="geofenceRadius"], #geofenceRadius');
    this.isActiveCheckbox = page.locator('[name="isActive"], #isActive');
    this.saveButton = page.locator('button[type="submit"], button:has-text("Save")');
    this.cancelButton = page.locator('button:has-text("Cancel"), a:has-text("Cancel")');
  }

  /**
   * Navigate to create new site
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/sites/new');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to edit site
   */
  async gotoEdit(id: string): Promise<void> {
    await this.page.goto(`/admin/sites/${id}/edit`);
    await this.waitForPageLoad();
  }

  /**
   * Fill the site form
   */
  async fillForm(data: {
    name: string;
    address?: string;
    city?: string;
    county?: string;
    postcode?: string;
    latitude?: number;
    longitude?: number;
    geofenceRadius?: number;
    isActive?: boolean;
  }): Promise<void> {
    await this.nameInput.fill(data.name);

    if (data.address) {
      await this.addressInput.fill(data.address);
    }
    if (data.city) {
      await this.cityInput.fill(data.city);
    }
    if (data.county) {
      await this.countyInput.fill(data.county);
    }
    if (data.postcode) {
      await this.postcodeInput.fill(data.postcode);
    }
    if (data.latitude !== undefined) {
      await this.latitudeInput.fill(data.latitude.toString());
    }
    if (data.longitude !== undefined) {
      await this.longitudeInput.fill(data.longitude.toString());
    }
    if (data.geofenceRadius !== undefined) {
      await this.geofenceRadiusInput.fill(data.geofenceRadius.toString());
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
