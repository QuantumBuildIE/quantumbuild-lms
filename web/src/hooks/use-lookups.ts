import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api/client';

export interface LookupValue {
  id: string;
  categoryId: string;
  code: string;
  name: string;
  metadata: string | null;
  sortOrder: number;
  isActive: boolean;
  isGlobal: boolean;
  lookupValueId: string | null;
}

export interface LookupCategory {
  id: string;
  name: string;
  module: string;
  allowCustom: boolean;
  isActive: boolean;
}

interface LookupValuesResponse {
  success: boolean;
  data: LookupValue[];
}

interface LookupCategoriesResponse {
  success: boolean;
  data: LookupCategory[];
}

interface LookupValueResponse {
  success: boolean;
  data: LookupValue;
}

export interface CreateLookupValueInput {
  code: string;
  name: string;
  metadata?: string | null;
  sortOrder: number;
}

export interface UpdateLookupValueInput {
  code: string;
  name: string;
  metadata?: string | null;
  sortOrder: number;
  isEnabled: boolean;
}

export function useLookupValues(categoryName: string, options?: { includeDisabled?: boolean }) {
  const includeDisabled = options?.includeDisabled ?? false;
  return useQuery({
    queryKey: ['lookups', categoryName, { includeDisabled }],
    queryFn: async () => {
      const params = includeDisabled ? { includeDisabled: true } : {};
      const response = await apiClient.get<LookupValuesResponse>(
        `/lookups/${encodeURIComponent(categoryName)}/values`,
        { params }
      );
      return response.data.data;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes — lookups rarely change
  });
}

export function useLookupCategories() {
  return useQuery({
    queryKey: ['lookups', 'categories'],
    queryFn: async () => {
      const response = await apiClient.get<LookupCategoriesResponse>(
        '/lookups/categories'
      );
      return response.data.data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateLookupValue(categoryName: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (input: CreateLookupValueInput) => {
      const response = await apiClient.post<LookupValueResponse>(
        `/lookups/${encodeURIComponent(categoryName)}/values`,
        input
      );
      return response.data.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['lookups', categoryName] });
    },
  });
}

export function useUpdateLookupValue(categoryName: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, ...input }: UpdateLookupValueInput & { id: string }) => {
      const response = await apiClient.put<LookupValueResponse>(
        `/lookups/values/${id}`,
        input
      );
      return response.data.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['lookups', categoryName] });
    },
  });
}

export function useDeleteLookupValue(categoryName: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/lookups/values/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['lookups', categoryName] });
    },
  });
}

export function useToggleLookupValue(categoryName: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ lookupValueId, isEnabled }: { lookupValueId: string; isEnabled: boolean }) => {
      const response = await apiClient.put<LookupValueResponse>(
        `/lookups/${encodeURIComponent(categoryName)}/values/${lookupValueId}/toggle`,
        { isEnabled }
      );
      return response.data.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['lookups', categoryName] });
    },
  });
}
