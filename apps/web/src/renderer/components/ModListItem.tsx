import { ModPackage } from '@/lib/thunderstore/normalize';

interface ModListItemProps {
  mod: ModPackage;
  isSelected: boolean;
  onClick: () => void;
}

export default function ModListItem({ mod, isSelected, onClick }: ModListItemProps) {
  const downloadCount = mod.downloadCount || 0;
  const formattedDownloads =
    downloadCount >= 1000 ? `${(downloadCount / 1000).toFixed(1)}k` : downloadCount.toString();

  return (
    <div
      className={`mod-list-item ${isSelected ? 'selected' : ''}`}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick();
        }
      }}
    >
      <div className="mod-item-content">
        <h3 className="mod-item-name">{mod.name}</h3>
        <p className="mod-item-author">by {mod.owner}</p>
        <p className="mod-item-description">
          {mod.description || 'No description'}
        </p>
        <div className="mod-item-meta">
          {downloadCount > 0 && <span className="mod-meta-badge">↓ {formattedDownloads}</span>}
        </div>
      </div>
    </div>
  );
}
