// ============================================
// Enums
// ============================================

export type ToolboxTalkFrequency = 'Once' | 'Weekly' | 'Monthly' | 'Annually';

export type VideoSource = 'None' | 'YouTube' | 'GoogleDrive' | 'Vimeo' | 'DirectUrl';

export type QuestionType = 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer';

export type ToolboxTalkScheduleStatus = 'Draft' | 'Active' | 'Completed' | 'Cancelled';

export type ScheduledTalkStatus = 'Pending' | 'InProgress' | 'Completed' | 'Overdue' | 'Cancelled';

/** Status of a learning in the content creation workflow */
export type ToolboxTalkStatus = 'Draft' | 'Processing' | 'ReadyForReview' | 'Published';

/** Source of content for learning sections and questions */
export type ContentSource = 'Manual' | 'Video' | 'Pdf' | 'Both';

// ============================================
// Learning DTOs
// ============================================

export interface ToolboxTalkSection {
  id: string;
  toolboxTalkId: string;
  sectionNumber: number;
  title: string;
  content: string;
  requiresAcknowledgment: boolean;
  source?: ContentSource;
  videoTimestamp?: string | null;
}

export interface ToolboxTalkQuestion {
  id: string;
  toolboxTalkId: string;
  questionNumber: number;
  questionText: string;
  questionType: QuestionType;
  questionTypeDisplay: string;
  options: string[] | null;
  correctAnswer: string | null;
  points: number;
  source?: ContentSource;
  videoTimestamp?: string | null;
}

export interface ToolboxTalkCompletionStats {
  totalAssignments: number;
  completedCount: number;
  overdueCount: number;
  pendingCount: number;
  inProgressCount: number;
  completionRate: number;
}

export interface ToolboxTalkTranslation {
  languageCode: string;
  language: string;
  translatedTitle: string;
  translatedAt: string;
  translationProvider: string;
}

export interface SlideDto {
  id: string;
  pageNumber: number;
  imageUrl: string;
  text?: string;
}

