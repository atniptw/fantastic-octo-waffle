import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import ActivityLog from '../ActivityLog';
import { ImportLogEntry } from '../../types/electron';

describe('ActivityLog', () => {
  it('should not render when there are no logs and not importing', () => {
    const { container } = render(<ActivityLog logs={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('should render when importing even with no logs', () => {
    render(<ActivityLog logs={[]} isImporting={true} />);
    expect(screen.getByText('Activity Log')).toBeInTheDocument();
    expect(screen.getByText('Processing...')).toBeInTheDocument();
  });

  it('should render success log entries', () => {
    const logs: ImportLogEntry[] = [
      {
        timestamp: '2024-01-01T12:00:00.000Z',
        filename: 'test-mod.zip',
        status: 'success',
        message: 'Imported successfully',
      },
    ];

    render(<ActivityLog logs={logs} />);
    
    expect(screen.getByText('test-mod.zip')).toBeInTheDocument();
    expect(screen.getByText('Imported successfully')).toBeInTheDocument();
    expect(screen.getByText('✅')).toBeInTheDocument();
  });

  it('should render warning log entries', () => {
    const logs: ImportLogEntry[] = [
      {
        timestamp: '2024-01-01T12:00:00.000Z',
        filename: 'warning-mod.zip',
        status: 'warning',
        message: 'Mod already imported',
      },
    ];

    render(<ActivityLog logs={logs} />);
    
    expect(screen.getByText('warning-mod.zip')).toBeInTheDocument();
    expect(screen.getByText('Mod already imported')).toBeInTheDocument();
    expect(screen.getByText('⚠️')).toBeInTheDocument();
  });

  it('should render error log entries', () => {
    const logs: ImportLogEntry[] = [
      {
        timestamp: '2024-01-01T12:00:00.000Z',
        filename: 'error-mod.zip',
        status: 'error',
        message: 'Failed to process',
      },
    ];

    render(<ActivityLog logs={logs} />);
    
    expect(screen.getByText('error-mod.zip')).toBeInTheDocument();
    expect(screen.getByText('Failed to process')).toBeInTheDocument();
    expect(screen.getByText('❌')).toBeInTheDocument();
  });

  it('should render multiple log entries', () => {
    const logs: ImportLogEntry[] = [
      {
        timestamp: '2024-01-01T12:00:00.000Z',
        filename: 'mod1.zip',
        status: 'success',
        message: 'Success 1',
      },
      {
        timestamp: '2024-01-01T12:01:00.000Z',
        filename: 'mod2.zip',
        status: 'error',
        message: 'Error 2',
      },
    ];

    render(<ActivityLog logs={logs} />);
    
    expect(screen.getByText('mod1.zip')).toBeInTheDocument();
    expect(screen.getByText('mod2.zip')).toBeInTheDocument();
    expect(screen.getByText('Success 1')).toBeInTheDocument();
    expect(screen.getByText('Error 2')).toBeInTheDocument();
  });

  it('should show starting message when importing with no logs', () => {
    render(<ActivityLog logs={[]} isImporting={true} />);
    
    expect(screen.getByText('Starting import...')).toBeInTheDocument();
    expect(screen.getByText('⏳')).toBeInTheDocument();
  });

  it('should have correct CSS classes for different statuses', () => {
    const logs: ImportLogEntry[] = [
      { timestamp: '2024-01-01T12:00:00.000Z', filename: 'success.zip', status: 'success', message: 'Success' },
      { timestamp: '2024-01-01T12:00:00.000Z', filename: 'warning.zip', status: 'warning', message: 'Warning' },
      { timestamp: '2024-01-01T12:00:00.000Z', filename: 'error.zip', status: 'error', message: 'Error' },
      { timestamp: '2024-01-01T12:00:00.000Z', filename: 'info.zip', status: 'info', message: 'Info' },
    ];

    const { container } = render(<ActivityLog logs={logs} />);
    
    expect(container.querySelector('.log-success')).toBeInTheDocument();
    expect(container.querySelector('.log-warning')).toBeInTheDocument();
    expect(container.querySelector('.log-error')).toBeInTheDocument();
    expect(container.querySelector('.log-info')).toBeInTheDocument();
  });
});
