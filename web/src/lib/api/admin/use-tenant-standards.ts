import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getAvailableStandards,
  getSubscribedStandards,
  subscribeToStandard,
  unsubscribeFromStandard,
} from "./tenant-standards";

export const tenantStandardKeys = {
  availableStandards: (tenantId: string, includeCrossSector: boolean) =>
    ["available-standards", tenantId, includeCrossSector],
  subscribedStandards: (tenantId: string) => ["subscribed-standards", tenantId],
};

export function useAvailableStandards(tenantId: string, includeCrossSector: boolean) {
  return useQuery({
    queryKey: tenantStandardKeys.availableStandards(tenantId, includeCrossSector),
    queryFn: () => getAvailableStandards(tenantId, includeCrossSector),
    enabled: !!tenantId,
  });
}

export function useSubscribedStandards(tenantId: string) {
  return useQuery({
    queryKey: tenantStandardKeys.subscribedStandards(tenantId),
    queryFn: () => getSubscribedStandards(tenantId),
    enabled: !!tenantId,
  });
}

function useInvalidateStandards(tenantId: string) {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ["available-standards", tenantId] });
    queryClient.invalidateQueries({ queryKey: tenantStandardKeys.subscribedStandards(tenantId) });
  };
}

export function useSubscribeToStandard(tenantId: string) {
  const invalidate = useInvalidateStandards(tenantId);

  return useMutation({
    mutationFn: (regulatoryBodyId: string) => subscribeToStandard(tenantId, regulatoryBodyId),
    onSuccess: invalidate,
  });
}

export function useUnsubscribeFromStandard(tenantId: string) {
  const invalidate = useInvalidateStandards(tenantId);

  return useMutation({
    mutationFn: (regulatoryBodyId: string) => unsubscribeFromStandard(tenantId, regulatoryBodyId),
    onSuccess: invalidate,
  });
}
