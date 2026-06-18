import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getToolboxTalks,
  getToolboxTalk,
  createToolboxTalk,
  updateToolboxTalk,
  deleteToolboxTalk,
  getToolboxTalkDashboard,
  getToolboxTalkSettings,
  updateToolboxTalkSettings,
  updateToolboxTalkNotificationSettings,
  generateContentTranslations,
  getContentTranslations,
  getToolboxTalkPreview,
  getToolboxTalkPreviewSlides,
  getAdminSlideshowHtml,
  regenerateCertificate,
  getWorkflowStates,
  getWorkflowHistory,
  acceptTranslation,
  validateTranslation,
  initiateExternalReview,
  cancelExternalReview,
  startTalkTranslation,
  addTargetLanguage,
} from './toolbox-talks';
import type {
  GenerateTranslationsRequest,
} from './toolbox-talks';
import type {
  CreateToolboxTalkRequest,
  UpdateToolboxTalkRequest,
  UpdateToolboxTalkSettingsRequest,
  UpdateToolboxTalkNotificationSettingsRequest,
  GetToolboxTalksParams,
} from '@/types/toolbox-talks';

// ============================================
// Query Keys
// ============================================

export const TOOLBOX_TALKS_KEY = ['toolbox-talks'];
export const TOOLBOX_TALKS_DASHBOARD_KEY = [...TOOLBOX_TALKS_KEY, 'dashboard'];
export const TOOLBOX_TALKS_SETTINGS_KEY = [...TOOLBOX_TALKS_KEY, 'settings'];

// ============================================
// Toolbox Talks Query Hooks
// ============================================

export function useToolboxTalks(params?: GetToolboxTalksParams) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, 'list', params],
    queryFn: () => getToolboxTalks(params),
  });
}

export function useToolboxTalk(id: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, id],
    queryFn: () => getToolboxTalk(id),
    enabled: !!id,
    refetchOnWindowFocus: true,
  });
}

export function useToolboxTalkDashboard() {
  return useQuery({
    queryKey: TOOLBOX_TALKS_DASHBOARD_KEY,
    queryFn: getToolboxTalkDashboard,
  });
}

export function useToolboxTalkSettings() {
  return useQuery({
    queryKey: TOOLBOX_TALKS_SETTINGS_KEY,
    queryFn: getToolboxTalkSettings,
  });
}

// ============================================
// Toolbox Talks Mutation Hooks
// ============================================

export function useCreateToolboxTalk() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateToolboxTalkRequest) => createToolboxTalk(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_KEY });
    },
  });
}

export function useUpdateToolboxTalk() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateToolboxTalkRequest }) =>
      updateToolboxTalk(id, data),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_KEY });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, id] });
    },
  });
}

export function useDeleteToolboxTalk() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteToolboxTalk(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_KEY });
    },
  });
}

export function useUpdateToolboxTalkSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateToolboxTalkSettingsRequest) => updateToolboxTalkSettings(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_SETTINGS_KEY });
    },
  });
}

export function useUpdateToolboxTalkNotificationSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateToolboxTalkNotificationSettingsRequest) =>
      updateToolboxTalkNotificationSettings(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_SETTINGS_KEY });
    },
  });
}

// ============================================
// Content Translation Hooks
// ============================================

export function useContentTranslations(toolboxTalkId: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'translations'],
    queryFn: () => getContentTranslations(toolboxTalkId),
    enabled: !!toolboxTalkId,
  });
}

export function useGenerateContentTranslations() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ toolboxTalkId, request }: { toolboxTalkId: string; request: GenerateTranslationsRequest }) =>
      generateContentTranslations(toolboxTalkId, request),
    onSuccess: (_, { toolboxTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'translations'] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'] });
    },
  });
}

// ============================================
// Admin Preview Hooks
// ============================================

export function useToolboxTalkPreview(id: string, lang?: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, id, 'preview', lang],
    queryFn: () => getToolboxTalkPreview(id, lang),
    enabled: !!id,
  });
}

export function useToolboxTalkPreviewSlides(id: string, lang?: string, enabled = true) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, id, 'preview-slides', lang],
    queryFn: () => getToolboxTalkPreviewSlides(id, lang),
    enabled: !!id && enabled,
  });
}

export function useAdminSlideshowHtml(id: string, lang?: string, enabled = true) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, id, 'slideshow-html', lang],
    queryFn: () => getAdminSlideshowHtml(id, lang),
    enabled: !!id && enabled,
  });
}

export function useRegenerateCertificate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ talkId, completionId }: { talkId: string; completionId: string }) =>
      regenerateCertificate(talkId, completionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reports'] });
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_KEY });
    },
  });
}

// ============================================
// Translation Workflow Hooks
// ============================================

export function useWorkflowStates(toolboxTalkId: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'],
    queryFn: () => getWorkflowStates(toolboxTalkId),
    enabled: !!toolboxTalkId,
  });
}

export function useWorkflowHistory(toolboxTalkId: string, languageCode: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-history', languageCode],
    queryFn: () => getWorkflowHistory(toolboxTalkId, languageCode),
    enabled: !!toolboxTalkId && !!languageCode,
  });
}

export function useAcceptTranslation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ toolboxTalkId, languageCode }: { toolboxTalkId: string; languageCode: string }) =>
      acceptTranslation(toolboxTalkId, languageCode),
    onSuccess: (_, { toolboxTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId] });
    },
  });
}

export function useValidateTranslation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ toolboxTalkId, languageCode }: { toolboxTalkId: string; languageCode: string }) =>
      validateTranslation(toolboxTalkId, languageCode),
    onSuccess: (_, { toolboxTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId] });
    },
  });
}

export function useInitiateExternalReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ toolboxTalkId, languageCode, reviewerEmail }: { toolboxTalkId: string; languageCode: string; reviewerEmail: string }) =>
      initiateExternalReview(toolboxTalkId, languageCode, reviewerEmail),
    onSuccess: (_, { toolboxTalkId, languageCode }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-history', languageCode] });
    },
  });
}

export function useCancelExternalReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ toolboxTalkId, languageCode }: { toolboxTalkId: string; languageCode: string }) =>
      cancelExternalReview(toolboxTalkId, languageCode),
    onSuccess: (_, { toolboxTalkId, languageCode }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-history', languageCode] });
    },
  });
}

export function useStartTalkTranslation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      talkId,
      languageCode,
      confirmOverwrite,
    }: {
      talkId: string;
      languageCode: string;
      confirmOverwrite?: boolean;
    }) => startTalkTranslation(talkId, languageCode, confirmOverwrite),
    onSuccess: (_, { talkId, languageCode }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, talkId, 'workflow-state'] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, talkId, 'workflow-history', languageCode] });
    },
  });
}

export function useAddTargetLanguage() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ talkId, languageCode }: { talkId: string; languageCode: string }) =>
      addTargetLanguage(talkId, languageCode),
    onSuccess: (_, { talkId }) => {
      // Invalidate the talk object so ToolboxTalkDetail sees the updated targetLanguageCodes
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, talkId] });
      // Invalidate workflow states so TranslateStep renders the new language (Initial state)
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, talkId, 'workflow-state'] });
      // Invalidate the learnings cache key used by TranslateStep's useTalk hook
      queryClient.invalidateQueries({ queryKey: ['learnings', talkId] });
    },
  });
}
