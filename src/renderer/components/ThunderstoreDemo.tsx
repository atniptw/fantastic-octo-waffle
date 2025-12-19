/**
 * Demo component showing Thunderstore API usage
 */

import { useState } from 'react';
import {
  createThunderstoreClient,
  type PackageIndexEntry,
  type PackageExperimental,
} from '@/lib/thunderstore';

export function ThunderstoreDemo() {
  const [packages, setPackages] = useState<PackageIndexEntry[]>([]);
  const [selectedPackage, setSelectedPackage] = useState<PackageExperimental | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');

  const client = createThunderstoreClient();

  const loadPackageIndex = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await client.getPackageIndex();
      setPackages(data);
      console.log(`Loaded ${data.length} packages from Thunderstore`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load packages');
      console.error('Error loading package index:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadPackageDetails = async (namespace: string, name: string) => {
    setLoading(true);
    setError(null);
    try {
      const data = await client.getPackage(namespace, name);
      setSelectedPackage(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load package details');
      console.error('Error loading package:', err);
    } finally {
      setLoading(false);
    }
  };

  const filteredPackages = packages.filter(
    (pkg) =>
      pkg.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      pkg.namespace.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div
      className="thunderstore-demo"
      style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}
    >
      <h1>Thunderstore API Demo</h1>

      <div style={{ marginBottom: '20px' }}>
        <button onClick={loadPackageIndex} disabled={loading}>
          {loading ? 'Loading...' : 'Load Package Index'}
        </button>
        {packages.length > 0 && (
          <span style={{ marginLeft: '10px' }}>Loaded {packages.length} packages</span>
        )}
      </div>

      {error && (
        <div style={{ color: 'red', marginBottom: '20px', padding: '10px', background: '#fee' }}>
          Error: {error}
        </div>
      )}

      {packages.length > 0 && (
        <div style={{ marginBottom: '20px' }}>
          <input
            type="text"
            placeholder="Search packages..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            style={{ padding: '8px', width: '300px', marginBottom: '10px' }}
          />
          <div>Showing {filteredPackages.length} packages</div>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
        {/* Package List */}
        <div>
          <h2>Packages</h2>
          <div
            style={{
              maxHeight: '600px',
              overflowY: 'auto',
              border: '1px solid #ccc',
              padding: '10px',
            }}
          >
            {filteredPackages.slice(0, 100).map((pkg) => (
              <div
                key={`${pkg.namespace}-${pkg.name}-${pkg.version_number}`}
                style={{
                  padding: '10px',
                  marginBottom: '5px',
                  background: '#f5f5f5',
                  cursor: 'pointer',
                  borderLeft:
                    selectedPackage?.namespace === pkg.namespace &&
                    selectedPackage?.name === pkg.name
                      ? '4px solid #007bff'
                      : 'none',
                }}
                onClick={() => loadPackageDetails(pkg.namespace, pkg.name)}
              >
                <div style={{ fontWeight: 'bold' }}>
                  {pkg.namespace}/{pkg.name}
                </div>
                <div style={{ fontSize: '0.9em', color: '#666' }}>
                  v{pkg.version_number} • {(pkg.file_size / 1024 / 1024).toFixed(2)} MB
                </div>
                {pkg.dependencies && (
                  <div style={{ fontSize: '0.8em', color: '#888' }}>
                    Dependencies: {pkg.dependencies.split(',').length}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Package Details */}
        <div>
          <h2>Package Details</h2>
          {selectedPackage ? (
            <div style={{ border: '1px solid #ccc', padding: '15px' }}>
              <h3>{selectedPackage.full_name}</h3>
              <p>
                <strong>Owner:</strong> {selectedPackage.owner}
              </p>
              <p>
                <strong>Latest Version:</strong> {selectedPackage.latest.version_number}
              </p>
              <p>
                <strong>Description:</strong> {selectedPackage.latest.description}
              </p>
              <p>
                <strong>Total Downloads:</strong> {selectedPackage.total_downloads}
              </p>
              <p>
                <strong>Rating Score:</strong> {selectedPackage.rating_score}
              </p>
              <p>
                <strong>Created:</strong>{' '}
                {new Date(selectedPackage.date_created).toLocaleDateString()}
              </p>
              <p>
                <strong>Updated:</strong>{' '}
                {new Date(selectedPackage.date_updated).toLocaleDateString()}
              </p>
              {selectedPackage.is_deprecated && (
                <div style={{ color: 'red', fontWeight: 'bold' }}>⚠️ DEPRECATED</div>
              )}
              {selectedPackage.is_pinned && (
                <div style={{ color: 'green', fontWeight: 'bold' }}>📌 PINNED</div>
              )}
              <div style={{ marginTop: '15px' }}>
                <a
                  href={client.getPackageUrl(selectedPackage.namespace, selectedPackage.name)}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{ marginRight: '10px' }}
                >
                  View on Thunderstore
                </a>
                <a
                  href={client.getPackageDownloadUrl(
                    selectedPackage.namespace,
                    selectedPackage.name,
                    selectedPackage.latest.version_number
                  )}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  Download
                </a>
              </div>
              <div style={{ marginTop: '15px' }}>
                <h4>Community Listings ({selectedPackage.community_listings.length})</h4>
                {selectedPackage.community_listings.map(
                  (listing: (typeof selectedPackage.community_listings)[0], idx: number) => (
                    <div key={idx} style={{ fontSize: '0.9em', marginBottom: '5px' }}>
                      <strong>{listing.community}</strong> - {listing.review_status}
                      {listing.has_nsfw_content && ' (NSFW)'}
                    </div>
                  )
                )}
              </div>
            </div>
          ) : (
            <div style={{ border: '1px solid #ccc', padding: '15px', color: '#666' }}>
              Select a package to view details
            </div>
          )}
        </div>
      </div>

      {/* Usage Example */}
      <div style={{ marginTop: '40px', padding: '20px', background: '#f9f9f9' }}>
        <h3>Usage Example</h3>
        <pre style={{ background: '#fff', padding: '15px', overflow: 'auto' }}>
          {`import { createThunderstoreClient } from './lib/thunderstore';

const client = createThunderstoreClient();

// Get all packages
const packages = await client.getPackageIndex();

// Get a specific package
const pkg = await client.getPackage('BepInEx', 'BepInExPack');

// Get package metrics
const metrics = await client.getPackageMetrics('BepInEx', 'BepInExPack');

// Build download URL
const url = client.getPackageDownloadUrl(
  'BepInEx',
  'BepInExPack',
  '5.4.21'
);`}
        </pre>
      </div>
    </div>
  );
}
