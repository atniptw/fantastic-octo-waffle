import { describe, it, expect } from 'vitest';
import { formatDownloads, formatRating } from './index';

describe('Utils', () => {
  describe('formatDownloads', () => {
    it('should return number as string for values < 1000', () => {
      expect(formatDownloads(0)).toBe('0');
      expect(formatDownloads(999)).toBe('999');
      expect(formatDownloads(500)).toBe('500');
    });

    it('should format thousands with K suffix', () => {
      expect(formatDownloads(1000)).toBe('1.0K');
      expect(formatDownloads(1500)).toBe('1.5K');
      expect(formatDownloads(999999)).toBe('1000.0K');
    });

    it('should format millions with M suffix', () => {
      expect(formatDownloads(1000000)).toBe('1.0M');
      expect(formatDownloads(1500000)).toBe('1.5M');
      expect(formatDownloads(10000000)).toBe('10.0M');
    });

    it('should handle large numbers', () => {
      expect(formatDownloads(123456789)).toBe('123.5M');
    });
  });

  describe('formatRating', () => {
    it('should return N/A for undefined', () => {
      expect(formatRating(undefined)).toBe('N/A');
    });

    it('should return N/A for null (edge case)', () => {
      // @ts-expect-error Testing null edge case
      expect(formatRating(null)).toBe('N/A');
    });

    it('should convert 0-100 scale to 0-5 stars', () => {
      expect(formatRating(0)).toBe('0.0 ★');
      expect(formatRating(50)).toBe('2.5 ★');
      expect(formatRating(85)).toBe('4.3 ★');
      expect(formatRating(100)).toBe('5.0 ★');
    });

    it('should handle decimal values', () => {
      expect(formatRating(33.3)).toBe('1.7 ★');
      expect(formatRating(66.7)).toBe('3.3 ★');
    });

    it('should handle out-of-range values gracefully', () => {
      expect(formatRating(150)).toBe('7.5 ★');
      expect(formatRating(-10)).toBe('-0.5 ★');
    });
  });
});
