# ZIP Download and Caching Feature Guide

## User Experience Flow

### Step 1: Browse Mods
- User loads the application and sees a list of cosmetic mods from Thunderstore
- Mods are fetched via the Cloudflare Worker CORS proxy

### Step 2: Select a Mod
- User clicks on a mod in the list to view its details
- Details include name, author, version, description, and download count

### Step 3: Click "Analyze Mod"
```
┌─────────────────────────────┐
│     MOD DETAIL PANEL        │
├─────────────────────────────┤
│ Awesome Cosmetic Pack       │
│ by CoolModder               │
│ v1.2.3  ↓ 1.2k              │
│                             │
│ Great cosmetics for R.E.P.O │
│                             │
│ [Analyze Mod]  [Open...]    │ ← Click here
└─────────────────────────────┘
```

### Step 4: Download with Progress
```
┌─────────────────────────────┐
│   DOWNLOAD PROGRESS         │
├─────────────────────────────┤
│ Downloading AwesomePack...  │
│                             │
│ ████████░░░░░░░░░░░░░░ 45% │
│                             │
│ 23.5 MB / 52.0 MB           │
└─────────────────────────────┘
```

**Features:**
- Real-time progress bar
- Percentage complete
- Downloaded / Total size
- Non-blocking UI (continues to be responsive)

### Step 5: Automatic Caching
After download completes:
- ZIP is automatically saved to IndexedDB
- Metadata (size, date) is stored
- No re-download needed on next visit

### Step 6: View Extraction Results
```
┌─────────────────────────────┐
│   EXTRACTION COMPLETE       │
├─────────────────────────────┤
│ Successfully extracted 12   │
│ cosmetic files              │
│                             │
│ No cosmetic assets found in │
│ this mod.                   │
│                             │
│ Cosmetic assets are         │
│ typically located in        │
│ plugins/*/Decorations/*.hhh │
└─────────────────────────────┘
```

### Step 7: View Cached Downloads
```
┌─────────────────────────────┐
│  CACHED DOWNLOADS (2)       │
├─────────────────────────────┤
│ AwesomePack                 │
│ 52.0 MB  12/19/2025     ✕   │
│                             │
│ CoolCosmetics               │
│ 38.5 MB  12/19/2025     ✕   │
└─────────────────────────────┘
```

**Features:**
- List all cached downloads
- Show file size
- Show download date
- Delete button for each item

## Technical Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    React Component                       │
│                    (ModDetail.tsx)                       │
├─────────────────────────────────────────────────────────┤
│ - Display mod details                                   │
│ - Show download progress                                │
│ - Show cached downloads list                            │
│ - Handle delete requests                                │
└──────────────────┬──────────────────────────────────────┘
                   │
    ┌──────────────┴──────────────┐
    │                             │
    ▼                             ▼
┌─────────────────┐      ┌──────────────────┐
│ useZipDownloader│      │ useZipScanner    │
├─────────────────┤      ├──────────────────┤
│ - Download      │      │ - Extract ZIP    │
│ - Progress      │      │ - Parse manifest │
│ - Caching       │      │ - Find cosmetics │
│ - Error handle  │      │ - Web Worker     │
└────────┬────────┘      └──────────────────┘
         │
         ▼
    ┌─────────────────────────────┐
    │   zipCache (Singleton)      │
    ├─────────────────────────────┤
    │ - Save ZIP files            │
    │ - Retrieve cached data      │
    │ - Manage metadata           │
    │ - Delete entries            │
    └─────────────┬───────────────┘
                  │
                  ▼
         ┌────────────────┐
         │   IndexedDB    │
         ├────────────────┤
         │ ┌────────────┐ │
         │ │zips store  │ │  metadata
         │ ├────────────┤ │
         │ │zipData     │ │  binary
         │ │store       │ │
         │ └────────────┘ │
         └────────────────┘
```

## Code Example: Using the Download Hook

```typescript
// In a React component
const { download, listCached, deleteCached, progress } = useZipDownloader();

// Download a mod
const handleDownload = async () => {
  try {
    const arrayBuffer = await download(url, 'namespace', 'modName');
    // Use the arrayBuffer for ZIP processing
  } catch (error) {
    console.error('Download failed:', error);
  }
};

