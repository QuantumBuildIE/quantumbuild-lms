import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getRegulatoryDocuments,
  startIngestion,
  getIngestionStatus,
  getDraftRequirements,
  approveRequirement,
  rejectRequirement,
  updateDraftRequirement,
  approveAllDrafts,
} from "./regulatory-ingestion";
import type {
  StartIngestionRequest,
  ApproveRequirementRequest,
  UpdateDraftRequirementRequest,
  RejectRequirementRequest,
} from "@/types/regulatory";

export const regulatoryKeys = {
  documents: () => ["regulatory-documents"] as const,
  ingestionStatus: (documentId: string) =>
    ["regulatory-ingestion-status", documentId] as const,
  draftRequirements: (documentId: string) =>
    ["regulatory-draft-requirements", documentId] as const,
};

export function useRegulatoryDocuments() {
  return useQuery({
    queryKey: regulatoryKeys.documents(),
    queryFn: () => getRegulatoryDocuments(),
    staleTime: 30 * 1000,
  });
}

export function useIngestionStatus(documentId: string, enabled = true) {
  return useQuery({
    queryKey: regulatoryKeys.ingestionStatus(documentId),
    queryFn: () => getIngestionStatus(documentId),
    enabled: !!documentId && enabled,
    refetchInterval: false,
  });
}

export function useIngestionStatusPolling(
  documentId: string,
  isPolling: boolean
) {
  return useQuery({
    queryKey: regulatoryKeys.ingestionStatus(documentId),
    queryFn: () => getIngestionStatus(documentId),
    enabled: !!documentId && isPolling,
    refetchInterval: isPolling ? 3000 : false,
  });
}

export function useDraftRequirements(documentId: string) {
  return useQuery({
    queryKey: regulatoryKeys.draftRequirements(documentId),
    queryFn: () => getDraftRequirements(documentId),
    enabled: !!documentId,
  });
}

export function useStartIngestion(documentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: StartIngestionRequest) =>
      startIngestion(documentId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.ingestionStatus(documentId),
      });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.documents(),
      });
    },
  });
}

export function useApproveRequirement(documentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      requirementId,
      data,
    }: {
      requirementId: string;
      data: ApproveRequirementRequest;
    }) => approveRequirement(requirementId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.draftRequirements(documentId),
      });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.ingestionStatus(documentId),
      });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.documents(),
      });
    },
  });
}

export function useRejectRequirement(documentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      requirementId,
      data,
    }: {
      requirementId: string;
      data: RejectRequirementRequest;
    }) => rejectRequirement(requirementId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.draftRequirements(documentId),
      });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.ingestionStatus(documentId),
      });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.documents(),
      });
    },
  });
}

export function useUpdateDraftRequirement(documentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      requirementId,
      data,
    }: {
      requirementId: string;
      data: UpdateDraftRequirementRequest;
    }) => updateDraftRequirement(requirementId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.draftRequirements(documentId),
      });
    },
  });
}

export function useApproveAllDrafts(documentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => approveAllDrafts(documentId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.draftRequirements(documentId),
      });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.ingestionStatus(documentId),
      });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.documents(),
      });
    },
  });
}
