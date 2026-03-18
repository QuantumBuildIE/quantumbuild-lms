import { apiClient } from "@/lib/api/client";
import type {
  RegulatoryDocumentListItem,
  IngestionSessionDto,
  DraftRequirementDto,
  RegulatoryRequirementDto,
  ApproveRequirementRequest,
  UpdateDraftRequirementRequest,
  StartIngestionRequest,
  RejectRequirementRequest,
} from "@/types/regulatory";

export async function getRegulatoryDocuments(): Promise<RegulatoryDocumentListItem[]> {
  const response = await apiClient.get<RegulatoryDocumentListItem[]>(
    "/regulatory/documents"
  );
  return response.data;
}

export async function startIngestion(
  documentId: string,
  data: StartIngestionRequest
): Promise<IngestionSessionDto> {
  const response = await apiClient.post<IngestionSessionDto>(
    `/regulatory/documents/${documentId}/ingest`,
    data
  );
  return response.data;
}

export async function getIngestionStatus(
  documentId: string
): Promise<IngestionSessionDto> {
  const response = await apiClient.get<IngestionSessionDto>(
    `/regulatory/documents/${documentId}/ingestion-status`
  );
  return response.data;
}

export async function getDraftRequirements(
  documentId: string
): Promise<DraftRequirementDto[]> {
  const response = await apiClient.get<DraftRequirementDto[]>(
    `/regulatory/documents/${documentId}/draft-requirements`
  );
  return response.data;
}

export async function approveRequirement(
  requirementId: string,
  data: ApproveRequirementRequest
): Promise<RegulatoryRequirementDto> {
  const response = await apiClient.put<RegulatoryRequirementDto>(
    `/regulatory/requirements/${requirementId}/approve`,
    data
  );
  return response.data;
}

export async function rejectRequirement(
  requirementId: string,
  data: RejectRequirementRequest
): Promise<void> {
  await apiClient.put(
    `/regulatory/requirements/${requirementId}/reject`,
    data
  );
}

export async function updateDraftRequirement(
  requirementId: string,
  data: UpdateDraftRequirementRequest
): Promise<RegulatoryRequirementDto> {
  const response = await apiClient.put<RegulatoryRequirementDto>(
    `/regulatory/requirements/${requirementId}`,
    data
  );
  return response.data;
}

export async function approveAllDrafts(
  documentId: string
): Promise<{ approved: number }> {
  const response = await apiClient.post<{ approved: number }>(
    `/regulatory/documents/${documentId}/approve-all`
  );
  return response.data;
}
