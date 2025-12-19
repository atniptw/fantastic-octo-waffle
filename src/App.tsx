import { useState, useEffect } from 'react';
import AppLayout from '@/renderer/components/AppLayout';
import Header from '@/renderer/components/Header';
import ModList from '@/renderer/components/ModList';
import ModDetail from '@/renderer/components/ModDetail';
import { ThunderstoreClient } from '@/lib/thunderstore/client';
import { config } from '@/config';
import { PackageExperimental, PackageListing, PackageIndexEntry } from '@/lib/thunderstore/types';

// Use configured Thunderstore base URL (Cloudflare Worker proxy if set)
const client = new ThunderstoreClient({ baseUrl: config.thunderstoreBaseUrl });

function App() {
  const [allMods, setAllMods] = useState<
    Array<PackageExperimental | PackageListing | PackageIndexEntry>
  >([]);
  const [filteredMods, setFilteredMods] = useState<
    Array<PackageExperimental | PackageListing | PackageIndexEntry>
  >([]);
  const [selectedMod, setSelectedMod] = useState<
    PackageExperimental | PackageListing | PackageIndexEntry | null
  >(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    loadMods();
  }, []);

  const loadMods = async () => {
    setIsLoading(true);
    setErrorMsg(null);
    setSelectedMod(null);
    try {
      // Use V1 listing scoped to R.E.P.O. community
      const v1 = await client.listPackagesV1('repo');
      const v1Filtered = v1.filter((pkg) => {
        const name = pkg.name.toLowerCase();
        // categories can be a string or array; safely convert to string
        const catsStr = Array.isArray(pkg.categories)
          ? pkg.categories.join(',').toLowerCase()
          : typeof pkg.categories === 'string'
            ? pkg.categories.toLowerCase()
            : '';
        const isCosmetic =
          name.includes('decoration') ||
          name.includes('cosmetic') ||
          name.includes('hat') ||
          name.includes('skin') ||
          catsStr.includes('decoration') ||
          catsStr.includes('cosmetic') ||
          catsStr.includes('hat') ||
          catsStr.includes('skin');
        return isCosmetic;
      });
      setAllMods(v1Filtered);
      setFilteredMods(v1Filtered);
    } catch (error) {
      const msg = error instanceof Error ? error.message : 'Unknown error';
      console.error('Failed to load mods:', error);
      setErrorMsg(`Failed to load mods: ${msg}`);
    } finally {
      setIsLoading(false);
    }
  };

  const handleSearch = (query: string) => {
    if (!query.trim()) {
      setFilteredMods(allMods);
      return;
    }

    const lowerQuery = query.toLowerCase();
    const filtered = allMods.filter((mod) => {
      const nameMatch = mod.name.toLowerCase().includes(lowerQuery);
      const ownerOrNamespace = 'owner' in mod ? mod.owner : mod.namespace;
      const ownerMatch = ownerOrNamespace.toLowerCase().includes(lowerQuery);
      const descMatch =
        'latest' in mod && mod.latest?.description
          ? mod.latest.description.toLowerCase().includes(lowerQuery)
          : false;
      return nameMatch || ownerMatch || descMatch;
    });
    setFilteredMods(filtered);
  };

  const handleSelectMod = (mod: PackageExperimental | PackageListing | PackageIndexEntry) => {
    setSelectedMod(mod);
  };

  const handleAnalyzeMod = async (
    mod: PackageExperimental | PackageListing | PackageIndexEntry
  ) => {
    // Placeholder for actual analysis logic
    const id = 'full_name' in mod ? mod.full_name : `${mod.namespace}/${mod.name}`;
    console.log('Analyzing mod:', id);
  };

  return (
    <AppLayout
      header={<Header onSearch={handleSearch} />}
      modList={
        <ModList
          mods={filteredMods}
          selectedModId={
            selectedMod
              ? 'full_name' in selectedMod
                ? selectedMod.full_name
                : `${selectedMod.namespace}/${selectedMod.name}`
              : null
          }
          onSelectMod={handleSelectMod}
          isLoading={isLoading}
          error={errorMsg || undefined}
          onRetry={loadMods}
        />
      }
      modDetail={<ModDetail mod={selectedMod} onAnalyze={handleAnalyzeMod} />}
    />
  );
}

export default App;
