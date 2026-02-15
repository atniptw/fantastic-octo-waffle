import { test, expect } from "@playwright/test";

test("planner loads on home route", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByText("R.E.P.O Outfit Planner")).toBeVisible();
  await expect(page.getByTestId("mods-modal")).toBeHidden();
});

test("base mod upload scans zip and shows list", async ({ page }) => {
  await page.goto("/");

  await page.getByTestId("load-mods-button").click();
  const baseInput = page.getByTestId("base-mod-input");
  await baseInput.setInputFiles("tests/e2e/fixtures/morehead-1.4.4.zip");

  await expect(page.getByTestId("base-mod-status")).toContainText(
    "morehead-1.4.4.zip"
  );
  await expect(page.getByTestId("base-mod-scan-status")).toContainText(
    "Scan complete"
  );
  await page.getByTestId("wizard-next").click();
  await expect(page.getByTestId("wizard-step-2")).toBeVisible();
});
