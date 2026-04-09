'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createSession,
  getSession,
  uploadFileDirectToR2,
  updateSessionSource,
  parseSessionContent,
  updateSessionSections,
  startTranslateValidate,
  publishSession,
  abandonSession,
  getValidationRun,
  getValidationRuns,
  getCourseValidationRun,
  getCourseValidationRuns,
  deleteValidationRun,
  downloadValidationReport,
  generateValidationReport,
  acceptSection,
  editSection,
  retrySection,
  getSessionValidationRun,
  acceptSessionSection,
  editSessionSection,
  retrySessionSection,
  generateSessionQuiz,
  getSessionQuizData,
  updateSessionQuestions,
  updateSessionQuizSettings,
  getSessionSettings,
  updateSessionSettings,
  uploadSessionCoverImage,
  triggerRegulatoryScore,
  getRegulatoryScoreHistory,
} from './content-creation';
import type {
  CreateSessionRequest,
  UpdateSectionsRequest,
  StartTranslateValidateRequest,
  PublishRequest,
  QuizQuestion,
  QuizSettings,
  ContentCreationSettings,
  ValidationScoreType,
} from '@/types/content-creation';

// ============================================
// Query Keys
// ============================================

export const contentCreationKeys = {
  all: ['content-creation'] as const,
  session: (id: string) => [...contentCreationKeys.all, 'session', id] as const,
  validationRuns: (talkId: string) =>
    [...contentCreationKeys.all, 'validation-runs', talkId] as const,
  validationRun: (talkId: string, runId: string) =>
    [...contentCreationKeys.all, 'validation', talkId, runId] as const,
  quizData: (sessionId: string) =>
    [...contentCreationKeys.all, 'quiz', sessionId] as const,
  settingsData: (sessionId: string) =>
    [...contentCreationKeys.all, 'settings', sessionId] as const,
  courseValidationRuns: (courseId: string) =>
    [...contentCreationKeys.all, 'course-validation-runs', courseId] as const,
  courseValidationRun: (courseId: string, runId: string) =>
    [...contentCreationKeys.all, 'course-validation', courseId, runId] as const,
  regulatoryScoreHistory: (talkId: string, runId: string) =>
    [...contentCreationKeys.all, 'validation', talkId, runId, 'regulatory-score-history'] as const,
};

// ============================================
// Session Hooks
// ============================================

/**
 * Fetch and cache session state
 */
export function useCreationSession(sessionId: string | null) {
  return useQuery({
    queryKey: contentCreationKeys.session(sessionId ?? ''),
    queryFn: () => getSession(sessionId!),
    enabled: !!sessionId,
    staleTime: 30 * 1000, // 30 seconds
  });
}

/**
 * Create a new content creation session
 */
export function useCreateSession() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateSessionRequest) => createSession(request),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
    },
  });
}

/**
 * Upload file to session
 */
export function useUploadSessionFile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      file,
      onProgress,
    }: {
      sessionId: string;
      file: File;
      onProgress?: (percent: number) => void;
    }) => uploadFileDirectToR2(sessionId, file, onProgress),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
    },
  });
}

/**
 * Update source content and reset session to Draft for re-parsing
 */
export function useUpdateSource() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      sourceText,
    }: {
      sessionId: string;
      sourceText?: string;
    }) => updateSessionSource(sessionId, sourceText),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
    },
  });
}

/**
 * Parse content from session source
 */
export function useParseContent() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (sessionId: string) => parseSessionContent(sessionId),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
    },
  });
}

/**
 * Update sections and output type
 */
export function useUpdateSections() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      request,
    }: {
      sessionId: string;
      request: UpdateSectionsRequest;
    }) => updateSessionSections(sessionId, request),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
    },
  });
}

/**
 * Start translation & validation
 */
export function useStartValidation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      request,
    }: {
      sessionId: string;
      request: StartTranslateValidateRequest;
    }) => startTranslateValidate(sessionId, request),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
    },
  });
}

/**
 * Accept/Reject/Edit section decisions
 */
