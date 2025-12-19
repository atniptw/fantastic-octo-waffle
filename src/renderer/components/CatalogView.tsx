import { useMemo, useState } from 'react';
import type { Cosmetic, Mod } from '@/shared/types';

interface CatalogViewProps {
  mods?: Mod[];
  cosmetics?: Cosmetic[];
}

interface CosmeticWithMod extends Cosmetic {
  mod?: Mod;
}

function CatalogView({ mods = [], cosmetics = [] }: CatalogViewProps) {
  const [searchQuery, setSearchQuery] = useState('');

  const cosmeticsWithMods: CosmeticWithMod[] = useMemo(() => {
    const modMap = new Map(mods.map((mod) => [mod.id, mod]));
    return cosmetics.map((cosmetic) => ({
      ...cosmetic,
      mod: modMap.get(cosmetic.mod_id),
    }));
  }, [mods, cosmetics]);

  const filteredCosmetics = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    if (!query) return cosmeticsWithMods;
    return cosmeticsWithMods.filter((cosmetic) => {
      return (
        cosmetic.display_name.toLowerCase().includes(query) ||
        cosmetic.filename.toLowerCase().includes(query) ||
        cosmetic.type.toLowerCase().includes(query) ||
        cosmetic.mod?.mod_name.toLowerCase().includes(query) ||
        cosmetic.mod?.author.toLowerCase().includes(query)
      );
    });
  }, [cosmeticsWithMods, searchQuery]);

  const totalMods = mods.length;
  const totalCosmetics = cosmetics.length;

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value);
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault();
    }
  };

  return (
    <div className="catalog-view">
      <div className="catalog-header">
        <h2>Cosmetics Catalog</h2>
        <div className="catalog-stats">
          {searchQuery ? (
            <>
              {filteredCosmetics.length} of {totalCosmetics} cosmetic(s) • {totalMods} mod(s)
            </>
          ) : (
            <>
              {totalMods} mod(s) • {totalCosmetics} cosmetic(s)
            </>
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
          aria-label="Search cosmetics"
        />
        <button type="submit" className="search-button">
          🔍 Search
        </button>
        {searchQuery && (
          <button type="button" className="clear-button" onClick={() => setSearchQuery('')}>
            ✕ Clear
          </button>
        )}
      </form>

      {cosmeticsWithMods.length === 0 && (
        <div className="empty-catalog">
          <p className="placeholder-text">Import mod ZIP files to populate the catalog.</p>
        </div>
      )}

      {cosmeticsWithMods.length > 0 && filteredCosmetics.length === 0 && (
        <div className="empty-catalog">
          <p className="placeholder-text">No cosmetics match your search.</p>
        </div>
      )}

      {filteredCosmetics.length > 0 && (
        <div className="cosmetics-grid">
          {filteredCosmetics.map((cosmetic) => (
            <div key={cosmetic.id} className="cosmetic-card">
              <div className="cosmetic-name">{cosmetic.display_name}</div>
              <div className="cosmetic-details">
                <span className="cosmetic-type">{cosmetic.type}</span>
                {cosmetic.mod && <span className="cosmetic-mod">by {cosmetic.mod.author}</span>}
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
