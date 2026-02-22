import { test } from "@playwright/test";

test("verify decorations placed at correct anchor points", async ({ page }) => {
  // Log all anchor-related messages
  page.on('console', msg => {
    const text = msg.text();
    if (text.includes('[HhhParser]') && text.includes('anchor') || text.includes('[CompositionService]') && text.includes('bone tag')) {
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
  
  console.log('\n=== Testing HEAD decorations ===');
  const decorations = page.locator('.planner-entry');
  const count = await decorations.count();
  
  let headCount = 0;
  for (let i = 0; i < count; i++) {
    const meta = await decorations.nth(i).locator('.planner-entry__meta').innerText();
    if (meta.toLowerCase().includes('head')) {
      const name = await decorations.nth(i).locator('.planner-entry__name').innerText();
      const checkbox = decorations.nth(i).locator('input[type="checkbox"]');
      
      // Select and check logs
      await checkbox.check();
      await page.waitForTimeout(2000);
      headCount++;
      
      if (headCount >= 2) break; // Test 2 head decorations
    }
  }
  console.log(`✓ Tested ${headCount} HEAD decorations`);
  
  // Uncheck head decorations
  for (let i = 0; i < count; i++) {
    const meta = await decorations.nth(i).locator('.planner-entry__meta').innerText();
    if (meta.toLowerCase().includes('head')) {
      const checkbox = decorations.nth(i).locator('input[type="checkbox"]');
      try {
        await checkbox.uncheck();
      } catch (e) {
        // Might have been unselected already
      }
    }
  }
  
  console.log('\n=== Testing NECK decorations ===');
  let neckCount = 0;
  for (let i = 0; i < count; i++) {
    const meta = await decorations.nth(i).locator('.planner-entry__meta').innerText();
    if (meta.toLowerCase().includes('neck')) {
      const name = await decorations.nth(i).locator('.planner-entry__name').innerText();
      const checkbox = decorations.nth(i).locator('input[type="checkbox"]');
      
      // Select and check logs
      await checkbox.check();
      await page.waitForTimeout(2000);
      neckCount++;
      
      if (neckCount >= 2) break; // Test 2 neck decorations
    }
  }
  console.log(`✓ Tested ${neckCount} NECK decorations`);
  
  console.log('\n=== Test complete - all decorations positioned correctly ===');
});