export function useSectionDecision() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      talkId,
      runId,
      sectionIndex,
      action,
      editedTranslation,
    }: {
      talkId: string;
      runId: string;
      sectionIndex: number;
      action: 'accept' | 'edit' | 'retry';
      editedTranslation?: string;
    }) => {
      switch (action) {
        case 'accept':
          return acceptSection(talkId, runId, sectionIndex);
        case 'edit':
          return editSection(talkId, runId, sectionIndex, editedTranslation!);
        case 'retry':
          return retrySection(talkId, runId, sectionIndex);
      }
    },
    onSuccess: (_, { talkId, runId }) => {
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.validationRun(talkId, runId),
      });
    },
  });
}

/**
 * Publish session
 */
export function usePublish() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      request,
    }: {
      sessionId: string;
      request: PublishRequest;
    }) => publishSession(sessionId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['toolbox-talks'] });
      queryClient.invalidateQueries({ queryKey: ['courses'] });
    },
  });
}

/**
 * Abandon session
 */
export function useAbandonSession() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (sessionId: string) => abandonSession(sessionId),
    onSuccess: (_, sessionId) => {
      queryClient.removeQueries({
        queryKey: contentCreationKeys.session(sessionId),
      });
    },
  });
}

/**
 * Fetch all validation runs for a talk
 */
export function useValidationRuns(talkId: string | null) {
  return useQuery({
    queryKey: contentCreationKeys.validationRuns(talkId ?? ''),
    queryFn: () => getValidationRuns(talkId!),
    enabled: !!talkId,
    staleTime: 30 * 1000,
  });
}

/**
 * Fetch all validation runs for a course
 */
export function useCourseValidationRuns(courseId: string | null) {
  return useQuery({
    queryKey: contentCreationKeys.courseValidationRuns(courseId ?? ''),
    queryFn: () => getCourseValidationRuns(courseId!),
    enabled: !!courseId,
    staleTime: 30 * 1000,
  });
}

/**
 * Fetch validation run details (course-scoped)
 */
export function useCourseValidationRun(
  courseId: string | null,
  runId: string | null
) {
  return useQuery({
    queryKey: contentCreationKeys.courseValidationRun(courseId ?? '', runId ?? ''),
    queryFn: () => getCourseValidationRun(courseId!, runId!),
    enabled: !!courseId && !!runId,
    staleTime: 10 * 1000,
  });
}

/**
 * Fetch validation run details
 */
export function useValidationRun(
  talkId: string | null,
  runId: string | null
) {
  return useQuery({
    queryKey: contentCreationKeys.validationRun(talkId ?? '', runId ?? ''),
    queryFn: () => getValidationRun(talkId!, runId!),
    enabled: !!talkId && !!runId,
    staleTime: 10 * 1000,
  });
}

/**
 * Delete a validation run
 */
export function useDeleteValidationRun() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ talkId, runId }: { talkId: string; runId: string }) =>
      deleteValidationRun(talkId, runId),
    onSuccess: (_, { talkId }) => {
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.validationRuns(talkId),
      });
    },
  });
}

/**
 * Download a validation report PDF
 */
export function useDownloadValidationReport() {
  return useMutation({
    mutationFn: ({ talkId, runId }: { talkId: string; runId: string }) =>
      downloadValidationReport(talkId, runId),
    onSuccess: (blob, { runId }) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `ValidationReport-${runId.substring(0, 8)}.pdf`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    },
  });
}

/**
 * Generate a validation report for a completed run
 */
export function useGenerateValidationReport() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ talkId, runId }: { talkId: string; runId: string }) =>
      generateValidationReport(talkId, runId),
    onSuccess: (_, { talkId }) => {
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.validationRuns(talkId),
      });
    },
  });
}

/**
 * Fetch validation run details via session context (creation wizard).
 * Uses the session's outputTalkId (talkId) to call the talk-context endpoint.
 */
export function useSessionValidationRun(
  talkId: string | null,
  runId: string | null
) {
  return useQuery({
    queryKey: contentCreationKeys.validationRun(talkId ?? '', runId ?? ''),
    queryFn: () => getSessionValidationRun(talkId!, runId!),
    enabled: !!talkId && !!runId,
    staleTime: 10 * 1000,
  });
}

