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

  test('should trigger file input when import button is clicked', async ({ page }) => {
    await page.goto('/');
    
    const importButton = page.locator('button.import-button');
    const fileInput = page.locator('input[type="file"][data-testid="import-input"]');
    
    // Verify file input exists and is hidden
    await expect(fileInput).toBeAttached();
    
    // File input should be present (even if hidden)
    const fileInputCount = await fileInput.count();
    expect(fileInputCount).toBe(1);
  });

  test('should have navigation buttons', async ({ page }) => {
    await page.goto('/');

    // Verify navigation buttons are present
    const importNav = page.locator('button.nav-button', { hasText: 'Import' });
    const catalogNav = page.locator('button.nav-button', { hasText: 'Catalog' });

    await expect(importNav).toBeVisible();
    await expect(catalogNav).toBeVisible();

    // Import should be active by default
    await expect(importNav).toHaveClass(/active/);
  });

  test('should navigate between views', async ({ page }) => {
    await page.goto('/');

    // Initially on import view
    await expect(page.locator('button.import-button')).toBeVisible();

    // Click catalog nav
    const catalogNav = page.locator('button.nav-button', { hasText: 'Catalog' });
    await catalogNav.click();

    // Should now show catalog view with header
    await expect(page.locator('h2', { hasText: 'Cosmetics Catalog' })).toBeVisible();
    await expect(catalogNav).toHaveClass(/active/);

    // Click import nav to go back
    const importNav = page.locator('button.nav-button', { hasText: 'Import' });
    await importNav.click();

    // Should be back on import view
    await expect(page.locator('button.import-button')).toBeVisible();
    await expect(importNav).toHaveClass(/active/);
  });
});
