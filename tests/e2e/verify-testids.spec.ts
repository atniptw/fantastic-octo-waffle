import { test } from "@playwright/test";

test("verify data-testid attributes", async ({ page }) => {
  await page.goto('http://localhost:5075/');
  await page.waitForLoadState('networkidle');
  
  // Open modal and upload files
  await page.getByTestId('load-mods-button').click();
  await page.waitForTimeout(1000);
  
  const fileInput = page.getByTestId('mod-file-input');
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.3.unitypackage');
  await page.waitForTimeout(3000);
  
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.4.4.zip');
  await page.waitForTimeout(3000);
  
  await page.getByTestId('wizard-close').click();
  await page.waitForTimeout(2000);
  
  // Check HTML of first decoration entry
  const firstEntry = page.locator('.planner-entry').first();
  const html = await firstEntry.evaluate(el => el.outerHTML);
  console.log('First entry HTML:', html.substring(0, 300));
  
  // Check if data-testid attribute exists
  const hasTestId = await firstEntry.evaluate(el => el.hasAttribute('data-testid'));
  console.log('Has data-testid:', hasTestId);
  
  // Get all attributes
  const attrs = await firstEntry.evaluate(el => {
    const attrs: Record<string, string> = {};
    for (let i = 0; i < el.attributes.length; i++) {
      attrs[el.attributes[i].name] = el.attributes[i].value;
    }
    return attrs;
  });
  console.log('All attributes:', JSON.stringify(attrs, null, 2));
});
