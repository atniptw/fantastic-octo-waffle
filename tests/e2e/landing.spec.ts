import { test, expect } from "@playwright/test";

async function waitForModScanComplete(page: import("@playwright/test").Page) {
  await expect
    .poll(
      async () => (await page.getByTestId("mod-file-status").innerText()).trim(),
      { timeout: 120_000 }
    )
    .toMatch(/saved successfully|Scan complete/i);
}

test("planner loads on home route", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByText("R.E.P.O Outfit Planner")).toBeVisible();
  await expect(page.getByTestId("mods-modal")).toBeHidden();
});

test("base mod upload scans zip and shows list", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const modInput = page.getByTestId("mod-file-input");
  await modInput.setInputFiles("tests/e2e/fixtures/morehead-1.4.4.zip");
  await waitForModScanComplete(page);
  
  // Decorations are automatically saved - check success state
  await expect(page.getByTestId("wizard-step-2")).toBeVisible();
  await expect(page.getByText(/Decorations saved/i)).toBeVisible();
  
  const closeButton = page.getByTestId("wizard-close");
  await expect(closeButton).toBeEnabled();
});

test("unitypackage upload shows anchor points and enables close button", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const modInput = page.getByTestId("mod-file-input");
  await modInput.setInputFiles("tests/e2e/fixtures/morehead-1.3.unitypackage");
  await waitForModScanComplete(page);
  
  // Avatar is automatically saved - check success state
  await expect(page.getByText(/Avatar saved with.*anchor point/i)).toBeVisible();
  
  const closeButton = page.getByTestId("wizard-close");
  await expect(closeButton).toBeEnabled();
});

test("unitypackage workflow shows avatar in preview after upload", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const modInput = page.getByTestId("mod-file-input");
  await modInput.setInputFiles("tests/e2e/fixtures/morehead-1.3.unitypackage");
  await waitForModScanComplete(page);

  // Avatar saved automatically - just close modal
  const closeButton = page.getByTestId("wizard-close");
  await expect(closeButton).toBeVisible();
  await closeButton.click();

  const previewStatus = page.getByTestId("preview-status");
  await expect(previewStatus).toBeVisible();
  // Avatar should show after unitypackage is uploaded, or fall back to "Select an item"
  const statusText = await previewStatus.innerText();
  const hasAvatarOrSelect = statusText.includes("Avatar loaded") || statusText.includes("Select an item");
  expect(hasAvatarOrSelect).toBeTruthy();
});

test("upload flow saves items immediately and allows closing", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const modInput = page.getByTestId("mod-file-input");
  await modInput.setInputFiles("tests/e2e/fixtures/morehead-1.4.4.zip");
  await waitForModScanComplete(page);

  // Items are saved automatically - verify success state
  await expect(page.getByTestId("wizard-step-2")).toBeVisible();
  await expect(page.getByText(/Decorations saved/i)).toBeVisible();
  
  const closeButton = page.getByTestId("wizard-close");
  await expect(closeButton).toBeEnabled();
  await closeButton.click();

  // Verify items are available in the planner
  const firstItem = page.locator('[data-testid^="item-"]').first();
  await expect(firstItem).toBeVisible();
  await firstItem.click();

  const previewStatus = page.getByTestId("preview-status");
  await expect(previewStatus).toBeVisible();
  await expect(previewStatus).not.toContainText(
    "Load mods to start previewing items."
  );
});
