import { test, expect } from "@playwright/test";

test("landing page loads and Continue is disabled", async ({ page }) => {
  await page.goto("/");

  await expect(
    page.getByRole("heading", { name: "R.E.P.O Outfit Planner" })
  ).toBeVisible();

  const continueButton = page.getByTestId("continue-button");
  await expect(continueButton).toBeDisabled();
});

test("base mod upload enables Continue", async ({ page }) => {
  await page.goto("/");

  const baseInput = page.getByTestId("base-mod-input");
  await baseInput.setInputFiles("tests/e2e/fixtures/morehead-1.4.4.zip");

  await expect(page.getByTestId("base-mod-status")).toContainText(
    "morehead-1.4.4.zip"
  );
  await expect(page.getByTestId("base-mod-scan-status")).toContainText(
    "Scan complete"
  );
  const continueButton = page.getByTestId("continue-button");
  await expect(continueButton).toBeEnabled();
  await continueButton.click();
  await expect(page).toHaveURL(/\/planner$/);
});
