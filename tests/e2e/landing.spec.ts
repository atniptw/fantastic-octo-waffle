import { expect, test } from '@playwright/test';

test('landing page renders welcome heading', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Hello, world!' })).toBeVisible({ timeout: 15_000 });
});
