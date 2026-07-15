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
  RegulatoryBrowseBody,
  RegulatoryDocumentUploadResponse,
  RegulatoryBody,
  CreateRegulatoryDocumentRequest,
} from "@/types/regulatory";

export async function getRegulatoryDocuments(): Promise<RegulatoryDocumentListItem[]> {
  const response = await apiClient.get<RegulatoryDocumentListItem[]>(
    "/regulatory/documents"
  );
  return response.data;
}

export async function getRegulatoryBodies(): Promise<RegulatoryBody[]> {
  const response = await apiClient.get<RegulatoryBody[]>("/regulatory/bodies");
  return response.data;
}

export async function createRegulatoryDocument(
  data: CreateRegulatoryDocumentRequest
): Promise<RegulatoryDocumentListItem> {
  const response = await apiClient.post<RegulatoryDocumentListItem>(
    "/regulatory/documents",
    data
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

export async function uploadRegulatoryDocument(
  documentId: string,
  file: File
): Promise<RegulatoryDocumentUploadResponse> {
  const form = new FormData();
  form.append("file", file);
  const response = await apiClient.post<RegulatoryDocumentUploadResponse>(
    `/regulatory/documents/${documentId}/upload`,
    form,
    { headers: { "Content-Type": "multipart/form-data" } }
  );
  return response.data;
}

export async function getBrowsableRequirements(): Promise<RegulatoryBrowseBody[]> {
  const response = await apiClient.get<RegulatoryBrowseBody[]>(
    "/regulatory/browse"
  );
  return response.data;
}
