import { useState } from 'react';
import ImportButton from '@/renderer/components/ImportButton';
import ActivityLog from '@/renderer/components/ActivityLog';
import CatalogView from '@/renderer/components/CatalogView';
import FileUploadDemo from '@/renderer/components/FileUploadDemo';
import { ImportLogEntry, ImportFilesResult, Mod, Cosmetic } from '@/shared/types';

type ViewMode = 'import' | 'catalog' | 'demo';

function App() {
  const [currentView, setCurrentView] = useState<ViewMode>('import');
  const [logs, setLogs] = useState<ImportLogEntry[]>([]);
  const [isImporting, setIsImporting] = useState(false);
  const [mods, setMods] = useState<Mod[]>([]);
  const [cosmetics, setCosmetics] = useState<Cosmetic[]>([]);
  const [lastImportSummary, setLastImportSummary] = useState<{
    total: number;
    success: number;
    errors: number;
    warnings: number;
  } | null>(null);

  const handleImportStart = () => {
    setIsImporting(true);
    setLogs([]);
    setLastImportSummary(null);
  };

  const handleImportComplete = (result: ImportFilesResult) => {
    setIsImporting(false);
    setLogs(result.logs);
    setLastImportSummary({
      total: result.totalFiles,
      success: result.successCount,
      errors: result.errorCount,
      warnings: result.warningCount,
    });
    // Update catalog with new mods and cosmetics
    setMods(prev => [...prev, ...result.mods]);
    setCosmetics(prev => [...prev, ...result.cosmetics]);
  };

  const handleImportError = (error: string) => {
    setIsImporting(false);
    const errorLog: ImportLogEntry = {
      timestamp: new Date().toISOString(),
      filename: 'System',
      status: 'error',
      message: error,
    };
    setLogs(prev => [...prev, errorLog]);
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>R.E.P.O. Cosmetic Catalog</h1>
        <p>Browse and search cosmetic mods for R.E.P.O.</p>
        <nav className="app-nav">
          <button
            className={`nav-button ${currentView === 'import' ? 'active' : ''}`}
            onClick={() => setCurrentView('import')}
            aria-current={currentView === 'import' ? 'page' : undefined}
          >
            📥 Import
          </button>
          <button
            className={`nav-button ${currentView === 'catalog' ? 'active' : ''}`}
            onClick={() => setCurrentView('catalog')}
            aria-current={currentView === 'catalog' ? 'page' : undefined}
          >
            📚 Catalog
          </button>
          <button
            className={`nav-button ${currentView === 'demo' ? 'active' : ''}`}
            onClick={() => setCurrentView('demo')}
            aria-current={currentView === 'demo' ? 'page' : undefined}
          >
            🎨 Upload Demo
          </button>
        </nav>
      </header>

      <main className="app-main">
        {currentView === 'import' && (
          <>
            <section className="import-section">
              <ImportButton
                onImportStart={handleImportStart}
                onImportComplete={handleImportComplete}
                onImportError={handleImportError}
                disabled={isImporting}
              />
            </section>

            {lastImportSummary && (
              <section className="import-summary">
                <div className="summary-title">Import Summary</div>
                <div className="summary-stats">
                  <span className="stat-item stat-total">
                    Total: {lastImportSummary.total}
                  </span>
                  <span className="stat-item stat-success">
                    ✅ Success: {lastImportSummary.success}
                  </span>
                  {lastImportSummary.warnings > 0 && (
                    <span className="stat-item stat-warning">
                      ⚠️ Warnings: {lastImportSummary.warnings}
                    </span>
                  )}
                  {lastImportSummary.errors > 0 && (
                    <span className="stat-item stat-error">
                      ❌ Errors: {lastImportSummary.errors}
                    </span>
                  )}
                </div>
              </section>
            )}

            <ActivityLog logs={logs} isImporting={isImporting} />

            {logs.length === 0 && !isImporting && (
              <section className="catalog-section">
                <p className="placeholder-text">
                  Import mod ZIP files to populate the catalog.
                </p>
              </section>
            )}
          </>
        )}

        {currentView === 'catalog' && (
          <CatalogView mods={mods} cosmetics={cosmetics} />
        )}

        {currentView === 'demo' && (
          <FileUploadDemo />
        )}
      </main>
    </div>
  );
}

export default App;
