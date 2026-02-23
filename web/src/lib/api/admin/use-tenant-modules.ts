import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getTenantModules,
  assignTenantModule,
  removeTenantModule,
  type AssignModuleRequest,
} from "./tenant-modules";

export const TENANT_MODULES_KEY = ["tenant-modules"];

export function useTenantModules(tenantId: string) {
  return useQuery({
    queryKey: [...TENANT_MODULES_KEY, tenantId],
    queryFn: () => getTenantModules(tenantId),
    enabled: !!tenantId,
  });
}

export function useAssignTenantModule(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: AssignModuleRequest) =>
      assignTenantModule(tenantId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: [...TENANT_MODULES_KEY, tenantId],
      });
    },
  });
}

export function useRemoveTenantModule(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (moduleName: string) =>
      removeTenantModule(tenantId, moduleName),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: [...TENANT_MODULES_KEY, tenantId],
      });
    },
  });
}
