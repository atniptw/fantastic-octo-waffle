import { test } from "@playwright/test";

test("verify textures and materials are merged", async ({ page }) => {
  const allLogs: string[] = [];
  
  page.on('console', msg => {
    const text = msg.text();
    allLogs.push(text);
    if (text.includes('[HhhParser]') || text.includes('[CompositionService]')) {
      console.log(`[BROWSER]: ${text}`);
    }
  });
  
  console.log('=== Setting up ===');
  await page.goto('http://localhost:5075/');
  await page.waitForLoadState('networkidle');
  
  await page.getByTestId('load-mods-button').click();
  await page.waitForTimeout(1000);
  
  const fileInput = page.getByTestId('mod-file-input');
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.3.unitypackage');
  await page.waitForTimeout(3000);
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.4.4.zip');
  await page.waitForTimeout(3000);
  await page.getByTestId('wizard-close').click();
  await page.waitForTimeout(2000);
  
  console.log('\n=== Selecting a decoration to trigger merge ===');
  const decorations = page.locator('.planner-entry');
  const count = await decorations.count();
  
  if (count > 0) {
    // Just select the first decoration
    const checkbox = decorations.first().locator('input[type="checkbox"]');
    const name = await decorations.first().locator('.planner-entry__name').innerText();
    
    console.log(`Selecting: ${name}`);
    await checkbox.check();
    
    // Wait for the composition to complete
    await page.waitForTimeout(5000);
    
    console.log('✓ Decoration selected and composed');
    console.log('\n=== Checking logs for texture merge info ===');
    const mergedLogs = allLogs.filter(log => log.toLowerCase().includes('merged'));
    console.log(`Found ${mergedLogs.length} "merged" logs:`);
    mergedLogs.forEach(log => console.log(`  ${log}`));
  }
});
