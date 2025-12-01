import ImportButton from './components/ImportButton';

function App() {
  return (
    <div className="app">
      <header className="app-header">
        <h1>R.E.P.O. Cosmetic Catalog</h1>
        <p>Browse and search cosmetic mods for R.E.P.O.</p>
      </header>
      <main className="app-main">
        <section className="import-section">
          <ImportButton />
        </section>
        <section className="catalog-section">
          <p className="placeholder-text">
            Import mod ZIP files to populate the catalog.
          </p>
        </section>
      </main>
    </div>
  );
}

export default App;
