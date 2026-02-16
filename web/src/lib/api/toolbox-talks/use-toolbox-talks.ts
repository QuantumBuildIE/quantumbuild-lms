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
  generateContentTranslations,
  getContentTranslations,
  getToolboxTalkPreview,
  getToolboxTalkPreviewSlides,
  getAdminSlideshowHtml,
} from './toolbox-talks';
import type {
  GenerateTranslationsRequest,
} from './toolbox-talks';
import type {
  CreateToolboxTalkRequest,
  UpdateToolboxTalkRequest,
  UpdateToolboxTalkSettingsRequest,
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
