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
  getBrowsableRequirements,
  uploadRegulatoryDocument,
  getRegulatoryBodies,
  createRegulatoryDocument,
  createRegulatoryBody,
  createRegulatoryProfile,
} from "./regulatory-ingestion";
import type {
  StartIngestionRequest,
  ApproveRequirementRequest,
  UpdateDraftRequirementRequest,
  RejectRequirementRequest,
  CreateRegulatoryDocumentRequest,
  CreateRegulatoryBodyRequest,
  CreateRegulatoryProfileRequest,
  RegulatoryBodyKind,
} from "@/types/regulatory";

export const regulatoryKeys = {
  documents: () => ["regulatory-documents"] as const,
  ingestionStatus: (documentId: string) =>
    ["regulatory-ingestion-status", documentId] as const,
  draftRequirements: (documentId: string) =>
    ["regulatory-draft-requirements", documentId] as const,
  browse: () => ["regulatory-browse"] as const,
  bodies: (kind?: RegulatoryBodyKind) =>
    kind ? (["regulatory-bodies", kind] as const) : (["regulatory-bodies"] as const),
};

export function useRegulatoryDocuments() {
  return useQuery({
    queryKey: regulatoryKeys.documents(),
    queryFn: () => getRegulatoryDocuments(),
    staleTime: 30 * 1000,
  });
}

const TERMINAL_INGESTION_STATUSES = new Set(["Success", "Failed", "Skipped"]);

export function isTerminalIngestionStatus(status: string | undefined): boolean {
  return !!status && TERMINAL_INGESTION_STATUSES.has(status);
}

/**
 * How long to keep polling after a start/retry attempt before the backend has ever
 * confirmed the job reached "Ingesting" for that attempt. Once "Ingesting" is observed,
 * polling continues with no ceiling — a real ingest can legitimately run several minutes
 * and must not be abandoned as stale mid-run. This ceiling only guards the (normally
 * sub-second) window between enqueueing the Hangfire job and it actually starting.
 */
export const INGESTION_BOOTSTRAP_CEILING_MS = 3 * 60 * 1000;

/**
 * Live ingestion status for a document. Always fetches on mount/documentId change.
 * Polls every 3s while the backend confirms a job is actively "Ingesting", or while a
 * just-started/retried attempt (attemptStartedAt, from useStartIngestion's
 * mutation.submittedAt) hasn't yet been confirmed one way or the other. Stops the
 * instant a fetch lands with a terminal status dated after attemptStartedAt — Success,
 * Failed, and Skipped are all terminal.
 */
export function useIngestionStatus(documentId: string, attemptStartedAt: number) {
  return useQuery({
    queryKey: regulatoryKeys.ingestionStatus(documentId),
    queryFn: () => getIngestionStatus(documentId),
    enabled: !!documentId,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status === "Ingesting") return 3000;

      if (attemptStartedAt > 0) {
        const confirmedTerminalForAttempt =
          query.state.dataUpdatedAt > attemptStartedAt &&
          isTerminalIngestionStatus(status);
        if (
          !confirmedTerminalForAttempt &&
          Date.now() - attemptStartedAt < INGESTION_BOOTSTRAP_CEILING_MS
        ) {
          return 3000;
        }
      }

      return false;
    },
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

export function useBrowsableRequirements() {
  return useQuery({
    queryKey: regulatoryKeys.browse(),
    queryFn: () => getBrowsableRequirements(),
    staleTime: 60 * 1000,
  });
}

export function useUploadRegulatoryDocument(documentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => uploadRegulatoryDocument(documentId, file),
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

export function useRegulatoryBodies(kind?: RegulatoryBodyKind) {
  return useQuery({
    queryKey: regulatoryKeys.bodies(kind),
    queryFn: () => getRegulatoryBodies(kind),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateRegulatoryBody() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateRegulatoryBodyRequest) => createRegulatoryBody(data),
    onSuccess: () => {
      // Invalidates every cached bodies query, filtered or not — matches the array-prefix
      // matching TanStack Query does when the passed key is a strict prefix of the cached one.
      queryClient.invalidateQueries({ queryKey: ["regulatory-bodies"] });
    },
  });
}

export function useCreateRegulatoryDocument() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateRegulatoryDocumentRequest) =>
      createRegulatoryDocument(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: regulatoryKeys.documents() });
    },
  });
}

export function useCreateRegulatoryProfile(documentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateRegulatoryProfileRequest) =>
      createRegulatoryProfile(documentId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: regulatoryKeys.documents() });
      queryClient.invalidateQueries({
        queryKey: regulatoryKeys.ingestionStatus(documentId),
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
