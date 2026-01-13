import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/preact';
import { ErrorToast } from '../components/ErrorToast';

describe('ErrorToast', () => {
  it('should render with role alert for accessibility', () => {
    const onDismiss = vi.fn();
    render(<ErrorToast message="Test error message" onDismiss={onDismiss} />);

    const alert = screen.getByRole('alert');
    expect(alert).toBeDefined();
  });

  it('should display the error message', () => {
    const onDismiss = vi.fn();
    const message = 'Failed to load data';
    render(<ErrorToast message={message} onDismiss={onDismiss} />);

    expect(screen.getByText(message)).toBeDefined();
  });

  it('should call onDismiss when dismiss button is clicked', () => {
    const onDismiss = vi.fn();
    render(<ErrorToast message="Test error" onDismiss={onDismiss} />);

    const dismissButton = screen.getByLabelText('Dismiss error');
    dismissButton.click();

    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('should render error icon', () => {
    const onDismiss = vi.fn();
    const { container } = render(<ErrorToast message="Test error" onDismiss={onDismiss} />);

    const icon = container.querySelector('.error-icon');
    expect(icon).toBeDefined();
    expect(icon?.textContent).toBe('⚠️');
  });

  it('should have dismiss button with × symbol', () => {
    const onDismiss = vi.fn();
    render(<ErrorToast message="Test error" onDismiss={onDismiss} />);

    const dismissButton = screen.getByLabelText('Dismiss error');
    expect(dismissButton.textContent).toBe('×');
  });
});
