import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getPendingMappings,
  confirmMapping,
  rejectMapping,
  confirmAllMappings,
  getUnconfirmedCount,
} from "./requirement-mappings";

export const requirementMappingKeys = {
  pending: () => ["requirement-mappings-pending"] as const,
  unconfirmedCount: (toolboxTalkId?: string, courseId?: string) =>
    ["requirement-mappings-unconfirmed", toolboxTalkId, courseId] as const,
};

export function usePendingMappings() {
  return useQuery({
    queryKey: requirementMappingKeys.pending(),
    queryFn: () => getPendingMappings(),
    staleTime: 30 * 1000,
  });
}

export function useConfirmMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (mappingId: string) => confirmMapping(mappingId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: requirementMappingKeys.pending(),
      });
    },
  });
}

export function useRejectMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ mappingId, notes }: { mappingId: string; notes: string | null }) =>
      rejectMapping(mappingId, notes),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: requirementMappingKeys.pending(),
      });
    },
  });
}

export function useConfirmAllMappings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => confirmAllMappings(),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: requirementMappingKeys.pending(),
      });
    },
  });
}

export function useUnconfirmedMappingCount(
  toolboxTalkId?: string,
  courseId?: string
) {
  return useQuery({
    queryKey: requirementMappingKeys.unconfirmedCount(toolboxTalkId, courseId),
    queryFn: () => getUnconfirmedCount(toolboxTalkId, courseId),
    enabled: !!(toolboxTalkId || courseId),
    staleTime: 30 * 1000,
  });
}
