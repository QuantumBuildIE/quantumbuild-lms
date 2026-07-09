import type { SectionReviewStatusDto } from '@/types/workflows';

export interface ReviewCoverage {
  reviewedCount: number;
  totalCount: number;
  mostRecentReviewedAt: string | null;
  mostRecentReviewedBy: string | null;
  /** All sections reviewed, by the same reviewer, in the same submission (same ReviewedAt). */
  isFullScope: boolean;
}

const EMPTY_COVERAGE: ReviewCoverage = {
  reviewedCount: 0,
  totalCount: 0,
  mostRecentReviewedAt: null,
  mostRecentReviewedBy: null,
  isFullScope: false,
};

/**
 * Derives external-review coverage (N of M sections, most recent reviewer/date, and whether
 * the review was full-scope-single-round) from per-section provenance. "Full scope" requires
 * every section to be reviewed by the same reviewer at the exact same ReviewedAt — string
 * equality on the raw ISO value, since sections stamped in the same submission share the same
 * serialised DateTime; a genuinely later round produces a different value.
 */
export function computeReviewCoverage(sections: SectionReviewStatusDto[]): ReviewCoverage {
  const totalCount = sections.length;
  const reviewed = sections.filter((s) => s.reviewedAt != null);
  const reviewedCount = reviewed.length;

  if (reviewedCount === 0) {
    return { ...EMPTY_COVERAGE, totalCount };
  }

  const mostRecent = reviewed.reduce((latest, s) =>
    new Date(s.reviewedAt as string).getTime() > new Date(latest.reviewedAt as string).getTime()
      ? s
      : latest
  );

  const isFullScope =
    reviewedCount === totalCount &&
    reviewed.every((s) => s.reviewedBy === reviewed[0].reviewedBy) &&
    reviewed.every((s) => s.reviewedAt === reviewed[0].reviewedAt);

  return {
    reviewedCount,
    totalCount,
    mostRecentReviewedAt: mostRecent.reviewedAt,
    mostRecentReviewedBy: mostRecent.reviewedBy,
    isFullScope,
  };
}
