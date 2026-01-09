import { useState } from 'react';

interface HeaderProps {
  onSearch?: (query: string) => void;
}

export default function Header({ onSearch }: HeaderProps) {
  const [searchQuery, setSearchQuery] = useState('');

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setSearchQuery(value);
    onSearch?.(value);
  };

  return (
    <header className="header">
      <div className="header-content">
        <h1 className="header-title">R.E.P.O. Cosmetic Inspector</h1>
        <div className="header-search">
          <input
            type="search"
            className="search-input"
            placeholder="Search mods..."
            value={searchQuery}
            onChange={handleSearchChange}
            aria-label="Search mods"
          />
        </div>
      </div>
    </header>
  );
}
