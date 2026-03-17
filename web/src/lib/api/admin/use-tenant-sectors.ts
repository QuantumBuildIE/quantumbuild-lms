import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getAvailableSectors,
  getTenantSectors,
  assignTenantSector,
  removeTenantSector,
  setDefaultTenantSector,
} from "./tenant-sectors";
import type { AssignTenantSectorRequest } from "@/types/admin";

export const tenantSectorKeys = {
  availableSectors: () => ["available-sectors"],
  tenantSectors: (tenantId: string) => ["tenant-sectors", tenantId],
};

export function useAvailableSectors() {
  return useQuery({
    queryKey: tenantSectorKeys.availableSectors(),
    queryFn: () => getAvailableSectors(),
    staleTime: 5 * 60 * 1000,
  });
}

export function useTenantSectors(tenantId: string) {
  return useQuery({
    queryKey: tenantSectorKeys.tenantSectors(tenantId),
    queryFn: () => getTenantSectors(tenantId),
    enabled: !!tenantId,
  });
}

export function useAssignTenantSector(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: AssignTenantSectorRequest) =>
      assignTenantSector(tenantId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: tenantSectorKeys.tenantSectors(tenantId),
      });
    },
  });
}

export function useRemoveTenantSector(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (sectorId: string) =>
      removeTenantSector(tenantId, sectorId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: tenantSectorKeys.tenantSectors(tenantId),
      });
    },
  });
}

export function useSetDefaultTenantSector(tenantId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (sectorId: string) =>
      setDefaultTenantSector(tenantId, sectorId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: tenantSectorKeys.tenantSectors(tenantId),
      });
    },
  });
}
