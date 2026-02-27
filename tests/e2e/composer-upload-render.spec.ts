/* global process */

import fs from 'node:fs';
import { expect, test } from '@playwright/test';

const modZipPath = process.env.E2E_MOD_ZIP_PATH;
const hasModZip = !!modZipPath && fs.existsSync(modZipPath);
const modZipFilePath = modZipPath ?? '';

test.describe('composer upload and render', () => {
  test.skip(!hasModZip, 'Set E2E_MOD_ZIP_PATH to a real mod zip path to run this scenario.');

  test('uploads mod, toggles an asset, and renders preview', async ({ page }) => {
    test.setTimeout(180_000);

    await page.addInitScript(() => {
      globalThis.indexedDB?.deleteDatabase('asset-store-v1');
    });

    await page.goto('/', { waitUntil: 'domcontentloaded' });

    await expect(page.getByRole('heading', { name: 'Repo Mod Composer' })).toBeVisible({ timeout: 30_000 });

    const fileInput = page.getByTestId('mod-zip-input');
    await fileInput.setInputFiles(modZipFilePath);

    const uploadStatus = page.getByTestId('upload-status');
    await expect(uploadStatus).toContainText('Imported', { timeout: 120_000 });

    const firstAsset = page.getByTestId('asset-item').first();
    await expect(firstAsset).toBeVisible({ timeout: 30_000 });

    const firstAssetCheckbox = firstAsset.locator('input[type="checkbox"]');
    const initiallyChecked = await firstAssetCheckbox.isChecked();
    await firstAssetCheckbox.click();
    if (initiallyChecked) {
      await expect(firstAssetCheckbox).not.toBeChecked({ timeout: 30_000 });
    } else {
      await expect(firstAssetCheckbox).toBeChecked({ timeout: 30_000 });
    }

    await firstAssetCheckbox.click();
    if (initiallyChecked) {
      await expect(firstAssetCheckbox).toBeChecked({ timeout: 30_000 });
    } else {
      await expect(firstAssetCheckbox).not.toBeChecked({ timeout: 30_000 });
    }

    const composeStatus = page.getByTestId('compose-status');
    await expect(composeStatus).toContainText('Composed', { timeout: 120_000 });

    const previewCanvas = page.getByTestId('preview-host').locator('canvas');
    await expect(previewCanvas).toBeVisible({ timeout: 60_000 });

    await expect(page.getByTestId('diagnostics-list')).not.toContainText('Preview render failed');
  });
});
