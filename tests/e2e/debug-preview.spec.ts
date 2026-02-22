import { test } from "@playwright/test";

test("debug preview issue", async ({ page }) => {
  // Collect console logs
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
  
  console.log('\n=== Clicking Load Mods button ===');
  await page.getByTestId('load-mods-button').click();
  await page.waitForTimeout(1000);
  
  console.log('\n=== Uploading .unitypackage file ===');
  const fileInput = page.getByTestId('mod-file-input');
  await fileInput.setInputFiles('tests/e2e/fixtures/morehead-1.3.unitypackage');
  
  console.log('\n=== Waiting for scan to complete ===');
  // Wait for the scan to actually complete, not just timeout
  await page.waitForTimeout(1000);
  const statusLocator = page.getByTestId('mod-file-status');
  await statusLocator.waitFor({ state: 'visible' });
  
  // Wait until scan is done (status contains "saved" or "complete")
  let attempts = 0;
  while (attempts < 30) {
    const status = await statusLocator.innerText();
    console.log(`Scan status check ${attempts}: ${status}`);
    if (status.includes('saved') || status.includes('complete') || status.includes('Scan complete')) {
      break;
    }
    await page.waitForTimeout(1000);
    attempts++;
  }
  
  const finalStatus = await statusLocator.innerText();
  console.log(`\n=== Final Upload status: ${finalStatus} ===`);
  
  console.log('\n=== Closing modal ===');
  await page.getByTestId('wizard-close').click();
  await page.waitForTimeout(2000);
  
  console.log('\n=== Checking preview status ===');
  const previewStatus = await page.getByTestId('preview-status').innerText();
  console.log(`Preview status: ${previewStatus}`);
  
  console.log('\n=== Waiting for any additional logs ===');
  await page.waitForTimeout(3000);
});
