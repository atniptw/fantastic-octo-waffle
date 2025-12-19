import { useState } from 'react';
import CatalogView from '@/renderer/components/CatalogView';
import FileUploadDemo from '@/renderer/components/FileUploadDemo';
import { Mod, Cosmetic } from '@/shared/types';
import CosmeticSearchView from '@/renderer/components/CosmeticSearchView';

type ViewMode = 'search' | 'catalog' | 'demo';

function App() {
  const [currentView, setCurrentView] = useState<ViewMode>('search');
  const [mods] = useState<Mod[]>([]);
  const [cosmetics] = useState<Cosmetic[]>([]);

  return (
    <div className="app">
      <header className="app-header">
        <h1>R.E.P.O. Cosmetic Catalog</h1>
        <p>Browse and search cosmetic mods for R.E.P.O.</p>
        <nav className="app-nav">
          <button
            className={`nav-button ${currentView === 'search' ? 'active' : ''}`}
            onClick={() => setCurrentView('search')}
            aria-current={currentView === 'search' ? 'page' : undefined}
          >
            🔎 Search
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
        {currentView === 'search' && <CosmeticSearchView />}

        {currentView === 'catalog' && <CatalogView mods={mods} cosmetics={cosmetics} />}

        {currentView === 'demo' && <FileUploadDemo />}
      </main>
    </div>
  );
}

export default App;
