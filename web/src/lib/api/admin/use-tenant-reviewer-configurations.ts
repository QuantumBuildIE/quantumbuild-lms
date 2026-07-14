import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getTenantReviewerConfigurations,
  createTenantReviewerConfiguration,
  updateTenantReviewerConfiguration,
  deleteTenantReviewerConfiguration,
} from "./tenant-reviewer-configurations";
import type {
  CreateTenantReviewerConfigurationRequest,
  UpdateTenantReviewerConfigurationRequest,
} from "@/types/admin";

export const tenantReviewerConfigurationKeys = {
  all: () => ["tenant-reviewer-configurations"],
};

export function useTenantReviewerConfigurations() {
  return useQuery({
    queryKey: tenantReviewerConfigurationKeys.all(),
    queryFn: () => getTenantReviewerConfigurations(),
  });
}

export function useCreateTenantReviewerConfiguration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateTenantReviewerConfigurationRequest) =>
      createTenantReviewerConfiguration(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantReviewerConfigurationKeys.all() });
    },
  });
}

export function useUpdateTenantReviewerConfiguration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateTenantReviewerConfigurationRequest }) =>
      updateTenantReviewerConfiguration(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantReviewerConfigurationKeys.all() });
    },
  });
}

export function useDeleteTenantReviewerConfiguration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteTenantReviewerConfiguration(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tenantReviewerConfigurationKeys.all() });
    },
  });
}
