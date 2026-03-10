import { apiClient } from '@/lib/api/client';
import type {
  ContentCreationSession,
  ContentCreationSettings,
  CreateSessionRequest,
  UpdateSectionsRequest,
  StartTranslateValidateRequest,
  PublishRequest,
  PublishResult,
  ValidationRunDetail,
  ValidationRunSummary,
  QuizData,
  QuizQuestion,
  QuizSettings,
} from '@/types/content-creation';

// ============================================
// Content Creation Session API
// ============================================

/**
 * Create a new content creation session
 */
export async function createSession(
  request: CreateSessionRequest
): Promise<ContentCreationSession> {
  const response = await apiClient.post<ContentCreationSession>(
    '/toolbox-talks/create/session',
    request
  );
  return response.data;
}

/**
 * Get current session state
 */
export async function getSession(
  sessionId: string
): Promise<ContentCreationSession> {
  const response = await apiClient.get<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}`
  );
  return response.data;
}

/**
 * Upload a file to the session (PDF or video)
 */
export async function uploadSessionFile(
  sessionId: string,
  file: File,
  onProgress?: (percent: number) => void
): Promise<ContentCreationSession> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/upload`,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const percent = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total
          );
          onProgress(percent);
        }
      },
    }
  );
  return response.data;
}

/**
 * Update source content and reset session to Draft for re-parsing
 */
export async function updateSessionSource(
  sessionId: string,
  sourceText?: string
): Promise<ContentCreationSession> {
  const response = await apiClient.put<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/source`,
    { sourceText }
  );
  return response.data;
}

/**
 * Trigger content parsing for the session
 */
export async function parseSessionContent(
  sessionId: string
): Promise<ContentCreationSession> {
  const response = await apiClient.post<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/parse`
  );
  return response.data;
}

/**
 * Update sections and confirm output type
 */
export async function updateSessionSections(
  sessionId: string,
  request: UpdateSectionsRequest
): Promise<ContentCreationSession> {
  const response = await apiClient.put<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/sections`,
    request
  );
  return response.data;
}

/**
 * Start translation & validation (returns 202 Accepted)
 */
export async function startTranslateValidate(
  sessionId: string,
  request: StartTranslateValidateRequest
): Promise<ContentCreationSession> {
  const response = await apiClient.post<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/translate-validate`,
    request
  );
  return response.data;
}

/**
 * Publish session as Talk or Course
 */
export async function publishSession(
  sessionId: string,
  request: PublishRequest
): Promise<PublishResult> {
  const response = await apiClient.post<PublishResult>(
    `/toolbox-talks/create/session/${sessionId}/publish`,
    request
  );
  return response.data;
}

/**
 * Abandon and clean up session
 */
export async function abandonSession(sessionId: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/create/session/${sessionId}`);
}

// ============================================
// Quiz API (session context)
// ============================================

/**
 * Generate quiz questions from session content using AI
 */
export async function generateSessionQuiz(
  sessionId: string
): Promise<ContentCreationSession> {
  const response = await apiClient.post<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/generate-quiz`
  );
  return response.data;
}

/**
 * Get quiz questions and settings for a session
 */
export async function getSessionQuizData(
  sessionId: string
): Promise<QuizData> {
  const response = await apiClient.get<QuizData>(
    `/toolbox-talks/create/session/${sessionId}/quiz`
  );
  return response.data;
}

/**
 * Update quiz questions for a session
 */
export async function updateSessionQuestions(
  sessionId: string,
  questions: QuizQuestion[]
): Promise<ContentCreationSession> {
  const response = await apiClient.put<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/questions`,
    { questions }
  );
  return response.data;
}

/**
 * Update quiz settings for a session
 */
export async function updateSessionQuizSettings(
  sessionId: string,
  settings: QuizSettings
): Promise<ContentCreationSession> {
  const response = await apiClient.put<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/quiz-settings`,
    settings
  );
  return response.data;
}

// ============================================
// Settings API (session context)
// ============================================

/**
 * Get session settings (title, category, behaviour)
 */
export async function getSessionSettings(
  sessionId: string
): Promise<ContentCreationSettings> {
  const response = await apiClient.get<ContentCreationSettings>(
    `/toolbox-talks/create/session/${sessionId}/settings`
  );
  return response.data;
}

/**
 * Update session settings
 */
