import { test, expect } from '@playwright/test';

test.describe('R.E.P.O. Cosmetic Catalog', () => {
  test('should display the main page with import button', async ({ page }) => {
    await page.goto('/');

    // Verify the header is present
    await expect(page.locator('h1')).toContainText('R.E.P.O. Cosmetic Catalog');
    
    // Verify the description text
    await expect(page.locator('.app-header p')).toContainText('Browse and search cosmetic mods');
  });

  test('should have import button visible and clickable', async ({ page }) => {
    await page.goto('/');

    // Find the import button
    const importButton = page.locator('button.import-button');
    await expect(importButton).toBeVisible();
    await expect(importButton).toContainText('Import Mod ZIP');

    // Click the button (it should be clickable)
    await importButton.click();

    // The button should still be present after clicking
    await expect(importButton).toBeVisible();
  });

  test('should display placeholder text for empty catalog', async ({ page }) => {
    await page.goto('/');

    // Verify placeholder text is shown when no mods are imported
    await expect(page.locator('.placeholder-text')).toContainText(
      'Import mod ZIP files to populate the catalog'
    );
  });

  test('import button click triggers console log', async ({ page }) => {
    // Capture console messages
    const consoleMessages: string[] = [];
    page.on('console', (msg) => consoleMessages.push(msg.text()));

    await page.goto('/');
    
    const importButton = page.locator('button.import-button');
    await importButton.click();

    // Verify the console log was triggered (console.log is synchronous)
    expect(consoleMessages.some((msg) => 
      msg.includes('Import button clicked')
    )).toBe(true);
  });
});
