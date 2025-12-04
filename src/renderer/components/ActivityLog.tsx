import { ImportLogEntry } from '../types/electron';

interface ActivityLogProps {
  logs: ImportLogEntry[];
  isImporting?: boolean;
}

function ActivityLog({ logs, isImporting = false }: ActivityLogProps) {
  const getStatusIcon = (status: ImportLogEntry['status']): string => {
    switch (status) {
      case 'success':
        return '✅';
      case 'warning':
        return '⚠️';
      case 'error':
        return '❌';
      case 'info':
        return 'ℹ️';
      default:
        return '📋';
    }
  };

  const getStatusClass = (status: ImportLogEntry['status']): string => {
    return `log-entry log-${status}`;
  };

  const formatTimestamp = (timestamp: string): string => {
    return new Date(timestamp).toLocaleTimeString();
  };

  if (logs.length === 0 && !isImporting) {
    return null;
  }

  return (
    <div className="activity-log">
      <h3 className="activity-log-title">
        Activity Log
        {isImporting && <span className="importing-indicator">Processing...</span>}
      </h3>
      <div className="log-container">
        {logs.length === 0 && isImporting ? (
          <div className="log-entry log-info">
            <span className="log-icon">⏳</span>
            <span className="log-message">Starting import...</span>
          </div>
        ) : (
          logs.map((log, index) => (
            <div key={index} className={getStatusClass(log.status)}>
              <span className="log-icon">{getStatusIcon(log.status)}</span>
              <span className="log-time">{formatTimestamp(log.timestamp)}</span>
              <span className="log-filename">{log.filename}</span>
              <span className="log-message">{log.message}</span>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export default ActivityLog;
