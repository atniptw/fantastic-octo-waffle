import { test } from "@playwright/test";

test("debug decoration scanning and display", async ({ page }) => {
  // Collect all console logs
  page.on('console', msg => {
    console.log(`[BROWSER ${msg.type()}]: ${msg.text()}`);
  });
  
  // Collect errors
  page.on('pageerror', err => {
    console.log(`[PAGE ERROR]: ${err.message}`);
  });
  
  console.log('=== Opening app ===');
  await page.goto('http://localhost:5075/');
  await page.waitForLoadState('networkidle');
  
  console.log('\n=== Opening Load Mods modal ===');
  await page.getByTestId('load-mods-button').click();
  await page.waitForTimeout(1000);
  
  console.log('\n=== Uploading avatar (.unitypackage) ===');
  const fileInput = page.getByTestId('mod-file-input');
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.3.unitypackage');
  
  console.log('\n=== Waiting for avatar scan to complete ===');
  await page.waitForTimeout(2000);
  const statusLocator = page.getByTestId('mod-file-status');
  await statusLocator.waitFor({ state: 'visible' });
  
  // Wait until avatar scan is done
  let attempts = 0;
  while (attempts < 30) {
    const status = await statusLocator.innerText();
    console.log(`Avatar scan status check ${attempts}: ${status}`);
    if (status.includes('saved') || status.includes('complete') || status.includes('Scan complete')) {
      break;
    }
    await page.waitForTimeout(1000);
    attempts++;
  }
  
  const avatarStatus = await statusLocator.innerText();
  console.log(`\n=== Avatar scan complete: ${avatarStatus} ===`);
  
  console.log('\n=== Uploading decorations (.zip) ===');
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.4.4.zip');
  
  console.log('\n=== Waiting for decoration scan to complete ===');
  await page.waitForTimeout(2000);
  
  // Wait until decoration scan is done
  attempts = 0;
  while (attempts < 30) {
    const status = await statusLocator.innerText();
    console.log(`Decoration scan status check ${attempts}: ${status}`);
    if (status.includes('saved') || status.includes('complete') || status.includes('Scan complete')) {
      break;
    }
    await page.waitForTimeout(1000);
    attempts++;
  }
  
  const decorationStatus = await statusLocator.innerText();
  console.log(`\n=== Decoration scan complete: ${decorationStatus} ===`);
  
  console.log('\n=== Closing modal ===');
  await page.getByTestId('wizard-close').click();
  await page.waitForTimeout(2000);
  
  console.log('\n=== Checking decoration list ===');
  // Look for decoration entries in the sidebar
  const decorationList = page.locator('[data-testid="decoration-entry"]');
  const decorationCount = await decorationList.count();
  console.log(`Decorations found in UI (by testid): ${decorationCount}`);
  
  // Also check by class name
  const decorationListByClass = page.locator('.planner-entry');
  const decorationCountByClass = await decorationListByClass.count();
  console.log(`Decorations found in UI (by class): ${decorationCountByClass}`);
  
  // Check if "No mods loaded yet" message is still showing
  const emptyMessage = page.locator('.planner-empty');
  const emptyMessageVisible = await emptyMessage.isVisible().catch(() => false);
  console.log(`"No mods loaded yet" message visible: ${emptyMessageVisible}`);
  
  // Check the full sidebar HTML
  const sidebar = page.locator('.planner-library');
  const sidebarText = await sidebar.innerText();
  console.log(`Sidebar text: ${sidebarText.substring(0, 200)}`);
  
  if (decorationCountByClass > 0) {
    for (let i = 0; i < Math.min(decorationCountByClass, 5); i++) {
      const text = await decorationListByClass.nth(i).innerText();
      console.log(`  Decoration ${i}: ${text}`);
    }
  }
  
  console.log('\n=== Checking preview status ===');
  const previewStatus = await page.getByTestId('preview-status').innerText();
  console.log(`Preview status: ${previewStatus}`);
  
  console.log('\n=== Waiting for any additional logs ===');
  await page.waitForTimeout(3000);
  
  console.log('\n=== Test complete ===');
});
