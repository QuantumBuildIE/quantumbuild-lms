import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Product form page object (create/edit)
 *
 * Actual field names from ProductForm component:
 * - productCode, productName, categoryId, supplierId, unitType
 * - baseRate, costPrice, sellPrice, reorderLevel, reorderQuantity, leadTimeDays
 * - isActive, productType
 */
export class ProductFormPage extends BasePage {
  readonly pageTitle: Locator;
  readonly productCodeInput: Locator;
  readonly productNameInput: Locator;
  readonly categorySelect: Locator;
  readonly supplierSelect: Locator;
  readonly unitTypeSelect: Locator;
  readonly baseRateInput: Locator;
  readonly costPriceInput: Locator;
  readonly sellPriceInput: Locator;
  readonly reorderLevelInput: Locator;
  readonly reorderQuantityInput: Locator;
  readonly isActiveCheckbox: Locator;
  readonly saveButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    // Form uses React Hook Form with shadcn/ui components
    // Input fields have name attributes from the form schema
    this.productCodeInput = page.locator('input[name="productCode"]');
    this.productNameInput = page.locator('input[name="productName"]');
    // Select components use shadcn/ui - need to click trigger then option
    this.categorySelect = page.locator('button:has-text("Select a category"), button[aria-label*="Category"]').first();
    this.supplierSelect = page.locator('button:has-text("Select a supplier"), button:has-text("No supplier")').first();
    this.unitTypeSelect = page.locator('button:has-text("Select unit type"), button:has-text("Each")').first();
    this.baseRateInput = page.locator('input[name="baseRate"]');
    this.costPriceInput = page.locator('input[name="costPrice"]');
    this.sellPriceInput = page.locator('input[name="sellPrice"]');
    this.reorderLevelInput = page.locator('input[name="reorderLevel"]');
    this.reorderQuantityInput = page.locator('input[name="reorderQuantity"]');
    // Checkbox uses shadcn/ui Checkbox component
    this.isActiveCheckbox = page.locator('button[role="checkbox"]');
    // Submit button text is "Create Product" for new, "Update Product" for edit
    this.saveButton = page.locator('button[type="submit"]:has-text("Create Product"), button[type="submit"]:has-text("Update Product")');
    this.cancelButton = page.locator('button:has-text("Cancel")');
  }

  /**
   * Navigate to create new product
   */
  async goto(): Promise<void> {
    await this.page.goto('/stock/products/new');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to edit product
   */
  async gotoEdit(id: string): Promise<void> {
    await this.page.goto(`/stock/products/${id}/edit`);
    await this.waitForPageLoad();
  }

  /**
   * Fill the product form
   *
   * Required fields: productCode, productName, categoryId (select), unitType (select), baseRate
   */
  async fillForm(data: {
    productCode: string;
    productName: string;
    categoryName?: string;  // Text to select from category dropdown
    supplierName?: string;  // Text to select from supplier dropdown
    unitType?: string;      // Text to select from unit type dropdown
    baseRate: number;
    costPrice?: number;
    sellPrice?: number;
    reorderLevel?: number;
    reorderQuantity?: number;
  }): Promise<void> {
    // Fill required text inputs
    await this.productCodeInput.fill(data.productCode);
    await this.productNameInput.fill(data.productName);

    // Select category (required) - click trigger then option
    if (data.categoryName) {
      await this.categorySelect.click();
      await this.page.locator(`[role="option"]:has-text("${data.categoryName}")`).click();
    }

    // Select supplier (optional)
    if (data.supplierName) {
      await this.supplierSelect.click();
      await this.page.locator(`[role="option"]:has-text("${data.supplierName}")`).click();
    }

    // Select unit type (optional, defaults to "Each")
    if (data.unitType) {
      await this.unitTypeSelect.click();
      await this.page.locator(`[role="option"]:has-text("${data.unitType}")`).click();
    }

    // Fill base rate (required)
    await this.baseRateInput.fill(data.baseRate.toString());

    // Fill optional pricing fields
    if (data.costPrice !== undefined) {
      await this.costPriceInput.fill(data.costPrice.toString());
    }
    if (data.sellPrice !== undefined) {
      await this.sellPriceInput.fill(data.sellPrice.toString());
    }
    if (data.reorderLevel !== undefined) {
      await this.reorderLevelInput.fill(data.reorderLevel.toString());
    }
    if (data.reorderQuantity !== undefined) {
      await this.reorderQuantityInput.fill(data.reorderQuantity.toString());
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
