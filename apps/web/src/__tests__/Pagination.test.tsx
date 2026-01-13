import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/preact';
import { Pagination } from '../components/Pagination';

describe('Pagination', () => {
  it('should render page information', () => {
    const onPrevious = vi.fn();
    const onNext = vi.fn();
    
    render(
      <Pagination
        currentPage={2}
        totalPages={5}
        hasPrevious={true}
        hasNext={true}
        onPrevious={onPrevious}
        onNext={onNext}
      />
    );

    expect(screen.getByText('Page 2 of 5')).toBeDefined();
  });

  it('should disable previous button on first page', () => {
    const onPrevious = vi.fn();
    const onNext = vi.fn();
    
    render(
      <Pagination
        currentPage={1}
        totalPages={5}
        hasPrevious={false}
        hasNext={true}
        onPrevious={onPrevious}
        onNext={onNext}
      />
    );

    const prevButton = screen.getByLabelText('Previous page');
    expect(prevButton.hasAttribute('disabled')).toBe(true);
  });

  it('should disable next button on last page', () => {
    const onPrevious = vi.fn();
    const onNext = vi.fn();
    
    render(
      <Pagination
        currentPage={5}
        totalPages={5}
        hasPrevious={true}
        hasNext={false}
        onPrevious={onPrevious}
        onNext={onNext}
      />
    );

    const nextButton = screen.getByLabelText('Next page');
    expect(nextButton.hasAttribute('disabled')).toBe(true);
  });

  it('should call onPrevious when previous button clicked', () => {
    const onPrevious = vi.fn();
    const onNext = vi.fn();
    
    render(
      <Pagination
        currentPage={2}
        totalPages={5}
        hasPrevious={true}
        hasNext={true}
        onPrevious={onPrevious}
        onNext={onNext}
      />
    );

    const prevButton = screen.getByLabelText('Previous page');
    prevButton.click();
    
    expect(onPrevious).toHaveBeenCalledTimes(1);
  });

  it('should call onNext when next button clicked', () => {
    const onPrevious = vi.fn();
    const onNext = vi.fn();
    
    render(
      <Pagination
        currentPage={2}
        totalPages={5}
        hasPrevious={true}
        hasNext={true}
        onPrevious={onPrevious}
        onNext={onNext}
      />
    );

    const nextButton = screen.getByLabelText('Next page');
    nextButton.click();
    
    expect(onNext).toHaveBeenCalledTimes(1);
  });

  it('should show page 1 of 1 when totalPages is 0', () => {
    const onPrevious = vi.fn();
    const onNext = vi.fn();
    
    render(
      <Pagination
        currentPage={1}
        totalPages={0}
        hasPrevious={false}
        hasNext={false}
        onPrevious={onPrevious}
        onNext={onNext}
      />
    );

    expect(screen.getByText('Page 1 of 1')).toBeDefined();
  });
});
