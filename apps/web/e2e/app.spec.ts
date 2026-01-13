import { test, expect } from '@playwright/test';

/**
 * E2E tests for the cosmetic viewer web app.
 * These tests verify the entire user flow from browsing mods to viewing 3D models.
 */

test.describe('Web App E2E Tests', () => {
  // Verify Playwright is configured correctly without requiring a running server
  test('verifies Playwright setup', async ({ page }) => {
    // This test verifies that Playwright is installed and working
    expect(page).toBeTruthy();
    expect(await page.evaluate(() => navigator.userAgent)).toBeTruthy();
  });

  // Phase 0: Basic UI tests - ENABLED after #83
  test('navigates to home page and displays stub UI', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/REPO Cosmetic Viewer/i);

    // Check header is visible
    const header = page.locator('h1');
    await expect(header).toHaveText('REPO Cosmetic Viewer');

    // Check welcome message
    await expect(page.locator('text=Welcome!')).toBeVisible();
    await expect(page.locator('text=Phase 0 - Setup Complete')).toBeVisible();
  });

  // TODO Phase 1: Enable when mod list is implemented
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
