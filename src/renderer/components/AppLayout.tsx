import { ReactNode } from 'react';

interface AppLayoutProps {
  header: ReactNode;
  modList: ReactNode;
  modDetail: ReactNode;
}

export default function AppLayout({ header, modList, modDetail }: AppLayoutProps) {
  return (
    <div className="app-layout">
      {header}
      <div className="app-content">
        <aside className="mod-list-pane">{modList}</aside>
        <main className="mod-detail-pane">{modDetail}</main>
      </div>
    </div>
  );
}
