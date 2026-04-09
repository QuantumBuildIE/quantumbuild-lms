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
  ValidationScoreType,
  RegulatoryScoreResultDto,
  RegulatoryScoreHistoryDto,
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
 * Upload a file to the session (PDF or video).
 * Legacy path: browser → Railway → R2. Use uploadFileDirectToR2 for the direct path.
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

interface UploadUrlResult {
  uploadUrl: string;
  key: string;
  publicUrl: string;
}

/**
 * Get a presigned PUT URL for direct browser-to-R2 upload.
 */
export async function getSessionUploadUrl(
  sessionId: string,
  fileName: string,
  contentType: string
): Promise<UploadUrlResult> {
  const response = await apiClient.get<UploadUrlResult>(
    `/toolbox-talks/create/session/${sessionId}/upload-url`,
    { params: { fileName, contentType } }
  );
  return response.data;
}

/**
 * Confirm a completed direct upload and record the file URL on the session.
 * Call after a successful PUT to the presigned URL.
 */
export async function confirmSessionUpload(
  sessionId: string,
  key: string,
  fileName: string,
  fileType: string
): Promise<ContentCreationSession> {
  const response = await apiClient.post<ContentCreationSession>(
    `/toolbox-talks/create/session/${sessionId}/upload-complete`,
    { key, fileName, fileType }
  );
  return response.data;
}

/**
 * Upload a file directly to R2, bypassing the Railway server.
 * Flow: get presigned URL → PUT file to R2 → confirm upload via API.
 *
 * Progress is reported 0-100 based on the PUT to R2.
 * The PUT uses XMLHttpRequest (not Axios) to avoid injecting auth headers
 * that would invalidate the presigned URL signature.
 */
export async function uploadFileDirectToR2(
  sessionId: string,
  file: File,
  onProgress?: (percent: number) => void
): Promise<ContentCreationSession> {
  // 1. Get the presigned PUT URL from the API
  const { uploadUrl, key } = await getSessionUploadUrl(
    sessionId,
    file.name,
    file.type
  );

  // 2. PUT the file directly to R2
  // DO NOT use Axios here — it injects auth headers that invalidate the presigned signature
  await new Promise<void>((resolve, reject) => {
    const xhr = new XMLHttpRequest();

    xhr.upload.onprogress = (event) => {
      if (onProgress && event.lengthComputable) {
        const percent = Math.round((event.loaded * 100) / event.total);
        onProgress(percent);
      }
    };

    xhr.onload = () => {
      if (xhr.status === 200 || xhr.status === 204) {
        resolve();
      } else {
        reject(new Error(`R2 upload failed with status ${xhr.status}: ${xhr.responseText}`));
      }
    };

    xhr.onerror = () => reject(new Error('R2 upload failed (network error)'));
    xhr.onabort = () => reject(new Error('R2 upload aborted'));

    xhr.open('PUT', uploadUrl);
    // Content-Type must match the value used to generate the presigned URL — R2 rejects mismatches with 403
    xhr.setRequestHeader('Content-Type', file.type);
    xhr.send(file);
  });

  // 3. Tell the API the upload is done and get the updated session
  const fileType = file.type.startsWith('video/') ? 'Video' : 'Pdf';
  return confirmSessionUpload(sessionId, key, file.name, fileType);
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

/**
 * Check if a title is available (not already used by another learning)
 */
export async function checkSessionTitle(
  sessionId: string,
  title: string
): Promise<{ available: boolean; message?: string }> {
  const response = await apiClient.get<{ available: boolean; message?: string }>(
    `/toolbox-talks/create/session/${sessionId}/check-title`,
    { params: { title } }
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
  const response = await apiClient.get<{
    success: boolean;
    data: { items: ValidationRunSummary[] };
  }>(`/toolbox-talks/${talkId}/validation/runs`);
  return response.data.data.items;
}

/**
 * Get validation run details (course-scoped)
 */
export async function getCourseValidationRun(
  courseId: string,
  runId: string
): Promise<ValidationRunDetail> {
  const response = await apiClient.get<ValidationRunDetail>(
    `/toolbox-talks/courses/${courseId}/validation/runs/${runId}`
  );
  return response.data;
}

/**
 * List validation runs for a course
 */
export async function getCourseValidationRuns(
  courseId: string
): Promise<ValidationRunSummary[]> {
  const response = await apiClient.get<{
    success: boolean;
    data: { items: ValidationRunSummary[] };
  }>(`/toolbox-talks/courses/${courseId}/validation/runs`);
  return response.data.data.items;
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
// Regulatory Score API
// ============================================

/**
 * Trigger a regulatory score assessment on a validation run
 */
export async function triggerRegulatoryScore(
  talkId: string,
  runId: string,
  scoreType: ValidationScoreType
): Promise<RegulatoryScoreResultDto> {
  const response = await apiClient.post<RegulatoryScoreResultDto>(
    `/toolbox-talks/validation-runs/${runId}/regulatory-score`,
    { scoreType }
  );
  return response.data;
}

/**
 * Get the full regulatory score history for a validation run
 */
export async function getRegulatoryScoreHistory(
  talkId: string,
  runId: string
): Promise<RegulatoryScoreHistoryDto> {
  const response = await apiClient.get<RegulatoryScoreHistoryDto>(
    `/toolbox-talks/validation-runs/${runId}/regulatory-score/history`
  );
  return response.data;
}

// ============================================
// Session-Context Validation API
// (Used during creation wizard — delegates to talk-context endpoints
//  using the session's outputTalkId as talkId)
// ============================================

/**
 * Get validation run details via session context.
 * Requires the session's outputTalkId (talkId) which is set after content generation.
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
