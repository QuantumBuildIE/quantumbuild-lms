import { apiClient } from "@/lib/api/client";

export interface TenantModuleDto {
  moduleName: string;
  assignedAt: string;
  assignedBy: string;
}

export interface AssignModuleRequest {
  moduleName: string;
}

export async function getTenantModules(tenantId: string): Promise<TenantModuleDto[]> {
  const response = await apiClient.get<TenantModuleDto[]>(
    `/tenants/${tenantId}/modules`
  );
  return response.data;
}

export async function assignTenantModule(
  tenantId: string,
  data: AssignModuleRequest
): Promise<TenantModuleDto> {
  const response = await apiClient.post<TenantModuleDto>(
    `/tenants/${tenantId}/modules`,
    data
  );
  return response.data;
}

export async function removeTenantModule(
  tenantId: string,
  moduleName: string
): Promise<void> {
  await apiClient.delete(`/tenants/${tenantId}/modules/${moduleName}`);
}
