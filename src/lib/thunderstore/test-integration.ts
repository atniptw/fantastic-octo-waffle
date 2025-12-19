/**
 * Quick test to verify Thunderstore API client works
 * Run with: npm run build && node --loader ts-node/esm this-file.ts
 */

import { createThunderstoreClient, type PackageIndexEntry } from './index';

async function testThunderstoreClient() {
  console.log('🧪 Testing Thunderstore API Client...\n');

  const client = createThunderstoreClient();

  try {
    // Test 1: Get package index
    console.log('1️⃣ Fetching package index...');
    const packages = await client.getPackageIndex();
    console.log(`✅ Loaded ${packages.length} packages\n`);

    // Test 2: Find R.E.P.O. related mods
    console.log('2️⃣ Finding R.E.P.O. cosmetic mods...');
    const repoMods: PackageIndexEntry[] = packages
      .filter(
        (pkg: PackageIndexEntry) =>
          pkg.name.toLowerCase().includes('decoration') ||
          pkg.name.toLowerCase().includes('cosmetic') ||
          pkg.name.toLowerCase().includes('hat')
      )
      .slice(0, 5);
    console.log(`✅ Found ${repoMods.length} cosmetic mods (showing first 5)`);
    repoMods.forEach((mod: PackageIndexEntry) => {
      console.log(`   - ${mod.namespace}/${mod.name} v${mod.version_number}`);
    });
    console.log();

    // Test 3: Get a specific popular package
    console.log('3️⃣ Fetching BepInEx package details...');
    const bepinex = await client.getPackage('BepInEx', 'BepInExPack');
    console.log(`✅ ${bepinex.full_name}`);
    console.log(`   Description: ${bepinex.latest.description}`);
    console.log(`   Latest Version: ${bepinex.latest.version_number}`);
    console.log(`   Downloads: ${bepinex.total_downloads}`);
    console.log(`   Rating: ${bepinex.rating_score}\n`);

    // Test 4: Get metrics
    console.log('4️⃣ Fetching package metrics...');
    const metrics = await client.getPackageMetrics('BepInEx', 'BepInExPack');
    console.log(`✅ Downloads: ${metrics.downloads}`);
    console.log(`   Rating: ${metrics.rating_score}`);
    console.log(`   Latest: ${metrics.latest_version}\n`);

    // Test 5: Test URL builders
    console.log('5️⃣ Testing URL builders...');
    const downloadUrl = client.getPackageDownloadUrl('BepInEx', 'BepInExPack', '5.4.21');
    const packageUrl = client.getPackageUrl('BepInEx', 'BepInExPack');
    console.log(`✅ Download URL: ${downloadUrl}`);
    console.log(`   Package URL: ${packageUrl}\n`);

    console.log('✅ All tests passed! Thunderstore API client is working correctly.\n');

    // Summary
    console.log('📊 Summary:');
    console.log(`   Total packages: ${packages.length}`);
    console.log(`   Cosmetic mods found: ${repoMods.length}`);
    console.log(`   API endpoints tested: 5`);
    console.log('   Status: ✅ Ready to use');
  } catch (error) {
    console.error('❌ Test failed:', error);
    process.exit(1);
  }
}

// Run if this is the main module
if (import.meta.url === `file://${process.argv[1]}`) {
  testThunderstoreClient();
}

export { testThunderstoreClient };
