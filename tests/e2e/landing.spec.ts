import { expect, test } from '@playwright/test';

test('landing page renders welcome heading', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });

  await expect(page.getByRole('heading', { name: 'Repo Mod Composer' })).toBeVisible({ timeout: 30_000 });
});
