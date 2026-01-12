import { test, expect } from '@playwright/test';

/**
 * E2E tests for the cosmetic viewer web app.
 * These tests verify the entire user flow from browsing mods to viewing 3D models.
 *
 * NOTE: These tests require the web app to be fully implemented with index.html and UI components.
 * They are currently skipped and will be enabled in Phase 1 when the UI is implemented.
 */

test.describe('Web App E2E Tests', () => {
  // Verify Playwright is configured correctly without requiring a running server
  test('verifies Playwright setup', async ({ page }) => {
    // This test verifies that Playwright is installed and working
    expect(page).toBeTruthy();
    expect(await page.evaluate(() => navigator.userAgent)).toBeTruthy();
  });

  // TODO Phase 1: Enable this test when index.html and UI components are implemented
  // Also uncomment webServer in playwright.config.ts
  test.skip('navigates to home page', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/fantastic-octo-waffle|Cosmetic Viewer/i);
  });

  test.skip('displays mod list', async ({ page }) => {
    await page.goto('/');
    // Wait for mod list to load
    await page.waitForSelector('[data-testid="mod-list"]', { timeout: 5000 });
    await expect(page.locator('[data-testid="mod-list"]')).toBeVisible();
  });

  test.skip('can select and view a mod', async ({ page }) => {
    await page.goto('/');
    // Wait for mod list to load
    await page.waitForSelector('[data-testid="mod-item"]', { timeout: 5000 });
    // Click first mod
    await page.click('[data-testid="mod-item"]:first-child');
    // Verify 3D viewer loads
    await expect(page.locator('[data-testid="three-canvas"]')).toBeVisible();
  });
});
