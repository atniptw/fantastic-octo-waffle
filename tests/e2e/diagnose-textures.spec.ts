import { test } from "@playwright/test";

test("diagnose texture and material content", async ({ page }) => {
  page.on('console', msg => {
    const text = msg.text();
    // Capture detailed context information
    if (text.includes('[HhhParser]') || text.includes('[CompositionService]') || text.includes('Context') || text.includes('JSON')) {
      console.log(`[BROWSER]: ${text}`);
    }
  });
  
  console.log('=== Setting up ===');
  await page.goto('http://localhost:5075/');
  await page.waitForLoadState('networkidle');
  
  // Upload files with verbose logging
  await page.getByTestId('load-mods-button').click();
  await page.waitForTimeout(1000);
  
  const fileInput = page.getByTestId('mod-file-input');
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.3.unitypackage');
  await page.waitForTimeout(3000);
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.4.4.zip');
  await page.waitForTimeout(3000);
  await page.getByTestId('wizard-close').click();
  await page.waitForTimeout(2000);
  
  console.log('\n=== Selecting BlindEye (body decoration with visible texture) ===');
  const decorations = page.locator('.planner-entry');
  
  for (let i = 0; i < await decorations.count(); i++) {
    const name = await decorations.nth(i).locator('.planner-entry__name').innerText();
    const meta = await decorations.nth(i).locator('.planner-entry__meta').innerText();
    
    if (name.toLowerCase().includes('blindeye')) {
      console.log(`Found: ${name} (${meta})`);
      const checkbox = decorations.nth(i).locator('input[type="checkbox"]');
      await checkbox.check();
      
      // Wait for composition
      await page.waitForTimeout(5000);
      
      console.log('✓ BlindEye composition complete (check logs above)');
      break;
    }
  }
});