export interface ToolboxTalk {
  id: string;
  title: string;
  description: string | null;
  category: string | null;
  frequency: ToolboxTalkFrequency;
  frequencyDisplay: string;
  videoUrl: string | null;
  videoSource: VideoSource;
  videoSourceDisplay: string;
  attachmentUrl: string | null;
  minimumVideoWatchPercent: number;
  requiresQuiz: boolean;
  passingScore: number | null;
  isActive: boolean;
  status: ToolboxTalkStatus;
  statusDisplay: string;
  pdfUrl: string | null;
  pdfFileName: string | null;
  generatedFromVideo: boolean;
  generatedFromPdf: boolean;
  // Slideshow settings
  generateSlidesFromPdf: boolean;
  slidesGenerated: boolean;
  slideCount: number;
  slideshowHtml: string | null;
  slideshowGeneratedAt: string | null;
  hasSlideshow: boolean;
  // Quiz randomization settings
  quizQuestionCount: number | null;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  useQuestionPool: boolean;
  // Source language
  sourceLanguageCode: string;
  // Auto-assignment settings
  autoAssignToNewEmployees: boolean;
  autoAssignDueDays: number;
  // Certificate settings
  generateCertificate: boolean;
  // Refresher settings
  requiresRefresher: boolean;
  refresherIntervalMonths: number;
  sections: ToolboxTalkSection[];
  questions: ToolboxTalkQuestion[];
  translations: ToolboxTalkTranslation[];
  completionStats: ToolboxTalkCompletionStats | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ToolboxTalkListItem {
  id: string;
  title: string;
  description: string | null;
  category: string | null;
  frequency: ToolboxTalkFrequency;
  frequencyDisplay: string;
  isActive: boolean;
  hasVideo: boolean;
  hasPdf: boolean;
  requiresQuiz: boolean;
  sectionCount: number;
  questionCount: number;
  status: ToolboxTalkStatus;
  statusDisplay: string;
  autoAssignToNewEmployees: boolean;
  completionStats: ToolboxTalkCompletionStats | null;
  createdAt: string;
}

// ============================================
// Schedule DTOs
// ============================================

export interface ToolboxTalkScheduleAssignment {
  id: string;
  scheduleId: string;
  employeeId: string;
  employeeName: string;
  employeeEmail: string | null;
  isProcessed: boolean;
  processedAt: string | null;
}

export interface ToolboxTalkSchedule {
  id: string;
  toolboxTalkId: string;
  toolboxTalkTitle: string;
  scheduledDate: string;
  endDate: string | null;
  frequency: ToolboxTalkFrequency;
  frequencyDisplay: string;
  assignToAllEmployees: boolean;
  status: ToolboxTalkScheduleStatus;
  statusDisplay: string;
  nextRunDate: string | null;
  notes: string | null;
  assignmentCount: number;
  processedCount: number;
  assignments: ToolboxTalkScheduleAssignment[];
  createdAt: string;
  updatedAt: string | null;
}

export interface ToolboxTalkScheduleListItem {
  id: string;
  toolboxTalkId: string;
  toolboxTalkTitle: string;
  scheduledDate: string;
  endDate: string | null;
  frequency: ToolboxTalkFrequency;
  frequencyDisplay: string;
  assignToAllEmployees: boolean;
  status: ToolboxTalkScheduleStatus;
  statusDisplay: string;
  nextRunDate: string | null;
  assignmentCount: number;
  processedCount: number;
  createdAt: string;
}

// ============================================
// Scheduled Talk (Assignment) DTOs
// ============================================

export interface ScheduledTalkSectionProgress {
  id: string;
  scheduledTalkId: string;
  sectionId: string;
  sectionTitle: string;
  sectionNumber: number;
  isRead: boolean;
  readAt: string | null;
  timeSpentSeconds: number;
}

export interface ScheduledTalkQuizAttempt {
  id: string;
  scheduledTalkId: string;
  attemptNumber: number;
  answers: string;
  score: number;
  maxScore: number;
  percentage: number;
  passed: boolean;
  attemptedAt: string;
}

export interface ScheduledTalkCompletion {
  id: string;
  scheduledTalkId: string;
  completedAt: string;
  totalTimeSpentSeconds: number;
  videoWatchPercent: number | null;
  quizScore: number | null;
  quizMaxScore: number | null;
  quizPassed: boolean | null;
  signatureData: string;
  signedAt: string;
  signedByName: string;
  ipAddress: string | null;
  userAgent: string | null;
  certificateUrl: string | null;
  completedLatitude: number | null;
  completedLongitude: number | null;
  completedAccuracyMeters: number | null;
  completedLocationTimestamp: string | null;
}

export interface ScheduledTalk {
  id: string;
  toolboxTalkId: string;
  toolboxTalkTitle: string;
  employeeId: string;
  employeeName: string;
  employeeEmail: string | null;
  scheduleId: string | null;
  requiredDate: string;
  dueDate: string;
  status: ScheduledTalkStatus;
  statusDisplay: string;
  remindersSent: number;
  lastReminderAt: string | null;
  languageCode: string;
  totalSections: number;
  completedSections: number;
  progressPercent: number;
  sectionProgress: ScheduledTalkSectionProgress[];
  quizAttempts: ScheduledTalkQuizAttempt[];
  completion: ScheduledTalkCompletion | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ScheduledTalkListItem {
  id: string;
  toolboxTalkId: string;
  toolboxTalkTitle: string;
  employeeId: string;
  employeeName: string;
  employeeEmail: string | null;
  scheduleId: string | null;
  requiredDate: string;
  dueDate: string;
  status: ScheduledTalkStatus;
  statusDisplay: string;
  remindersSent: number;
  totalSections: number;
  completedSections: number;
  progressPercent: number;
  createdAt: string;
}

// ============================================
// My Learning DTOs (Employee Portal)
// ============================================

export interface MyToolboxTalkSection {
  sectionId: string;
  sectionNumber: number;
  title: string;
  content: string;
  requiresAcknowledgment: boolean;
  isRead: boolean;
  readAt: string | null;
  timeSpentSeconds: number;
}

export interface MyToolboxTalkQuestion {
  id: string;
  questionNumber: number;
  questionText: string;
  questionType: QuestionType;
  questionTypeDisplay: string;
  options: string[] | null;
  points: number;
  /** Maps each display position to its original option index (for shuffled quizzes).
   *  null for non-shuffled quizzes (display index = original index). */
  optionOriginalIndices: number[] | null;
}

export interface MyToolboxTalk {
  scheduledTalkId: string;
  toolboxTalkId: string;
  title: string;
  description: string | null;
  videoUrl: string | null;
  videoSource: VideoSource;
  attachmentUrl: string | null;
  pdfUrl: string | null;
  pdfFileName: string | null;
  minimumVideoWatchPercent: number;
  requiresQuiz: boolean;
  passingScore: number | null;
  requiredDate: string;
  dueDate: string;
  status: ScheduledTalkStatus;
  statusDisplay: string;
  languageCode: string;
  /** Employee's preferred language for subtitle selection (e.g., "es", "pl", "ro") */
  employeePreferredLanguage: string;
  totalSections: number;
  completedSections: number;
  progressPercent: number;
  videoWatchPercent: number | null;
  quizAttemptCount: number;
  lastQuizPassed: boolean | null;
  lastQuizScore: number | null;
  hasSlideshow: boolean;
  sections: MyToolboxTalkSection[];
  questions: MyToolboxTalkQuestion[];
  completedAt: string | null;
  certificateUrl: string | null;
  isOverdue: boolean;
  daysUntilDue: number;
}

export interface MyToolboxTalkListItem {
  scheduledTalkId: string;
  toolboxTalkId: string;
  title: string;
  description: string | null;
  requiredDate: string;
  dueDate: string;
  status: ScheduledTalkStatus;
  statusDisplay: string;
  hasVideo: boolean;
  requiresQuiz: boolean;
  totalSections: number;
  completedSections: number;
  progressPercent: number;
  isOverdue: boolean;
  daysUntilDue: number;
  isRefresher: boolean;
  refresherDueDate: string | null;
}

// ============================================
// My Training Summary DTO
// ============================================

export interface MyTrainingSummary {
  pendingCount: number;
  inProgressCount: number;
  overdueCount: number;
  totalCount: number;
}

// ============================================
// Dashboard DTOs
// ============================================

export interface RecentCompletion {
  scheduledTalkId: string;
  employeeName: string;
  toolboxTalkTitle: string;
  completedAt: string;
  totalTimeSpentSeconds: number;
  quizPassed: boolean | null;
  quizScore: number | null;
}

export interface OverdueAssignment {
  scheduledTalkId: string;
  employeeId: string;
  employeeName: string;
  employeeEmail: string | null;
  toolboxTalkTitle: string;
  dueDate: string;
  daysOverdue: number;
  remindersSent: number;
  status: ScheduledTalkStatus;
}

export interface UpcomingSchedule {
  scheduleId: string;
  toolboxTalkTitle: string;
  scheduledDate: string;
  frequency: ToolboxTalkFrequency;
  frequencyDisplay: string;
  assignmentCount: number;
  assignToAllEmployees: boolean;
}

export interface ToolboxTalkDashboard {
  totalTalks: number;
  activeTalks: number;
  inactiveTalks: number;
  totalAssignments: number;
  pendingCount: number;
  inProgressCount: number;
  completedCount: number;
  overdueCount: number;
  completionRate: number;
  overdueRate: number;
  averageCompletionTimeHours: number;
  averageQuizScore: number;
  quizPassRate: number;
  talksByStatus: Record<ScheduledTalkStatus, number>;
  talksByFrequency: Record<ToolboxTalkFrequency, number>;
  recentCompletions: RecentCompletion[];
  overdueAssignments: OverdueAssignment[];
  upcomingSchedules: UpcomingSchedule[];
}

// ============================================
// Settings DTOs
// ============================================

export interface ToolboxTalkSettings {
  id: string;
  tenantId: string;
  defaultDueDays: number;
  reminderFrequencyDays: number;
  maxReminders: number;
  escalateAfterReminders: number;
  requireVideoCompletion: boolean;
  defaultPassingScore: number;
  enableTranslation: boolean;
  translationProvider: string | null;
  enableVideoDubbing: boolean;
  videoDubbingProvider: string | null;
  notificationEmailTemplate: string | null;
  reminderEmailTemplate: string | null;
}

export interface UpdateToolboxTalkSettingsRequest {
  defaultDueDays?: number;
  reminderDaysBefore?: number;
  sendEmailReminders?: boolean;
  sendPushReminders?: boolean;
  maxQuizAttempts?: number;
  requireSignature?: boolean;
  autoAssignNewEmployees?: boolean;
}

// ============================================
// Quiz DTOs
// ============================================

export interface QuestionResult {
  questionId: string;
  questionNumber: number;
  questionText: string;
  submittedAnswer: string;
  isCorrect: boolean;
  correctAnswer: string;
  /** Index of correct option in original Options array (0-based). Null for non-MC questions. */
  correctOptionIndex: number | null;
  pointsEarned: number;
  maxPoints: number;
}

export interface QuizResult {
  attemptId: string;
  attemptNumber: number;
  score: number;
  maxScore: number;
  percentage: number;
  passed: boolean;
  passingScore: number;
  questionResults: QuestionResult[];
}

// ============================================
// Video Progress DTOs
// ============================================

export interface VideoProgress {
  watchPercent: number;
  minimumWatchPercent: number;
  requirementMet: boolean;
}

// ============================================
// Process Schedule DTOs
// ============================================

export interface ProcessScheduleResult {
  scheduleId: string;
  talksCreated: number;
  scheduleCompleted: boolean;
  nextRunDate: string | null;
  message: string;
}

// ============================================
// Request DTOs
// ============================================

export interface CreateToolboxTalkSectionRequest {
  id?: string;
  sectionNumber: number;
  title: string;
  content: string;
  requiresAcknowledgment?: boolean;
  source?: ContentSource;
}

export interface CreateToolboxTalkQuestionRequest {
  id?: string;
  questionNumber: number;
  questionText: string;
  questionType: QuestionType;
  options?: string[];
  correctAnswer: string;
  points?: number;
  source?: ContentSource;
}

export interface CreateToolboxTalkRequest {
  title: string;
  description?: string;
  category?: string;
  frequency: ToolboxTalkFrequency;
  videoUrl?: string;
  videoSource: VideoSource;
  attachmentUrl?: string;
  minimumVideoWatchPercent?: number;
  requiresQuiz?: boolean;
  passingScore?: number;
  isActive?: boolean;
  // Quiz randomization settings
  quizQuestionCount?: number | null;
  shuffleQuestions?: boolean;
  shuffleOptions?: boolean;
  useQuestionPool?: boolean;
  // Source language
  sourceLanguageCode?: string;
  // Auto-assignment settings
  autoAssignToNewEmployees?: boolean;
  autoAssignDueDays?: number;
  // Slideshow settings
  generateSlidesFromPdf?: boolean;
  // Certificate settings
  generateCertificate?: boolean;
  // Refresher settings
  requiresRefresher?: boolean;
  refresherIntervalMonths?: number;
  sections: CreateToolboxTalkSectionRequest[];
  questions?: CreateToolboxTalkQuestionRequest[];
}

export interface UpdateToolboxTalkRequest extends CreateToolboxTalkRequest {
  id: string;
}

export interface CreateToolboxTalkScheduleRequest {
  toolboxTalkId: string;
  scheduledDate: string;
  endDate?: string;
  frequency: ToolboxTalkFrequency;
  assignToAllEmployees: boolean;
  employeeIds?: string[];
  notes?: string;
}

export interface UpdateToolboxTalkScheduleRequest extends CreateToolboxTalkScheduleRequest {
  id: string;
}

export interface MarkSectionReadRequest {
  timeSpentSeconds?: number;
}

export interface SubmitQuizRequest {
  answers: Record<string, string>;
}

export interface UpdateVideoProgressRequest {
  watchPercent: number;
}

export interface StartTalkRequest {
  latitude?: number;
  longitude?: number;
  accuracyMeters?: number;
}

export interface CompleteToolboxTalkRequest {
  signatureData: string;
  signedByName: string;
  latitude?: number;
  longitude?: number;
  accuracyMeters?: number;
}

// ============================================
// Query Parameters
// ============================================

export interface GetToolboxTalksParams {
  searchTerm?: string;
  frequency?: ToolboxTalkFrequency;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

export interface GetToolboxTalkSchedulesParams {
  toolboxTalkId?: string;
  status?: ToolboxTalkScheduleStatus;
  dateFrom?: string;
  dateTo?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface GetScheduledTalksParams {
  toolboxTalkId?: string;
  employeeId?: string;
  scheduleId?: string;
  status?: ScheduledTalkStatus;
  pageNumber?: number;
  pageSize?: number;
}

export interface GetMyToolboxTalksParams {
  status?: ScheduledTalkStatus;
  pageNumber?: number;
  pageSize?: number;
}

// ============================================
// Report DTOs
// ============================================

export interface DepartmentCompliance {
  siteId: string | null;
  departmentName: string;
  totalEmployees: number;
  assignedCount: number;
  completedCount: number;
  compliancePercentage: number;
  overdueCount: number;
}

export interface TalkCompliance {
  toolboxTalkId: string;
  title: string;
  assignedCount: number;
  completedCount: number;
  compliancePercentage: number;
  overdueCount: number;
  averageQuizScore: number | null;
  quizPassRate: number | null;
}

export interface ComplianceReport {
  totalEmployees: number;
  assignedCount: number;
  completedCount: number;
  compliancePercentage: number;
  overdueCount: number;
  pendingCount: number;
  inProgressCount: number;
  byDepartment: DepartmentCompliance[];
  byTalk: TalkCompliance[];
  dateFrom: string | null;
  dateTo: string | null;
  generatedAt: string;
}

export interface OverdueItem {
  scheduledTalkId: string;
  employeeId: string;
  employeeName: string;
  email: string | null;
  siteName: string | null;
  toolboxTalkId: string;
  talkTitle: string;
  dueDate: string;
  daysOverdue: number;
  remindersSent: number;
  lastReminderAt: string | null;
  isInProgress: boolean;
  videoWatchPercent: number;
}

export interface CompletionDetail {
  scheduledTalkId: string;
  completionId: string;
  employeeId: string;
  employeeName: string;
  email: string | null;
  siteName: string | null;
  toolboxTalkId: string;
  talkTitle: string;
  requiredDate: string;
  dueDate: string;
  completedAt: string;
  timeSpentMinutes: number;
  videoWatchPercent: number | null;
  quizScore: number | null;
  quizMaxScore: number | null;
  quizPassed: boolean | null;
  quizScorePercentage: number | null;
  signedByName: string;
  signedAt: string;
  completedOnTime: boolean;
  certificateUrl: string | null;
  startedLatitude: number | null;
  startedLongitude: number | null;
  startedAccuracyMeters: number | null;
  startedLocationTimestamp: string | null;
  completedLatitude: number | null;
  completedLongitude: number | null;
  completedAccuracyMeters: number | null;
  completedLocationTimestamp: string | null;
}

// ============================================
// Paginated Response
// ============================================

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// ============================================
// Subtitle Processing Types
// ============================================

export type SubtitleProcessingStatus =
  | 'Pending'
  | 'Transcribing'
  | 'Translating'
  | 'Uploading'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export type SubtitleTranslationStatus =
  | 'Pending'
  | 'InProgress'
  | 'Completed'
  | 'Failed';

export type SubtitleVideoSourceType =
  | 'GoogleDrive'
  | 'AzureBlob'
  | 'DirectUrl';

export interface LanguageStatus {
  language: string;
  languageCode: string;
  status: SubtitleTranslationStatus;
  percentage: number;
  srtUrl?: string;
  errorMessage?: string;
}

export interface SubtitleProcessingStatusResponse {
  jobId: string;
  toolboxTalkId: string;
  status: SubtitleProcessingStatus;
  overallPercentage: number;
  currentStep: string;
  errorMessage?: string;
  startedAt?: string;
  completedAt?: string;
  totalSubtitles: number;
  languages: LanguageStatus[];
}

export interface StartSubtitleProcessingRequest {
  videoUrl: string;
  videoSourceType: SubtitleVideoSourceType;
  targetLanguages: string[];
}

export interface StartProcessingResponse {
  jobId: string;
  message: string;
  statusUrl: string;
}

export interface EmployeeLanguageInfo {
  language: string;
  languageCode: string;
  employeeCount: number;
}

export interface SupportedLanguageInfo {
  language: string;
  languageCode: string;
}

export interface AvailableLanguagesResponse {
  employeeLanguages: EmployeeLanguageInfo[];
  allSupportedLanguages: SupportedLanguageInfo[];
}

export interface SubtitleProgressUpdate {
  overallStatus: SubtitleProcessingStatus;
  overallPercentage: number;
  currentStep: string;
  errorMessage?: string;
  languages: LanguageStatus[];
}

// ============================================
// Content Deduplication DTOs
// ============================================

/** File type for deduplication checking */
export type FileHashType = 'PDF' | 'Video';

/** Request to check for duplicate content */
export interface CheckDuplicateRequest {
  /** The file hash (SHA-256) to check for duplicates. If not provided, fileUrl must be provided. */
  fileHash?: string;
  /** The file URL to calculate hash from. Used if fileHash is not provided. */
  fileUrl?: string;
  /** Type of file: "PDF" or "Video" */
  fileType: FileHashType;
}

/** Information about a source learning for content reuse */
export interface SourceToolboxTalkInfo {
  /** ID of the source learning */
  id: string;
  /** Title of the source learning */
  title: string;
  /** When the content was originally generated */
  processedAt: string | null;
  /** Number of sections in the source */
  sectionCount: number;
  /** Number of questions in the source */
  questionCount: number;
  /** Whether the source has an HTML slideshow */
  hasSlideshow: boolean;
  /** Languages that have translations available */
  translationLanguages: string[];
}

/** Response from duplicate check */
export interface DuplicateCheckResponse {
  /** Whether a duplicate was found */
  isDuplicate: boolean;
  /** The calculated file hash */
  fileHash: string;
  /** Information about the source learning if a duplicate was found */
  sourceToolboxTalk: SourceToolboxTalkInfo | null;
}

/** Request to reuse content from another learning */
export interface ReuseContentRequest {
  /** ID of the source learning to copy content from */
  sourceToolboxTalkId: string;
}

/** Response from content reuse operation */
export interface ContentReuseResponse {
  /** Whether the reuse was successful */
  success: boolean;
  /** Number of sections copied */
  sectionsCopied: number;
  /** Number of questions copied */
  questionsCopied: number;
  /** Number of translations copied */
  translationsCopied: number;
  /** Status message */
  message: string;
}

// ============================================
// Preview DTOs (Admin viewing as employee)
// ============================================

export interface PreviewSectionDto {
  id: string;
  sectionNumber: number;
  title: string;
  content: string;
  requiresAcknowledgment: boolean;
}

export interface PreviewQuestionDto {
  id: string;
  questionNumber: number;
  questionText: string;
  questionType: QuestionType;
  questionTypeDisplay: string;
  options: string[] | null;
  points: number;
}

export interface ToolboxTalkPreview {
  id: string;
  title: string;
  description: string | null;
  category: string | null;
  videoUrl: string | null;
  videoSource: VideoSource;
  requiresQuiz: boolean;
  passingScore: number | null;
  slidesGenerated: boolean;
  slideCount: number;
  slideshowHtml: string | null;
  slideshowGeneratedAt: string | null;
  hasSlideshow: boolean;
  sourceLanguageCode: string;
  previewLanguageCode: string;
  availableTranslations: ToolboxTalkTranslation[];
  sections: PreviewSectionDto[];
  questions: PreviewQuestionDto[];
}

export interface SlideshowHtmlResponse {
  html: string;
  languageCode: string;
  isTranslated: boolean;
  generatedAt: string;
}

/** Request to update file hash */
export interface UpdateFileHashRequest {
  /** The file hash (SHA-256). If not provided, fileUrl must be provided. */
  fileHash?: string;
  /** The file URL to calculate hash from. Used if fileHash is not provided. */
  fileUrl?: string;
  /** Type of file: "PDF" or "Video" */
  fileType: FileHashType;
}

// ============================================
// Smart Content Generation
// ============================================

/** Request for smart content generation (auto reuse + generate missing) */
export interface SmartGenerateContentRequest {
  generateSections: boolean;
  generateQuestions: boolean;
  generateSlideshow: boolean;
  slideshowSource?: 'none' | 'pdf' | 'video';
  includeVideo: boolean;
  includePdf: boolean;
  sourceLanguageCode: string;
  minimumSections?: number;
  minimumQuestions?: number;
  passThreshold?: number;
  connectionId?: string;
}

/** Response from smart content generation */
export interface SmartGenerateContentResult {
  // What was copied from existing source
  sectionsCopied: number;
  questionsCopied: number;
  slideshowCopied: boolean;
  translationsCopied: number;
  // What was generated via AI (0 if generation is queued as background job)
  sectionsGenerated: number;
  questionsGenerated: number;
  slideshowGenerated: boolean;
  // Source info
  contentCopiedFromTitle?: string;
  // Background job info (if AI generation was needed)
  generationJobQueued: boolean;
  generationJobId?: string;
}
