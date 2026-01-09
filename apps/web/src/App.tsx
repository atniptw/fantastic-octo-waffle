import { useEffect } from 'react';
import AppLayout from '@/renderer/components/AppLayout';
import Header from '@/renderer/components/Header';
import ModList from '@/renderer/components/ModList';
import ModDetail from '@/renderer/components/ModDetail';
import { ThunderstoreClient } from '@/lib/thunderstore/client';
import { config } from '@/config';
import { useModData } from '@/renderer/hooks/useModData';

// Use configured Thunderstore base URL and proxy URL for all API calls
const client = new ThunderstoreClient({
  proxyUrl: config.thunderstoreProxyUrl,
  community: config.thunderstoreCommunity,
});

function App() {
  const { mods, selectedMod, loading, error, search, selectMod, retry, loadMods } = useModData(client);

  useEffect(() => {
    loadMods();
  }, [loadMods]);

  return (
    <AppLayout
      header={<Header onSearch={search} />}
      modList={
        <ModList
          mods={mods}
          selectedModId={selectedMod?.id || null}
          onSelectMod={selectMod}
          isLoading={loading}
          error={error || undefined}
          onRetry={retry}
        />
      }
      modDetail={<ModDetail mod={selectedMod} />}
    />
  );
}

export default App;
