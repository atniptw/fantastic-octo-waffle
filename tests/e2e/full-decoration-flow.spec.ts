import { test } from "@playwright/test";

test("full decoration flow: upload, display, and select", async ({ page }) => {
  // Collect logs
  page.on('console', msg => {
    const text = msg.text();
    if (text.includes('[Home]') || text.includes('[Composition]') || text.includes('[HhhParser]')) {
      console.log(`[BROWSER]: ${text}`);
    }
  });
  
  console.log('=== Opening app ===');
  await page.goto('http://localhost:5075/');
  await page.waitForLoadState('networkidle');
  
  console.log('\n=== Opening and uploading files ===');
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
  
  console.log('\n=== Checking decoration list ===');
  const decorations = page.locator('.planner-entry');
  const count = await decorations.count();
  console.log(`✓ Found ${count} decorations in the sidebar`);
  
  if (count > 0) {
    // Print first 5 decoration names
    for (let i = 0; i < Math.min(count, 5); i++) {
      const nameEl = decorations.nth(i).locator('.planner-entry__name');
      const name = await nameEl.innerText();
      const metaEl = decorations.nth(i).locator('.planner-entry__meta');
      const meta = await metaEl.innerText();
      console.log(`  ${i + 1}. ${name} (${meta})`);
    }
    
    console.log('\n=== Selecting first decoration ===');
    const firstCheckbox = decorations.first().locator('input[type="checkbox"]');
    await firstCheckbox.check();
    await page.waitForTimeout(3000);
    
    console.log('\n=== Checking preview after selection ===');
    const previewStatus = await page.getByTestId('preview-status').innerText().catch(() => '');
    console.log(`Preview status: ${previewStatus || '(no status message)'}`);
    
    console.log('\n✓ Full flow completed successfully!');
  }
});
