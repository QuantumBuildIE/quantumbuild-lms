import { apiClient } from "@/lib/api/client";
import type {
  MappingSummaryDto,
  PendingMappingDto,
  RejectMappingRequest,
  UnconfirmedCountDto,
  ComplianceChecklistDto,
  AddManualMappingRequest,
  ContentOptionDto,
} from "@/types/requirement-mappings";

export async function getPendingMappings(): Promise<MappingSummaryDto> {
  const response = await apiClient.get<MappingSummaryDto>(
    "/toolbox-talks/requirement-mappings/pending"
  );
  return response.data;
}

export async function confirmMapping(
  mappingId: string
): Promise<PendingMappingDto> {
  const response = await apiClient.put<PendingMappingDto>(
    `/toolbox-talks/requirement-mappings/${mappingId}/confirm`
  );
  return response.data;
}

export async function rejectMapping(
  mappingId: string,
  notes: string | null
): Promise<void> {
  await apiClient.put(
    `/toolbox-talks/requirement-mappings/${mappingId}/reject`,
    { mappingId, notes } satisfies RejectMappingRequest
  );
}

export async function confirmAllMappings(): Promise<{ confirmed: number }> {
  const response = await apiClient.post<{ confirmed: number }>(
    "/toolbox-talks/requirement-mappings/confirm-all"
  );
  return response.data;
}

export async function getUnconfirmedCount(
  toolboxTalkId?: string,
  courseId?: string
): Promise<number> {
  const params = new URLSearchParams();
  if (toolboxTalkId) params.append("toolboxTalkId", toolboxTalkId);
  if (courseId) params.append("courseId", courseId);
  const response = await apiClient.get<UnconfirmedCountDto>(
    `/toolbox-talks/requirement-mappings/unconfirmed-count?${params.toString()}`
  );
  return response.data.count;
}

export async function getComplianceChecklist(
  sectorKey: string
): Promise<ComplianceChecklistDto> {
  const response = await apiClient.get<ComplianceChecklistDto>(
    `/toolbox-talks/requirement-mappings/compliance/${encodeURIComponent(sectorKey)}`
  );
  return response.data;
}

export async function addManualMapping(
  request: AddManualMappingRequest
): Promise<PendingMappingDto> {
  const response = await apiClient.post<PendingMappingDto>(
    "/toolbox-talks/requirement-mappings/manual",
    request
  );
  return response.data;
}

export async function getContentOptions(): Promise<ContentOptionDto[]> {
  const response = await apiClient.get<ContentOptionDto[]>(
    "/toolbox-talks/requirement-mappings/content-options"
  );
  return response.data;
}
