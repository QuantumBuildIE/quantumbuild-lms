export type ParseInputType = 'Pdf' | 'Docx' | 'Url' | 'Text';
export type ParseJobStatus = 'Processing' | 'Completed' | 'Failed';
export type TranslationQueueStatus = 'NotRequired' | 'Queued' | 'PartialFailure' | 'Failed' | 'Completed';

export interface ParseJob {
  id: string;
  inputType: ParseInputType;
  inputReference: string;
  status: ParseJobStatus;
  generatedCourseId: string | null;
  generatedCourseTitle: string | null;
  talksGenerated: number;
  errorMessage: string | null;
  translationStatus: TranslationQueueStatus;
  translationLanguages: string | null;
  translationsQueued: number;
  translationFailures: string | null;
  createdAt: string;
  createdBy: string;
}

export interface LessonParseProgress {
  stage: string;
  percentComplete: number;
  currentTalk: number;
  totalTalks: number;
}

export interface LessonParseResult {
  courseId: string;
  courseTitle: string;
  talksGenerated: number;
  translationsQueued: boolean;
  translationLanguages: string[];
  translationJobCount: number;
}

export interface StartParseResponse {
  jobId: string;
  message: string;
}

export interface ParseJobsResponse {
  items: ParseJob[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}
