import { test, expect } from "@playwright/test";

test("landing page loads and Continue is disabled", async ({ page }) => {
  await page.goto("/");

  await expect(
    page.getByRole("heading", { name: "R.E.P.O Outfit Planner" })
  ).toBeVisible();

  const continueButton = page.getByTestId("continue-button");
  await expect(continueButton).toBeDisabled();
});
