import { test, expect } from '@playwright/test';

test.describe('Debug Auth', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('debug page content', async ({ page }) => {
    // Log console messages
    page.on('console', msg => console.log('PAGE LOG:', msg.text()));
    
    // Go to products page
    await page.goto('/stock/products');
    
    // Wait a bit for any redirects
    await page.waitForTimeout(3000);
    
    // Log current URL
    console.log('Current URL:', page.url());
    
    // Log localStorage
    const localStorage = await page.evaluate(() => {
      const items: Record<string, string> = {};
      for (let i = 0; i < window.localStorage.length; i++) {
        const key = window.localStorage.key(i);
        if (key) items[key] = window.localStorage.getItem(key) || '';
      }
      return items;
    });
    console.log('localStorage:', localStorage);
    
    // Take screenshot
    await page.screenshot({ path: 'debug-screenshot.png' });
    
    // Check for h1
    const h1Text = await page.locator('h1').first().textContent().catch(() => 'NO H1 FOUND');
    console.log('H1 text:', h1Text);
    
    // Get body text
    const bodyText = await page.locator('body').innerText();
    console.log('Body text (first 500 chars):', bodyText.substring(0, 500));
  });
});
