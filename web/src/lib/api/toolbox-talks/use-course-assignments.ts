import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  assignCourse,
  getCourseAssignments,
  getCourseAssignment,
  deleteCourseAssignment,
  getMyCourseAssignment,
  getMyCourseAssignments,
  getCourseAssignmentPreview,
} from './course-assignments';
import type { AssignCourseDto } from './course-assignments';
import { TOOLBOX_TALKS_KEY } from './use-toolbox-talks';

// ============================================
// Query Keys
// ============================================

export const COURSE_ASSIGNMENTS_KEY = [...TOOLBOX_TALKS_KEY, 'course-assignments'];

// ============================================
// Query Hooks
// ============================================

export function useCourseAssignments(courseId: string) {
  return useQuery({
    queryKey: [...COURSE_ASSIGNMENTS_KEY, 'by-course', courseId],
    queryFn: () => getCourseAssignments(courseId),
    enabled: !!courseId,
  });
}

export function useCourseAssignment(id: string) {
  return useQuery({
    queryKey: [...COURSE_ASSIGNMENTS_KEY, 'detail', id],
    queryFn: () => getCourseAssignment(id),
    enabled: !!id,
  });
}

export function useMyCourseAssignment(id: string) {
  return useQuery({
    queryKey: [...COURSE_ASSIGNMENTS_KEY, 'my', 'detail', id],
    queryFn: () => getMyCourseAssignment(id),
    enabled: !!id,
  });
}

export function useMyCourseAssignments() {
  return useQuery({
    queryKey: [...COURSE_ASSIGNMENTS_KEY, 'my'],
    queryFn: getMyCourseAssignments,
  });
}

// ============================================
// Mutation Hooks
// ============================================

export function useCourseAssignmentPreview() {
  return useMutation({
    mutationFn: ({ courseId, employeeIds }: { courseId: string; employeeIds: string[] }) =>
      getCourseAssignmentPreview(courseId, employeeIds),
  });
}

export function useAssignCourse() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: AssignCourseDto) => assignCourse(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: COURSE_ASSIGNMENTS_KEY });
    },
  });
}

export function useDeleteCourseAssignment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteCourseAssignment(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: COURSE_ASSIGNMENTS_KEY });
    },
  });
}
