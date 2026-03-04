import { apiClient } from '@/lib/api/client';
import type {
  GlossarySectorListItem,
  GlossarySectorDetail,
  CreateSectorRequest,
  UpdateSectorRequest,
  CreateTermRequest,
  UpdateTermRequest,
  GlossaryTermDto,
} from '@/types/validation';

// ============================================
// Glossary Sectors
// ============================================

export async function getGlossarySectors(): Promise<GlossarySectorListItem[]> {
  const response = await apiClient.get<GlossarySectorListItem[]>(
    '/toolbox-talks/glossary/sectors'
  );
  return response.data;
}

export async function getGlossarySector(key: string): Promise<GlossarySectorDetail> {
  const response = await apiClient.get<GlossarySectorDetail>(
    `/toolbox-talks/glossary/sectors/${encodeURIComponent(key)}`
  );
  return response.data;
}

export async function createGlossarySector(
  request: CreateSectorRequest
): Promise<GlossarySectorDetail> {
  const response = await apiClient.post<GlossarySectorDetail>(
    '/toolbox-talks/glossary/sectors',
    request
  );
  return response.data;
}

export async function updateGlossarySector(
  id: string,
  request: UpdateSectorRequest
): Promise<void> {
  await apiClient.put(`/toolbox-talks/glossary/sectors/${id}`, request);
}

// ============================================
// Glossary Terms
// ============================================

export async function createGlossaryTerm(
  sectorId: string,
  request: CreateTermRequest
): Promise<GlossaryTermDto> {
  const response = await apiClient.post<GlossaryTermDto>(
    `/toolbox-talks/glossary/sectors/${sectorId}/terms`,
    request
  );
  return response.data;
}

export async function updateGlossaryTerm(
  termId: string,
  request: UpdateTermRequest
): Promise<void> {
  await apiClient.put(`/toolbox-talks/glossary/terms/${termId}`, request);
}

export async function deleteGlossaryTerm(termId: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/glossary/terms/${termId}`);
}
