import { expect, test } from '@playwright/test';

test('landing page renders welcome heading', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForLoadState('networkidle');

  await expect(page.getByRole('heading', { name: 'Hello, world!' })).toBeVisible({ timeout: 30_000 });
});