// Show progress
{progress && (
  <div>{Math.round(progress.percentage)}%</div>
)}

// List cached downloads
const cached = await listCached();
cached.forEach(zip => {
  console.log(`${zip.name}: ${zip.size} bytes`);
});

// Delete a cached ZIP
await deleteCached('namespace', 'modName');
```

## Storage Details

### IndexedDB Structure

```
Database: repo-cosmetic-catalog
├── Object Store: zips
│   ├── Index: namespace
│   ├── Index: downloadedAt
│   └── Keys:
│       ├── "namespace/modname" → {
│       │   id: "namespace/modname",
│       │   namespace: "namespace",
│       │   name: "modname",
│       │   fileName: "modname.zip",
│       │   size: 52000000,
│       │   mimeType: "application/zip",
│       │   downloadedAt: 1702968000000,
│       │   expiresAt: undefined
│       │ }
│       └── ... more entries
│
└── Object Store: zipData
    └── Keys:
        ├── "namespace/modname" → Uint8Array (actual ZIP bytes)
        └── ... more entries
```

### Storage Quota

**Browser Defaults:**
- Chrome/Edge: 50-60% of available disk space (typically 50MB+)
- Firefox: 50MB per origin
- Safari: 50MB per origin
- Opera: Similar to Chrome

**Check Usage:**
```typescript
const { used, limit } = await zipCache.getStorageUsage();
console.log(`Using ${used} of ${limit} bytes`);
```

## Performance Metrics

### First Download (New Mod)
```
Fetch from network:  500-2000ms (network dependent)
IndexedDB write:     50-200ms
Total:              550-2200ms
User sees: Progress bar with real-time updates
```

### Cached Download (Previously Downloaded)
```
IndexedDB read:      10-50ms
Return immediately: No network request
User sees: Instant access to cached ZIP
```

### Extraction Time
```
Extract manifest:    5-20ms
Find cosmetics:      10-50ms
Hash calculation:    100-500ms (depends on file count)
Total:              115-570ms
```

## Error Handling

### Download Failures
- Network errors are caught and reported
- User can retry the download
- Failed downloads are not cached

### Storage Quota Exceeded
```typescript
// Future enhancement: Handle quota exceeded
try {
  await zipCache.saveZip(...);
} catch (e) {
  if (e.name === 'QuotaExceededError') {
    // Offer to clear old cached items
  }
}
```

### Corruption Detection
```typescript
// Validate ZIP after retrieval
const data = await zipCache.getZipData(...);
if (data.byteLength === 0) {
  // Invalid ZIP, clear cache
  await zipCache.deleteZip(...);
}
```

## Browser DevTools Tips

### Inspect IndexedDB
1. Open DevTools → Application → IndexedDB
2. Expand "repo-cosmetic-catalog"
3. View both "zips" (metadata) and "zipData" (binary) stores

### Monitor Storage
1. Application → Storage → Estimated usage
2. See total bytes used by IndexedDB

### Clear Cache
1. Right-click database → Delete database
   - OR -
2. Call `zipCache.clearAll()` from console

## Known Limitations

1. **No Auto-Update:** Cached mods don't check for updates
   - *Workaround:* User can manually delete old cache

2. **Storage Quota:** Limited by browser quota
   - *Solution:* Implement LRU eviction (planned)

3. **No Encryption:** Cached data is not encrypted
   - *Note:* IndexedDB is origin-scoped, cross-site access blocked

4. **Single Tab Sync:** No cross-tab cache synchronization
   - *Planned:* Use storage events for sync

## Future Enhancements

### Phase 2: Smart Cache Management
- [ ] Display storage usage percentage
- [ ] Implement LRU eviction
- [ ] Auto-cleanup old versions
- [ ] User cache size preference

### Phase 3: Advanced Features
- [ ] Resume interrupted downloads
- [ ] Batch download queue
- [ ] Cross-tab cache sync
- [ ] Import/export cache

### Phase 4: Cloud Integration
- [ ] Optional cloud sync
- [ ] Share cache across devices
- [ ] Cache versioning
- [ ] Bandwidth optimization