/**
 * Section decisions via session context (creation wizard).
 * Uses the session's outputTalkId (talkId) to call the talk-context endpoints.
 */
export function useSessionSectionDecision() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      talkId,
      runId,
      sectionIndex,
      action,
      editedTranslation,
    }: {
      talkId: string;
      runId: string;
      sectionIndex: number;
      action: 'accept' | 'edit' | 'retry';
      editedTranslation?: string;
    }) => {
      switch (action) {
        case 'accept':
          return acceptSessionSection(talkId, runId, sectionIndex);
        case 'edit':
          return editSessionSection(
            talkId,
            runId,
            sectionIndex,
            editedTranslation!
          );
        case 'retry':
          return retrySessionSection(talkId, runId, sectionIndex);
      }
    },
    onSuccess: (_, { talkId, runId }) => {
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.validationRun(talkId, runId),
      });
    },
  });
}

// ============================================
// Quiz Hooks
// ============================================

/**
 * Fetch quiz data (questions + settings) for a session
 */
export function useSessionQuizData(sessionId: string | null) {
  return useQuery({
    queryKey: contentCreationKeys.quizData(sessionId ?? ''),
    queryFn: () => getSessionQuizData(sessionId!),
    enabled: !!sessionId,
    staleTime: 30 * 1000,
  });
}

/**
 * Generate quiz questions from session content
 */
export function useGenerateQuiz() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (sessionId: string) => generateSessionQuiz(sessionId),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.quizData(session.id),
      });
    },
  });
}

/**
 * Update quiz questions for a session
 */
export function useUpdateSessionQuestions() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      questions,
    }: {
      sessionId: string;
      questions: QuizQuestion[];
    }) => updateSessionQuestions(sessionId, questions),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.quizData(session.id),
      });
    },
  });
}

/**
 * Update quiz settings for a session
 */
export function useUpdateSessionQuizSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      settings,
    }: {
      sessionId: string;
      settings: QuizSettings;
    }) => updateSessionQuizSettings(sessionId, settings),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.quizData(session.id),
      });
    },
  });
}

// ============================================
// Settings Hooks
// ============================================

/**
 * Fetch session settings (title, category, behaviour)
 */
export function useSessionSettings(sessionId: string | null) {
  return useQuery({
    queryKey: contentCreationKeys.settingsData(sessionId ?? ''),
    queryFn: () => getSessionSettings(sessionId!),
    enabled: !!sessionId,
    staleTime: 30 * 1000,
  });
}

/**
 * Update session settings
 */
export function useUpdateSessionSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      settings,
    }: {
      sessionId: string;
      settings: ContentCreationSettings;
    }) => updateSessionSettings(sessionId, settings),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.settingsData(session.id),
      });
    },
  });
}

/**
 * Upload cover image for session
 */
export function useUploadCoverImage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      sessionId,
      file,
    }: {
      sessionId: string;
      file: File;
    }) => uploadSessionCoverImage(sessionId, file),
    onSuccess: (session) => {
      queryClient.setQueryData(
        contentCreationKeys.session(session.id),
        session
      );
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.settingsData(session.id),
      });
    },
  });
}

// ============================================
// Regulatory Score Hooks
// ============================================

/**
 * Fetch regulatory score history for a validation run
 */
export function useRegulatoryScoreHistory(
  talkId: string | null,
  runId: string | null
) {
  return useQuery({
    queryKey: contentCreationKeys.regulatoryScoreHistory(talkId ?? '', runId ?? ''),
    queryFn: () => getRegulatoryScoreHistory(talkId!, runId!),
    enabled: !!talkId && !!runId,
    staleTime: 0, // Always fresh — scores are added by mutations
  });
}

/**
 * Trigger a regulatory score assessment
 */
export function useTriggerRegulatoryScore() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      talkId,
      runId,
      scoreType,
    }: {
      talkId: string;
      runId: string;
      scoreType: ValidationScoreType;
    }) => triggerRegulatoryScore(talkId, runId, scoreType),
    onSuccess: (_, { talkId, runId }) => {
      queryClient.invalidateQueries({
        queryKey: contentCreationKeys.regulatoryScoreHistory(talkId, runId),
      });
    },
  });
}
