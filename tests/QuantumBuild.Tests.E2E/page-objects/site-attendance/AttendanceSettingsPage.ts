import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Attendance settings page object
 */
export class AttendanceSettingsPage extends BasePage {
  readonly pageTitle: Locator;
  readonly expectedHoursInput: Locator;
  readonly workStartTimeInput: Locator;
  readonly lateThresholdInput: Locator;
  readonly includeSaturdayCheckbox: Locator;
  readonly includeSundayCheckbox: Locator;
  readonly geofenceRadiusInput: Locator;
  readonly noiseThresholdInput: Locator;
  readonly spaGracePeriodInput: Locator;
  readonly enablePushNotificationsCheckbox: Locator;
  readonly enableEmailNotificationsCheckbox: Locator;
  readonly enableSmsNotificationsCheckbox: Locator;
  readonly notificationTitleInput: Locator;
  readonly notificationMessageInput: Locator;
  readonly saveButton: Locator;
  readonly resetButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.expectedHoursInput = page.locator('[name="expectedHoursPerDay"], #expectedHoursPerDay');
    this.workStartTimeInput = page.locator('[name="workStartTime"], #workStartTime');
    this.lateThresholdInput = page.locator('[name="lateThresholdMinutes"], #lateThresholdMinutes');
    this.includeSaturdayCheckbox = page.locator('[name="includeSaturday"], #includeSaturday');
    this.includeSundayCheckbox = page.locator('[name="includeSunday"], #includeSunday');
    this.geofenceRadiusInput = page.locator('[name="geofenceRadiusMeters"], #geofenceRadiusMeters');
    this.noiseThresholdInput = page.locator('[name="noiseThresholdMeters"], #noiseThresholdMeters');
    this.spaGracePeriodInput = page.locator('[name="spaGracePeriodMinutes"], #spaGracePeriodMinutes');
    this.enablePushNotificationsCheckbox = page.locator('[name="enablePushNotifications"], #enablePushNotifications');
    this.enableEmailNotificationsCheckbox = page.locator('[name="enableEmailNotifications"], #enableEmailNotifications');
    this.enableSmsNotificationsCheckbox = page.locator('[name="enableSmsNotifications"], #enableSmsNotifications');
    this.notificationTitleInput = page.locator('[name="notificationTitle"], #notificationTitle');
    this.notificationMessageInput = page.locator('[name="notificationMessage"], #notificationMessage');
    this.saveButton = page.locator('button:has-text("Save"), button[type="submit"]');
    this.resetButton = page.locator('button:has-text("Reset"), button:has-text("Restore Defaults")');
  }

  /**
   * Navigate to attendance settings
   */
  async goto(): Promise<void> {
    await this.page.goto('/site-attendance/settings');
    await this.waitForPageLoad();
  }

  /**
   * Set expected hours per day
   */
  async setExpectedHours(hours: number): Promise<void> {
    await this.expectedHoursInput.fill(hours.toString());
  }

  /**
   * Set work start time
   */
  async setWorkStartTime(time: string): Promise<void> {
    await this.workStartTimeInput.fill(time);
  }

  /**
   * Set late threshold in minutes
   */
  async setLateThreshold(minutes: number): Promise<void> {
    await this.lateThresholdInput.fill(minutes.toString());
  }

  /**
   * Set whether to include Saturday as working day
   */
  async setIncludeSaturday(include: boolean): Promise<void> {
    if (include) {
      await this.includeSaturdayCheckbox.check();
    } else {
      await this.includeSaturdayCheckbox.uncheck();
    }
  }

  /**
   * Set whether to include Sunday as working day
   */
  async setIncludeSunday(include: boolean): Promise<void> {
    if (include) {
      await this.includeSundayCheckbox.check();
    } else {
      await this.includeSundayCheckbox.uncheck();
    }
  }

  /**
   * Set geofence radius
   */
  async setGeofenceRadius(meters: number): Promise<void> {
    await this.geofenceRadiusInput.fill(meters.toString());
  }

  /**
   * Set noise threshold
   */
  async setNoiseThreshold(meters: number): Promise<void> {
    await this.noiseThresholdInput.fill(meters.toString());
  }

  /**
   * Set SPA grace period
   */
  async setSpaGracePeriod(minutes: number): Promise<void> {
    await this.spaGracePeriodInput.fill(minutes.toString());
  }

  /**
   * Enable/disable push notifications
   */
  async setEnablePushNotifications(enable: boolean): Promise<void> {
    if (enable) {
      await this.enablePushNotificationsCheckbox.check();
    } else {
      await this.enablePushNotificationsCheckbox.uncheck();
    }
  }

  /**
   * Enable/disable email notifications
   */
  async setEnableEmailNotifications(enable: boolean): Promise<void> {
    if (enable) {
      await this.enableEmailNotificationsCheckbox.check();
    } else {
      await this.enableEmailNotificationsCheckbox.uncheck();
    }
  }

  /**
   * Enable/disable SMS notifications
   */
  async setEnableSmsNotifications(enable: boolean): Promise<void> {
    if (enable) {
      await this.enableSmsNotificationsCheckbox.check();
    } else {
      await this.enableSmsNotificationsCheckbox.uncheck();
    }
  }

  /**
   * Save settings
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
   * Reset to defaults
   */
  async resetToDefaults(): Promise<void> {
    await this.resetButton.click();
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Update all settings at once
   */
  async updateSettings(data: {
    expectedHours?: number;
    workStartTime?: string;
    lateThreshold?: number;
    includeSaturday?: boolean;
    includeSunday?: boolean;
    geofenceRadius?: number;
    noiseThreshold?: number;
    spaGracePeriod?: number;
  }): Promise<void> {
    if (data.expectedHours !== undefined) {
      await this.setExpectedHours(data.expectedHours);
    }
    if (data.workStartTime) {
      await this.setWorkStartTime(data.workStartTime);
    }
    if (data.lateThreshold !== undefined) {
      await this.setLateThreshold(data.lateThreshold);
    }
    if (data.includeSaturday !== undefined) {
      await this.setIncludeSaturday(data.includeSaturday);
    }
    if (data.includeSunday !== undefined) {
      await this.setIncludeSunday(data.includeSunday);
    }
    if (data.geofenceRadius !== undefined) {
      await this.setGeofenceRadius(data.geofenceRadius);
    }
    if (data.noiseThreshold !== undefined) {
      await this.setNoiseThreshold(data.noiseThreshold);
    }
    if (data.spaGracePeriod !== undefined) {
      await this.setSpaGracePeriod(data.spaGracePeriod);
    }
  }
}
