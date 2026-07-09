import { describe, it, expect } from 'vitest';
import { computeReviewCoverage } from '../reviewCoverage';
import type { SectionReviewStatusDto } from '@/types/workflows';

function section(overrides: Partial<SectionReviewStatusDto>): SectionReviewStatusDto {
  return { sectionIndex: 0, reviewedAt: null, reviewedBy: null, ...overrides };
}

describe('computeReviewCoverage', () => {
  it('returns zero coverage and no badge signal when no sections have provenance', () => {
    const result = computeReviewCoverage([
      section({ sectionIndex: 0 }),
      section({ sectionIndex: 1 }),
    ]);
    expect(result).toEqual({
      reviewedCount: 0,
      totalCount: 2,
      mostRecentReviewedAt: null,
      mostRecentReviewedBy: null,
      isFullScope: false,
    });
  });

  it('treats an empty translation (no sections) the same as unreviewed', () => {
    expect(computeReviewCoverage([])).toEqual({
      reviewedCount: 0,
      totalCount: 0,
      mostRecentReviewedAt: null,
      mostRecentReviewedBy: null,
      isFullScope: false,
    });
  });

  it('detects full scope when every section shares the same reviewer and timestamp', () => {
    const reviewedAt = '2026-07-09T10:00:00.000Z';
    const result = computeReviewCoverage([
      section({ sectionIndex: 0, reviewedAt, reviewedBy: 'reviewer@example.com' }),
      section({ sectionIndex: 1, reviewedAt, reviewedBy: 'reviewer@example.com' }),
    ]);
    expect(result.isFullScope).toBe(true);
    expect(result.reviewedCount).toBe(2);
    expect(result.totalCount).toBe(2);
    expect(result.mostRecentReviewedAt).toBe(reviewedAt);
    expect(result.mostRecentReviewedBy).toBe('reviewer@example.com');
  });

  it('treats partial coverage as not full scope, even with a single shared reviewer', () => {
    const reviewedAt = '2026-07-09T10:00:00.000Z';
    const result = computeReviewCoverage([
      section({ sectionIndex: 0, reviewedAt, reviewedBy: 'reviewer@example.com' }),
      section({ sectionIndex: 1 }),
      section({ sectionIndex: 2 }),
    ]);
    expect(result.isFullScope).toBe(false);
    expect(result.reviewedCount).toBe(1);
    expect(result.totalCount).toBe(3);
  });

  it('treats multi-round coverage (differing timestamps) as not full scope', () => {
    const result = computeReviewCoverage([
      section({ sectionIndex: 0, reviewedAt: '2026-07-06T10:00:00.000Z', reviewedBy: 'first@example.com' }),
      section({ sectionIndex: 1, reviewedAt: '2026-07-09T10:00:00.000Z', reviewedBy: 'second@example.com' }),
    ]);
    expect(result.isFullScope).toBe(false);
    expect(result.reviewedCount).toBe(2);
    expect(result.totalCount).toBe(2);
    expect(result.mostRecentReviewedAt).toBe('2026-07-09T10:00:00.000Z');
    expect(result.mostRecentReviewedBy).toBe('second@example.com');
  });

  it('picks the most recent ReviewedAt/By across mixed reviewed and unreviewed sections', () => {
    const result = computeReviewCoverage([
      section({ sectionIndex: 0, reviewedAt: '2026-07-06T10:00:00.000Z', reviewedBy: 'first@example.com' }),
      section({ sectionIndex: 1 }),
      section({ sectionIndex: 2, reviewedAt: '2026-07-09T10:00:00.000Z', reviewedBy: 'second@example.com' }),
      section({ sectionIndex: 3 }),
    ]);
    expect(result.reviewedCount).toBe(2);
    expect(result.totalCount).toBe(4);
    expect(result.mostRecentReviewedAt).toBe('2026-07-09T10:00:00.000Z');
    expect(result.mostRecentReviewedBy).toBe('second@example.com');
    expect(result.isFullScope).toBe(false);
  });
});
