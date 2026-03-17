import { apiClient } from "@/lib/api/client";
import type { SectorDto, TenantSectorDto, AssignTenantSectorRequest } from "@/types/admin";

export async function getAvailableSectors(): Promise<SectorDto[]> {
  const response = await apiClient.get<SectorDto[]>(
    "/toolbox-talks/sectors"
  );
  return response.data;
}

export async function getTenantSectors(tenantId: string): Promise<TenantSectorDto[]> {
  const response = await apiClient.get<TenantSectorDto[]>(
    `/tenants/${tenantId}/sectors`
  );
  return response.data;
}

export async function assignTenantSector(
  tenantId: string,
  data: AssignTenantSectorRequest
): Promise<TenantSectorDto> {
  const response = await apiClient.post<TenantSectorDto>(
    `/tenants/${tenantId}/sectors`,
    data
  );
  return response.data;
}

export async function removeTenantSector(
  tenantId: string,
  sectorId: string
): Promise<void> {
  await apiClient.delete(`/tenants/${tenantId}/sectors/${sectorId}`);
}

export async function setDefaultTenantSector(
  tenantId: string,
  sectorId: string
): Promise<void> {
  await apiClient.put(`/tenants/${tenantId}/sectors/${sectorId}/set-default`);
}
