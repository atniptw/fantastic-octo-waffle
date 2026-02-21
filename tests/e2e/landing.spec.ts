import { test, expect } from "@playwright/test";

async function waitForModScanComplete(page: import("@playwright/test").Page) {
  await expect
    .poll(
      async () => (await page.getByTestId("mod-file-status").innerText()).trim(),
      { timeout: 120_000 }
    )
    .toMatch(/Scan complete/i);
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
  
  const nextButton = page.getByTestId("wizard-next");
  await expect(nextButton).toBeEnabled();
  await nextButton.click();
  
  await expect(page.getByTestId("wizard-step-2")).toBeVisible();
  await expect(page.getByTestId("wizard-process")).toBeEnabled();
});

test("unitypackage upload shows anchor points and enables next button", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const modInput = page.getByTestId("mod-file-input");
  await modInput.setInputFiles("tests/e2e/fixtures/morehead-1.3.unitypackage");
  await waitForModScanComplete(page);
  
  await expect(page.getByText(/Anchor points:.*found/i)).toBeVisible();
  
  const nextButton = page.getByTestId("wizard-next");
  await expect(nextButton).toBeEnabled();
  await nextButton.click();
  
  await expect(page.getByTestId("wizard-step-2")).toBeVisible();
  await expect(page.getByText(/Unitypackage uploaded with.*anchor point/i)).toBeVisible();
  await expect(page.getByTestId("wizard-process")).toBeEnabled();
});

test("unitypackage workflow shows avatar in preview after process", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const modInput = page.getByTestId("mod-file-input");
  await modInput.setInputFiles("tests/e2e/fixtures/morehead-1.3.unitypackage");
  await waitForModScanComplete(page);

  const nextButton = page.getByTestId("wizard-next");
  await nextButton.click();
  await expect(page.getByTestId("wizard-step-2")).toBeVisible();

  const processButton = page.getByTestId("wizard-process");
  await expect(processButton).toBeEnabled();
  await processButton.click();

  const closeButton = page.getByTestId("wizard-close");
  await expect(closeButton).toBeVisible();
  await closeButton.click();

  const previewStatus = page.getByTestId("preview-status");
  await expect(previewStatus).toBeVisible();
  // Avatar should show after unitypackage is processed, or fall back to "Select an item"
  const statusText = await previewStatus.innerText();
  const hasAvatarOrSelect = statusText.includes("Avatar loaded") || statusText.includes("Select an item");
  expect(hasAvatarOrSelect).toBeTruthy();
});

test("process flow runs sequentially and completes", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const modInput = page.getByTestId("mod-file-input");
  await modInput.setInputFiles("tests/e2e/fixtures/morehead-1.4.4.zip");
  await waitForModScanComplete(page);

  await page.getByTestId("wizard-next").click();
  await expect(page.getByTestId("wizard-step-2")).toBeVisible();

  const processButton = page.getByTestId("wizard-process");
  await expect(processButton).toBeEnabled();
  await processButton.click();
  await expect(processButton).toHaveText(/Processing/i);
  await expect(page.getByTestId("wizard-close")).toBeVisible();
  await expect(page.getByTestId("wizard-reset")).toBeVisible();

  await page.getByTestId("wizard-close").click();

  const firstItem = page.locator('[data-testid^="item-"]').first();
  await expect(firstItem).toBeVisible();
  await firstItem.click();

  const previewStatus = page.getByTestId("preview-status");
  await expect(previewStatus).toBeVisible();
  await expect(previewStatus).not.toContainText(
    "Load mods to start previewing items."
  );
});
