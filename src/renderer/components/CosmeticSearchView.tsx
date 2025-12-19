import { useEffect, useMemo, useState } from 'react';
import { createThunderstoreClient, type PackageListing } from '@/lib/thunderstore';

interface ModSummary {
  namespace: string;
  name: string;
  fullName: string;
  author: string;
  version: string;
  downloads: string;
  description: string;
  url: string;
}

type Candidate = { namespace: string; name: string };

function dedupeCandidates(cands: Candidate[]): Candidate[] {
  const map = new Map<string, Candidate>();
  for (const c of cands) {
    const key = `${c.namespace}/${c.name}`;
    if (!map.has(key)) map.set(key, c);
  }
  return Array.from(map.values());
}

export default function CosmeticSearchView() {
  const [v1List, setV1List] = useState<PackageListing[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [mods, setMods] = useState<ModSummary[]>([]);
  const [selected, setSelected] = useState<ModSummary | null>(null);
  const [pageIndex, setPageIndex] = useState(0);

  const client = useMemo(() => createThunderstoreClient(), []);
  const ITEMS_PER_PAGE = 50;

  // Load all V1 packages once on mount
  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setError(null);
      setPageIndex(0);
      try {
        const dataV1 = await client.listPackagesV1('repo');
        setV1List(dataV1);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load R.E.P.O. packages');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [client]);

  const cosmeticKeywords = ['cosmetic', 'decoration', 'outfit', 'skin', 'character', 'hat', 'accessory'];

  // Filter V1 list for cosmetics locally
  const cosmeticCandidates = useMemo<Candidate[]>(() => {
    if (v1List) {
      const filtered = v1List.filter((pkg) => {
        const fullText = `${pkg.name} ${pkg.full_name} ${pkg.owner}`.toLowerCase();
        return cosmeticKeywords.some((kw) => fullText.includes(kw));
      });
      return dedupeCandidates(filtered.map((p) => ({ namespace: p.owner, name: p.name })));
    }
    return [];
  }, [v1List]);

  // Load details for current page
  useEffect(() => {
    const loadDetails = async () => {
      if (cosmeticCandidates.length === 0) {
        setMods([]);
        return;
      }
      setLoading(true);
      setError(null);
      try {
        const start = pageIndex * ITEMS_PER_PAGE;
        const end = start + ITEMS_PER_PAGE;
        const slice = cosmeticCandidates.slice(start, end);

        const v1Map = new Map<string, PackageListing>();
        for (const item of v1List || []) {
          v1Map.set(`${item.owner}/${item.name}`, item);
        }

        const results: ModSummary[] = [];
        for (const pkg of slice) {
          const key = `${pkg.namespace}/${pkg.name}`;
          const base = v1Map.get(key);
          
          // Get version from V1 package data (always available)
          let version = 'unknown';
          const baseAny = base as any;
          if (baseAny?.versions && Array.isArray(baseAny.versions) && baseAny.versions.length > 0) {
            const firstVersion = baseAny.versions[0];
            version = firstVersion.version_number || 'unknown';
          }
          
          // Try to get downloads from metrics endpoint
          let downloads = 'N/A';
          try {
            const metrics = await client.getPackageMetrics(pkg.namespace, pkg.name);
            downloads = String(metrics.downloads);
          } catch {
            // Fallback: try to get from V1 version data
            if (baseAny?.versions && Array.isArray(baseAny.versions) && baseAny.versions.length > 0) {
              const firstVersion = baseAny.versions[0];
              if (firstVersion.downloads !== undefined) {
                downloads = String(firstVersion.downloads);
              }
            }
          }
          
          results.push({
            namespace: pkg.namespace,
            name: pkg.name,
            fullName: base ? base.full_name ?? key : key,
            author: base ? base.owner : pkg.namespace,
            version,
            downloads,
            description: '',
            url: base ? base.package_url : client.getPackageUrl(pkg.namespace, pkg.name),
          });
        }
        setMods(results);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load mod details');
      } finally {
        setLoading(false);
      }
    };
    loadDetails();
  }, [client, cosmeticCandidates, v1List, pageIndex]);

  // Filter mods by search term
  const filteredMods = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    if (!term) return mods;
    return mods.filter(
      (m) =>
        m.fullName.toLowerCase().includes(term) ||
        m.author.toLowerCase().includes(term) ||
        m.description.toLowerCase().includes(term)
    );
  }, [mods, searchTerm]);

  const hasMore = cosmeticCandidates.length > (pageIndex + 1) * ITEMS_PER_PAGE;
  const totalFound = cosmeticCandidates.length;
  const showing = Math.min((pageIndex + 1) * ITEMS_PER_PAGE, totalFound);

  return (
    <div className="cosmetic-search" style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
      <h2>R.E.P.O. Cosmetic Mods</h2>
      <p style={{ color: '#666' }}>
        {totalFound > 0
          ? `Found ${totalFound} cosmetic mods for R.E.P.O.`
          : 'Searching for R.E.P.O. cosmetic mods...'}
      </p>

      <div style={{ display: 'flex', gap: '10px', alignItems: 'center', margin: '10px 0 20px' }}>
        <input
          type="text"
          placeholder="Filter results by name, author..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          style={{ padding: '8px', width: '360px' }}
        />
        <span style={{ color: '#666' }}>
          {v1List ? `Showing ${showing} of ${totalFound}` : 'Loading packages...'}
        </span>
      </div>

      {error && (
        <div style={{ color: 'red', padding: '10px', background: '#fee', marginBottom: '20px' }}>
          Error: {error}
        </div>
      )}

      {loading && <div style={{ marginBottom: '10px' }}>Loading...</div>}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
        <div>
          <h3>Mods ({filteredMods.length})</h3>
          <div style={{ maxHeight: '600px', overflowY: 'auto', border: '1px solid #ccc', padding: '10px' }}>
            {filteredMods.map((m) => (
              <div
                key={m.fullName}
                onClick={() => setSelected(m)}
                style={{
                  padding: '10px',
                  marginBottom: '6px',
                  background: '#f5f5f5',
                  cursor: 'pointer',
                  borderLeft: selected?.fullName === m.fullName ? '4px solid #007bff' : 'none',
                }}
              >
                <div style={{ fontWeight: 'bold' }}>{m.fullName}</div>
                <div style={{ fontSize: '0.9em', color: '#666' }}>
                  v{m.version} • by {m.author} • {m.downloads} downloads
                </div>
              </div>
            ))}
            {filteredMods.length === 0 && totalFound > 0 && (
              <div style={{ color: '#666' }}>No mods match your filter.</div>
            )}
          </div>
          {hasMore && (
            <button
              onClick={() => setPageIndex((i) => i + 1)}
              style={{ marginTop: '10px', padding: '8px 16px', cursor: 'pointer' }}
              disabled={loading}
            >
              {loading ? 'Loading...' : `Load More (${showing}/${totalFound})`}
            </button>
          )}
        </div>

        <div>
          <h3>Details</h3>
          {selected ? (
            <div style={{ border: '1px solid #ccc', padding: '15px' }}>
              <h4 style={{ margin: 0 }}>{selected.fullName}</h4>
              <p style={{ margin: '8px 0' }}>
                <strong>Author:</strong> {selected.author}
              </p>
              <p style={{ margin: '8px 0' }}>
                <strong>Version:</strong> {selected.version}
              </p>
              <p style={{ margin: '8px 0' }}>
                <strong>Downloads:</strong> {selected.downloads}
              </p>
              <div style={{ marginTop: '12px' }}>
                <a href={selected.url} target="_blank" rel="noopener noreferrer" style={{ marginRight: '10px' }}>
                  View on Thunderstore
                </a>
              </div>
            </div>
          ) : (
            <div style={{ border: '1px solid #ccc', padding: '15px', color: '#666' }}>
              Select a mod to view details
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
