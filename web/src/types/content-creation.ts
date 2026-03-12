// ============================================
// Content Creation Session Types
// ============================================

export type InputMode = 'Text' | 'Pdf' | 'Video';

export type OutputType = 'Lesson' | 'Course';

export type ContentCreationSessionStatus =
  | 'Draft'
  | 'Parsing'
  | 'Parsed'
  | 'GeneratingQuiz'
  | 'QuizGenerated'
  | 'TranslatingValidating'
  | 'Validated'
  | 'Publishing'
  | 'Completed'
  | 'Abandoned'
  | 'Failed';

export type ValidationRunStatus =
  | 'Pending'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export type ValidationOutcome = 'Pass' | 'Review' | 'Fail';

export type ReviewerDecision = 'Pending' | 'Accepted' | 'Rejected' | 'Edited';

// ============================================
// Session DTOs
// ============================================

export interface ContentCreationSession {
  id: string;
  inputMode: InputMode;
  status: ContentCreationSessionStatus;
  sourceText: string | null;
  sourceFileName: string | null;
  sourceFileUrl: string | null;
  sourceFileType: string | null;
  transcriptText: string | null;
  parsedSectionsJson: string | null;
  outputType: OutputType | null;
  outputTalkId: string | null;
  outputCourseId: string | null;
  targetLanguageCodes: string | null;
  passThreshold: number;
  sectorKey: string | null;
  reviewerName: string | null;
  reviewerOrg: string | null;
  reviewerRole: string | null;
  documentRef: string | null;
  clientName: string | null;
  auditPurpose: string | null;
  expiresAt: string;
  validationRunIds: string | null;
  questionsJson: string | null;
  quizSettingsJson: string | null;
  settingsJson: string | null;
  subtitleJobId: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ParsedSection {
  title: string;
  content: string;
  suggestedOrder: number;
}

export interface ContentParseResult {
  success: boolean;
  sections: ParsedSection[];
  suggestedOutputType: OutputType;
  errorMessage: string | null;
  tokensUsed: number;
}

// ============================================
// Request DTOs
// ============================================

export interface CreateSessionRequest {
  inputMode: InputMode;
  sourceText?: string;
  sectorKey?: string;
  passThreshold?: number;
  reviewerName?: string;
  reviewerOrg?: string;
  reviewerRole?: string;
  documentRef?: string;
  clientName?: string;
  auditPurpose?: string;
}

export interface UpdateSectionsRequest {
  sections: UpdatedSection[];
  outputType: OutputType;
}

export interface UpdatedSection {
  title: string;
  content: string;
  order: number;
}

export interface StartTranslateValidateRequest {
  targetLanguageCodes: string[];
}

export interface PublishRequest {
  title: string;
  description?: string;
  category?: string;
  code?: string;
  sourceLanguageCode?: string;
}

export interface PublishResult {
  success: boolean;
  outputId: string | null; // Effective output ID (talkId for Lesson, courseId for Course)
  outputType: OutputType | null;
  errorMessage: string | null;
}

// ============================================
// Validation DTOs
// ============================================

export interface ValidationRunSummary {
  id: string;
  languageCode: string;
  sectorKey: string | null;
  overallScore: number;
  overallOutcome: ValidationOutcome;
  safetyVerdict: ValidationOutcome | null;
  status: ValidationRunStatus;
  totalSections: number;
  passedSections: number;
  reviewSections: number;
  failedSections: number;
  passThreshold: number;
  auditReportUrl: string | null;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
}

export interface ValidationRunDetail {
  id: string;
  toolboxTalkId: string | null;
  courseId: string | null;
  languageCode: string;
  sectorKey: string | null;
  sourceLanguage: string;
  sourceDialect: string | null;
  passThreshold: number;
  overallScore: number;
  overallOutcome: ValidationOutcome;
  safetyVerdict: ValidationOutcome | null;
  totalSections: number;
  passedSections: number;
  reviewSections: number;
  failedSections: number;
  status: ValidationRunStatus;
  auditReportUrl: string | null;
  reviewerName: string | null;
  reviewerOrg: string | null;
  reviewerRole: string | null;
  documentRef: string | null;
  clientName: string | null;
  auditPurpose: string | null;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
  results: SectionValidationResult[];
}

export interface SectionValidationResult {
  id: string;
  sectionIndex: number;
  sectionTitle: string;
  originalText: string;
  translatedText: string;
  backTranslationA: string | null;
  backTranslationB: string | null;
  backTranslationC: string | null;
  backTranslationD: string | null;
  scoreA: number;
  scoreB: number;
  scoreC: number | null;
  scoreD: number | null;
  finalScore: number;
  roundsUsed: number;
  outcome: ValidationOutcome;
  engineOutcome: ValidationOutcome;
  isSafetyCritical: boolean;
  criticalTerms: string | null;
  glossaryMismatches: string | null;
  effectiveThreshold: number;
  reviewerDecision: ReviewerDecision;
  editedTranslation: string | null;
  decisionAt: string | null;
  decisionBy: string | null;
}

// ============================================
// Quiz Types
// ============================================

export type QuestionType = 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer';

export interface QuizQuestion {
  id: string;
  sectionIndex: number;
  questionText: string;
  questionType: QuestionType;
  options: string[];
  correctAnswerIndex: number;
  points: number;
  isAiGenerated: boolean;
}

export interface QuizSettings {
  requireQuiz: boolean;
  passingScore: number;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  allowRetry: boolean;
}

export interface QuizData {
  questions: QuizQuestion[];
  settings: QuizSettings;
}

export interface UpdateQuestionsRequest {
  questions: QuizQuestion[];
}

// ============================================
// Settings Types
// ============================================

export interface ContentCreationSettings {
  title: string;
  description: string;
  coverImageUrl: string | null;
  category: string | null;
  refresherFrequency: 'Once' | 'Monthly' | 'Quarterly' | 'Annually';
  isActiveOnPublish: boolean;
  generateCertificate: boolean;
  minimumWatchPercent: number;
  autoAssign: boolean;
  autoAssignDueDays: number;
  generateSlideshow: boolean;
  slideshowSource: string;
}

// ============================================
// SignalR Event Types
// ============================================

export interface ValidationProgressEvent {
  runId: string;
  sessionId: string;
  languageCode: string;
  sectionIndex: number;
  totalSections: number;
  stage: string;
  percentComplete: number;
  message: string;
}

export interface SectionCompletedEvent {
  runId: string;
  sessionId: string;
  sectionIndex: number;
  sectionTitle: string;
  result: SectionValidationResult;
}

// ============================================
// Wizard Step State
// ============================================

export type WizardStep = 1 | 2 | 3 | 4 | 5 | 6;

export const WIZARD_STEPS = [
  { step: 1 as const, label: 'Input & Config' },
  { step: 2 as const, label: 'Parse' },
  { step: 3 as const, label: 'Quiz' },
  { step: 4 as const, label: 'Settings' },
  { step: 5 as const, label: 'Translate & Validate' },
  { step: 6 as const, label: 'Publish' },
] as const;
