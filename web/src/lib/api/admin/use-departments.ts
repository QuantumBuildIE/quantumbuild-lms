import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getDepartments,
  getAllDepartments,
  getDepartment,
  createDepartment,
  updateDepartment,
  type CreateDepartmentDto,
  type UpdateDepartmentDto,
  type GetDepartmentsParams,
} from "./departments";

export const DEPARTMENTS_KEY = ["departments"];

export function useDepartments(params?: GetDepartmentsParams) {
  return useQuery({
    queryKey: [...DEPARTMENTS_KEY, params],
    queryFn: () => getDepartments(params),
  });
}

export function useAllDepartments() {
  return useQuery({
    queryKey: [...DEPARTMENTS_KEY, "all"],
    queryFn: getAllDepartments,
  });
}

export function useDepartment(id: string) {
  return useQuery({
    queryKey: [...DEPARTMENTS_KEY, id],
    queryFn: () => getDepartment(id),
    enabled: !!id,
  });
}

export function useCreateDepartment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateDepartmentDto) => createDepartment(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DEPARTMENTS_KEY });
    },
  });
}

export function useUpdateDepartment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateDepartmentDto }) =>
      updateDepartment(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DEPARTMENTS_KEY });
    },
  });
}

export type { CreateDepartmentDto, UpdateDepartmentDto, GetDepartmentsParams } from "./departments";
export type { PaginatedResponse } from "./departments";
