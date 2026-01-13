import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/preact';
import { LoadingSpinner } from '../components/LoadingSpinner';

describe('LoadingSpinner', () => {
  it('should render with correct accessibility attributes', () => {
    render(<LoadingSpinner />);

    const spinner = screen.getByRole('status');
    expect(spinner).toBeDefined();
    expect(spinner.getAttribute('aria-label')).toBe('Loading');
  });

  it('should render loading text', () => {
    render(<LoadingSpinner />);

    expect(screen.getByText('Loading mods...')).toBeDefined();
  });

  it('should have spinner animation element', () => {
    const { container } = render(<LoadingSpinner />);

    const spinnerElement = container.querySelector('.spinner');
    expect(spinnerElement).toBeDefined();
  });
});