export async function updateSessionSettings(
  sessionId: string,
  settings: ContentCreationSettings
): Promise<ContentCreationSession> {
  const response = await apiClient.put<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/settings`,
    settings
  );
  return response.data;
}

/**
 * Upload cover image for session
 */
export async function uploadSessionCoverImage(
  sessionId: string,
  file: File
): Promise<ContentCreationSession> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/cover-image`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );
  return response.data;
}

// ============================================
// Validation API (per-run endpoints)
// ============================================

/**
 * Get validation run details with results
 */
export async function getValidationRun(
  talkId: string,
  runId: string
): Promise<ValidationRunDetail> {
  const response = await apiClient.get<ValidationRunDetail>(
    `/toolbox-talks/${talkId}/validation/runs/${runId}`
  );
  return response.data;
}

/**
 * List validation runs for a talk
 */
export async function getValidationRuns(
  talkId: string
): Promise<ValidationRunSummary[]> {
  const response = await apiClient.get<ValidationRunSummary[]>(
    `/toolbox-talks/${talkId}/validation/runs`
  );
  return response.data;
}

/**
 * Delete (soft) a validation run
 */
export async function deleteValidationRun(
  talkId: string,
  runId: string
): Promise<void> {
  await apiClient.delete(
    `/toolbox-talks/${talkId}/validation/runs/${runId}`
  );
}

/**
 * Download audit report for a validation run (returns blob)
 */
export async function downloadValidationReport(
  talkId: string,
  runId: string
): Promise<Blob> {
  const response = await apiClient.get(
    `/toolbox-talks/${talkId}/validation/runs/${runId}/report`,
    { responseType: 'blob' }
  );
  return response.data;
}

/**
 * Trigger report generation for a validation run that has no report yet
 */
export async function generateValidationReport(
  talkId: string,
  runId: string
): Promise<void> {
  await apiClient.post(
    `/toolbox-talks/${talkId}/validation/runs/${runId}/report/generate`
  );
}

/**
 * Accept a section translation
 */
export async function acceptSection(
  talkId: string,
  runId: string,
  sectionIndex: number
): Promise<void> {
  await apiClient.put(
    `/toolbox-talks/${talkId}/validation/runs/${runId}/sections/${sectionIndex}/accept`
  );
}

/**
 * Reject a section translation
 */
export async function rejectSection(
  talkId: string,
  runId: string,
  sectionIndex: number
): Promise<void> {
  await apiClient.put(
    `/toolbox-talks/${talkId}/validation/runs/${runId}/sections/${sectionIndex}/reject`
  );
}

/**
 * Edit a section translation and re-validate
 */
export async function editSection(
  talkId: string,
  runId: string,
  sectionIndex: number,
  editedTranslation: string
): Promise<void> {
  await apiClient.put(
    `/toolbox-talks/${talkId}/validation/runs/${runId}/sections/${sectionIndex}/edit`,
    { editedTranslation }
  );
}

/**
 * Retry validation for a section
 */
export async function retrySection(
  talkId: string,
  runId: string,
  sectionIndex: number
): Promise<void> {
  await apiClient.post(
    `/toolbox-talks/${talkId}/validation/runs/${runId}/sections/${sectionIndex}/retry`
  );
}

// ============================================
// Session-Context Validation API
// (Used during creation wizard — delegates to talk-context endpoints
//  using the session's outputId as talkId)
// ============================================

/**
 * Get validation run details via session context.
 * Requires the session's outputId (talkId) which is set after content generation.
 */
export async function getSessionValidationRun(
  talkId: string,
  runId: string
): Promise<ValidationRunDetail> {
  return getValidationRun(talkId, runId);
}

/**
 * Accept a section translation via session context
 */
export async function acceptSessionSection(
  talkId: string,
  runId: string,
  sectionIndex: number
): Promise<void> {
  return acceptSection(talkId, runId, sectionIndex);
}

/**
 * Reject a section translation via session context
 */
export async function rejectSessionSection(
  talkId: string,
  runId: string,
  sectionIndex: number
): Promise<void> {
  return rejectSection(talkId, runId, sectionIndex);
}

/**
 * Edit a section translation via session context
 */
export async function editSessionSection(
  talkId: string,
  runId: string,
  sectionIndex: number,
  editedTranslation: string
): Promise<void> {
  return editSection(talkId, runId, sectionIndex, editedTranslation);
}

/**
 * Retry validation for a section via session context
 */
export async function retrySessionSection(
  talkId: string,
  runId: string,
  sectionIndex: number
): Promise<void> {
  return retrySection(talkId, runId, sectionIndex);
}
