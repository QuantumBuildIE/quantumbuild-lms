import { describe, it, expect } from 'vitest';
import { isStepReachable } from '../stepOrder';
import type { ToolboxTalk } from '@/types/toolbox-talks';
import type { ValidationRunSummary } from '@/types/content-creation';

// Minimal talk factory — only the fields case 7 reads
function makeTalk(overrides: Partial<ToolboxTalk> = {}): ToolboxTalk {
  return {
    id: 'talk-1',
    code: 'T001',
    title: 'Test Talk',
    description: null,
    category: null,
    frequency: 'Once',
    frequencyDisplay: 'Once',
    videoUrl: null,
    videoSource: 'None',
    videoSourceDisplay: 'None',
    attachmentUrl: null,
    minimumVideoWatchPercent: 90,
    requiresQuiz: true,
    passingScore: 80,
    isActive: true,
    status: 'Draft',
    statusDisplay: 'Draft',
    pdfUrl: null,
    pdfFileName: null,
    generatedFromVideo: false,
    generatedFromPdf: false,
    generateSlidesFromPdf: false,
    slidesGenerated: false,
    slideCount: 0,
    slideshowHtml: null,
    slideshowGeneratedAt: null,
    hasSlideshow: false,
    quizQuestionCount: null,
    shuffleQuestions: false,
    shuffleOptions: false,
    useQuestionPool: false,
    allowRetry: true,
    isPartOfCourse: false,
    sourceLanguageCode: 'en',
    autoAssignToNewEmployees: false,
    autoAssignDueDays: 7,
    generateCertificate: true,
    requiresRefresher: false,
    refresherIntervalMonths: 12,
    sections: [{ id: 's1', toolboxTalkId: 'talk-1', sectionNumber: 1, title: 'Sec 1', content: 'Content', requiresAcknowledgment: true }],
    questions: [],
    translations: [],
    completionStats: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    publishedAt: null,
    lastEditedStep: null,
    inputMode: 'Text',
    sourceFileUrl: null,
    sourceFileName: null,
    sourceFileType: null,
    sourceText: null,
    targetLanguageCodes: null,
    reviewerName: null,
    reviewerOrg: null,
    reviewerRole: null,
    documentRef: null,
    clientName: null,
    auditPurpose: null,
    audienceRole: null,
    preserveSourceWording: false,
    coverImageUrl: null,
    ...overrides,
  };
}

function makeCompletedRun(hasPendingDecisions: boolean): ValidationRunSummary {
  return {
    id: 'run-1',
    languageCode: 'fr',
    sectorKey: null,
    overallScore: 85,
    overallOutcome: 'Pass',
    safetyVerdict: null,
    status: 'Completed',
    totalSections: 1,
    passedSections: 1,
    reviewSections: 0,
    failedSections: 0,
    passThreshold: 75,
    auditReportUrl: null,
    startedAt: null,
    completedAt: '2026-01-02T00:00:00Z',
    createdAt: '2026-01-01T00:00:00Z',
    hasPendingDecisions,
  };
}

describe('isStepReachable - case 7 (Publish step)', () => {
  it('returns false when talk has zero sections', () => {
    const talk = makeTalk({ sections: [] });
    expect(isStepReachable(7, talk)).toBe(false);
  });

  it('returns true when no target languages are declared (English-only path)', () => {
    const talk = makeTalk({ targetLanguageCodes: null });
    expect(isStepReachable(7, talk)).toBe(true);
  });

  it('returns false when target languages are declared but no validation runs exist', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr"]' });
    expect(isStepReachable(7, talk, [])).toBe(false);
  });

  it('returns true when target languages declared and a completed run has no pending decisions', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr"]' });
    const runs: ValidationRunSummary[] = [makeCompletedRun(false)];
    expect(isStepReachable(7, talk, runs)).toBe(true);
  });

  it('returns false when talk is already Published', () => {
    const talk = makeTalk({ status: 'Published', targetLanguageCodes: '["fr"]' });
    const runs: ValidationRunSummary[] = [makeCompletedRun(false)];
    expect(isStepReachable(7, talk, runs)).toBe(false);
  });

  it('returns false when a completed run has pending decisions (strict-review gate)', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr"]' });
    const runs: ValidationRunSummary[] = [makeCompletedRun(true)];
    expect(isStepReachable(7, talk, runs)).toBe(false);
  });

  it('treats undefined validationRuns the same as an empty array', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr"]' });
    expect(isStepReachable(7, talk, undefined)).toBe(false);
  });

  it('returns false when one of several target languages was never started (no run at all)', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr","af"]' });
    // Only 'fr' has a run; 'af' has none — this is the exact bug scenario: reaching
    // Publish with an untranslated language present would guarantee a 409.
    const runs: ValidationRunSummary[] = [makeCompletedRun(false)];
    expect(isStepReachable(7, talk, runs)).toBe(false);
  });

  it('returns true when every target language has a completed run with no pending decisions', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr","af"]' });
    const runs: ValidationRunSummary[] = [
      makeCompletedRun(false),
      { ...makeCompletedRun(false), id: 'run-2', languageCode: 'af' },
    ];
    expect(isStepReachable(7, talk, runs)).toBe(true);
  });

  it('returns false when one target language is still Running (not yet resolved)', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr","af"]' });
    const runs: ValidationRunSummary[] = [
      makeCompletedRun(false),
      { ...makeCompletedRun(false), id: 'run-2', languageCode: 'af', status: 'Running' },
    ];
    expect(isStepReachable(7, talk, runs)).toBe(false);
  });

  it('treats a Failed run as resolved (not a permanent dead-end)', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr","af"]' });
    const runs: ValidationRunSummary[] = [
      makeCompletedRun(false),
      { ...makeCompletedRun(false), id: 'run-2', languageCode: 'af', status: 'Failed' },
    ];
    expect(isStepReachable(7, talk, runs)).toBe(true);
  });

  it('returns false when a non-Pass section on one language is still pending review, even if another language is fully resolved', () => {
    const talk = makeTalk({ targetLanguageCodes: '["fr","af"]' });
    const runs: ValidationRunSummary[] = [
      makeCompletedRun(false),
      { ...makeCompletedRun(true), id: 'run-2', languageCode: 'af' },
    ];
    expect(isStepReachable(7, talk, runs)).toBe(false);
  });
});
