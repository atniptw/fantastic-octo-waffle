import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import ImportButton from '../ImportButton';

describe('ImportButton', () => {
  it('should render the import button', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button', { name: /import mod zip/i });
    expect(button).toBeInTheDocument();
  });

  it('should display the folder icon in idle state', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button.textContent).toContain('📁');
    expect(button.textContent).toContain('Import Mod ZIP(s)');
  });

  it('should have idle status class by default', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button).toHaveClass('import-button--idle');
  });

  it('should have the correct CSS class', () => {
    render(<ImportButton />);
    
    const button = screen.getByRole('button');
    expect(button).toHaveClass('import-button');
  });

  it('should be disabled when disabled prop is true', () => {
    render(<ImportButton disabled={true} />);
    
    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('should have a hidden file input', () => {
    render(<ImportButton />);
    
    const fileInput = screen.getByTestId('import-input');
    expect(fileInput).toBeInTheDocument();
    expect(fileInput).toHaveAttribute('type', 'file');
    expect(fileInput).toHaveAttribute('accept', '.zip');
    expect(fileInput).toHaveAttribute('multiple');
  });
});
