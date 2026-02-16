import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getToolboxTalkCourses,
  getToolboxTalkCourse,
  createToolboxTalkCourse,
  updateToolboxTalkCourse,
  deleteToolboxTalkCourse,
  addCourseItem,
  removeCourseItem,
  updateCourseItems,
} from './courses';
import type {
  CreateToolboxTalkCourseDto,
  UpdateToolboxTalkCourseDto,
  CreateToolboxTalkCourseItemDto,
  UpdateCourseItemsDto,
  GetToolboxTalkCoursesParams,
} from './courses';
import { TOOLBOX_TALKS_KEY } from './use-toolbox-talks';

// ============================================
// Query Keys
// ============================================

export const TOOLBOX_TALK_COURSES_KEY = [...TOOLBOX_TALKS_KEY, 'courses'];

// ============================================
// Query Hooks
// ============================================

export function useToolboxTalkCourses(params?: GetToolboxTalkCoursesParams) {
  return useQuery({
    queryKey: [...TOOLBOX_TALK_COURSES_KEY, 'list', params],
    queryFn: () => getToolboxTalkCourses(params),
  });
}

export function useToolboxTalkCourse(id: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALK_COURSES_KEY, id],
    queryFn: () => getToolboxTalkCourse(id),
    enabled: !!id,
  });
}

// ============================================
// Mutation Hooks
// ============================================

export function useCreateToolboxTalkCourse() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateToolboxTalkCourseDto) => createToolboxTalkCourse(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALK_COURSES_KEY });
    },
  });
}

export function useUpdateToolboxTalkCourse() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateToolboxTalkCourseDto }) =>
      updateToolboxTalkCourse(id, data),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALK_COURSES_KEY });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_COURSES_KEY, id] });
    },
  });
}

export function useDeleteToolboxTalkCourse() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteToolboxTalkCourse(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALK_COURSES_KEY });
    },
  });
}

export function useAddCourseItem() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ courseId, data }: { courseId: string; data: CreateToolboxTalkCourseItemDto }) =>
      addCourseItem(courseId, data),
    onSuccess: (_, { courseId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_COURSES_KEY, courseId] });
    },
  });
}

export function useRemoveCourseItem() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ courseId, talkId }: { courseId: string; talkId: string }) =>
      removeCourseItem(courseId, talkId),
    onSuccess: (_, { courseId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_COURSES_KEY, courseId] });
    },
  });
}

export function useUpdateCourseItems() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ courseId, data }: { courseId: string; data: UpdateCourseItemsDto }) =>
      updateCourseItems(courseId, data),
    onSuccess: (_, { courseId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_COURSES_KEY, courseId] });
    },
  });
}
