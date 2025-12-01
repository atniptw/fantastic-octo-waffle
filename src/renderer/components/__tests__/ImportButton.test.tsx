import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import ImportButton from '../ImportButton';

describe('ImportButton', () => {
  it('should render the import button', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button', { name: /import mod zip/i });
    expect(button).toBeInTheDocument();
  });

  it('should display the folder icon', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button.textContent).toContain('📁');
  });

  it('should log message when clicked', () => {
    const consoleSpy = vi.spyOn(console, 'log');
    
    render(<ImportButton />);
    const button = screen.getByRole('button');
    
    fireEvent.click(button);
    
    expect(consoleSpy).toHaveBeenCalledWith(
      'Import button clicked - functionality to be implemented'
    );
    
    consoleSpy.mockRestore();
  });

  it('should have the correct CSS class', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button).toHaveClass('import-button');
  });
});
