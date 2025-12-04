import { useState, useEffect } from 'react';
import { Cosmetic, Mod, CatalogData } from '../types/electron';

interface CatalogViewProps {
  refreshTrigger?: number;
}

interface CosmeticWithMod extends Cosmetic {
  mod?: Mod;
}

function CatalogView({ refreshTrigger }: CatalogViewProps) {
  const [cosmetics, setCosmetics] = useState<CosmeticWithMod[]>([]);
  const [mods, setMods] = useState<Mod[]>([]);
  const [totalMods, setTotalMods] = useState(0);
  const [totalCosmetics, setTotalCosmetics] = useState(0);
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadCatalog = async () => {
    if (!window.electronAPI) {
      // Running outside Electron (web browser), show placeholder
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const data: CatalogData = await window.electronAPI.getCatalog();
      setMods(data.mods);
      setTotalMods(data.mods.length);
      setTotalCosmetics(data.cosmetics.length);
      
      // Enrich cosmetics with mod data
      const cosmeticsWithMods: CosmeticWithMod[] = data.cosmetics.map(cosmetic => ({
        ...cosmetic,
        mod: data.mods.find(m => m.id === cosmetic.mod_id),
      }));
      setCosmetics(cosmeticsWithMods);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load catalog');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSearch = async () => {
    if (!window.electronAPI) return;
    
    if (!searchQuery.trim()) {
      await loadCatalog();
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      // Ensure mods are loaded before searching
      if (mods.length === 0) {
        const data: CatalogData = await window.electronAPI.getCatalog();
        setMods(data.mods);
        setTotalMods(data.mods.length);
        setTotalCosmetics(data.cosmetics.length);
        // Do NOT setCosmetics here, only update mods
      }
      const results = await window.electronAPI.searchCosmetics(searchQuery);
      const cosmeticsWithMods: CosmeticWithMod[] = results.map(cosmetic => ({
        ...cosmetic,
        mod: (mods.length > 0 ? mods : (await window.electronAPI.getCatalog()).mods).find(m => m.id === cosmetic.mod_id),
      }));
      setCosmetics(cosmeticsWithMods);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadCatalog();
  }, [refreshTrigger]);

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value);
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    handleSearch();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSearch();
    }
  };

  if (!window.electronAPI) {
    return (
      <div className="catalog-placeholder">
        <p className="placeholder-text">
          Catalog view is available in the desktop application.
        </p>
      </div>
    );
  }

  return (
    <div className="catalog-view">
      <div className="catalog-header">
        <h2>Cosmetics Catalog</h2>
        <div className="catalog-stats">
          {searchQuery ? (
            <>{cosmetics.length} of {totalCosmetics} cosmetic(s) • {totalMods} mod(s)</>
          ) : (
            <>{totalMods} mod(s) • {totalCosmetics} cosmetic(s)</>
          )}
        </div>
      </div>

      <form className="search-bar" onSubmit={handleSearchSubmit}>
        <input
          type="text"
          placeholder="Search cosmetics..."
          value={searchQuery}
          onChange={handleSearchChange}
          onKeyDown={handleKeyDown}
          className="search-input"
        />
        <button type="submit" className="search-button">
          🔍 Search
        </button>
        {searchQuery && (
          <button
            type="button"
            className="clear-button"
            onClick={() => {
              setSearchQuery('');
              loadCatalog();
            }}
          >
            ✕ Clear
          </button>
        )}
      </form>

      {isLoading && (
        <div className="loading-indicator">Loading...</div>
      )}

      {error && (
        <div className="error-message">
          ❌ {error}
        </div>
      )}

      {!isLoading && !error && cosmetics.length === 0 && (
        <div className="empty-catalog">
          <p className="placeholder-text">
            {searchQuery 
              ? 'No cosmetics match your search.' 
              : 'Import mod ZIP files to populate the catalog.'}
          </p>
        </div>
      )}

      {!isLoading && cosmetics.length > 0 && (
        <div className="cosmetics-grid">
          {cosmetics.map((cosmetic) => (
            <div key={cosmetic.id} className="cosmetic-card">
              <div className="cosmetic-name">{cosmetic.display_name}</div>
              <div className="cosmetic-details">
                <span className="cosmetic-type">{cosmetic.type}</span>
                {cosmetic.mod && (
                  <span className="cosmetic-mod">
                    by {cosmetic.mod.author}
                  </span>
                )}
              </div>
              <div className="cosmetic-filename">{cosmetic.filename}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default CatalogView;
