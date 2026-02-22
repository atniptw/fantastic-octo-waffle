import { test } from "@playwright/test";

test("test decoration anchor positioning", async ({ page }) => {
  // Collect all console logs, especially from HhhParser and CompositionService
  page.on('console', msg => {
    const text = msg.text();
    if (text.includes('[HhhParser]') || text.includes('[CompositionService]') || text.includes('anchor') || text.includes('bone')) {
      console.log(`[BROWSER]: ${text}`);
    }
  });
  
  console.log('=== Opening app ===');
  await page.goto('http://localhost:5075/');
  await page.waitForLoadState('networkidle');
  
  console.log('\n=== Uploading files ===');
  await page.getByTestId('load-mods-button').click();
  await page.waitForTimeout(1000);
  
  const fileInput = page.getByTestId('mod-file-input');
  
  // Upload avatar
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.3.unitypackage');
  await page.waitForTimeout(3000);
  
  // Upload decorations
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.4.4.zip');
  await page.waitForTimeout(3000);
  
  await page.getByTestId('wizard-close').click();
  await page.waitForTimeout(2000);
  
  console.log('\n=== Selecting a HEAD decoration ===');
  // Find a head decoration
  const decorations = page.locator('.planner-entry');
  const count = await decorations.count();
  
  let headDecorationIndex = -1;
  for (let i = 0; i < count; i++) {
    const meta = await decorations.nth(i).locator('.planner-entry__meta').innerText();
    if (meta.toLowerCase().includes('head')) {
      const name = await decorations.nth(i).locator('.planner-entry__name').innerText();
      console.log(`Found HEAD decoration: ${name}`);
      headDecorationIndex = i;
      break;
    }
  }
  
  if (headDecorationIndex >= 0) {
    const checkbox = decorations.nth(headDecorationIndex).locator('input[type="checkbox"]');
    await checkbox.check();
    console.log('Checked the decoration, waiting for composition...');
    await page.waitForTimeout(5000);
  }
  
  console.log('\n=== Selecting a NECK decoration ===');
  let neckDecorationIndex = -1;
  for (let i = 0; i < count; i++) {
    const meta = await decorations.nth(i).locator('.planner-entry__meta').innerText();
    if (meta.toLowerCase().includes('neck')) {
      const name = await decorations.nth(i).locator('.planner-entry__name').innerText();
      console.log(`Found NECK decoration: ${name}`);
      neckDecorationIndex = i;
      break;
    }
  }
  
  if (neckDecorationIndex >= 0) {
    const checkbox = decorations.nth(neckDecorationIndex).locator('input[type="checkbox"]');
    await checkbox.check();
    console.log('Checked the decoration, waiting for composition...');
    await page.waitForTimeout(5000);
  }
  
  console.log('\n=== Test complete ===');
});
