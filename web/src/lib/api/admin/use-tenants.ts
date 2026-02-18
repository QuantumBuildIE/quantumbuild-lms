import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getTenants,
  getTenant,
  createTenant,
  updateTenant,
  updateTenantStatus,
  type CreateTenantDto,
  type UpdateTenantDto,
  type UpdateTenantStatusDto,
  type GetTenantsParams,
} from "./tenants";

export const TENANTS_KEY = ["tenants"];

export function useTenants(params?: GetTenantsParams) {
  return useQuery({
    queryKey: [...TENANTS_KEY, params],
    queryFn: () => getTenants(params),
  });
}

export function useTenant(id: string) {
  return useQuery({
    queryKey: [...TENANTS_KEY, id],
    queryFn: () => getTenant(id),
    enabled: !!id,
  });
}

export function useCreateTenant() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateTenantDto) => createTenant(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TENANTS_KEY });
    },
  });
}

export function useUpdateTenant() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateTenantDto }) =>
      updateTenant(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TENANTS_KEY });
    },
  });
}

export function useUpdateTenantStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      id,
      data,
    }: {
      id: string;
      data: UpdateTenantStatusDto;
    }) => updateTenantStatus(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TENANTS_KEY });
    },
  });
}

export type {
  CreateTenantDto,
  UpdateTenantDto,
  UpdateTenantStatusDto,
  GetTenantsParams,
} from "./tenants";
export type { PaginatedResponse } from "./tenants";
