import { apiClient } from "@/lib/api/client";
import type { AvailableStandardDto, TenantStandardSubscriptionDto } from "@/types/regulatory";

export async function getAvailableStandards(
  tenantId: string,
  includeCrossSector: boolean
): Promise<AvailableStandardDto[]> {
  const response = await apiClient.get<AvailableStandardDto[]>(
    `/tenants/${tenantId}/standards/available`,
    { params: includeCrossSector ? { includeCrossSector: true } : undefined }
  );
  return response.data;
}

export async function getSubscribedStandards(tenantId: string): Promise<TenantStandardSubscriptionDto[]> {
  const response = await apiClient.get<TenantStandardSubscriptionDto[]>(
    `/tenants/${tenantId}/standards`
  );
  return response.data;
}

export async function subscribeToStandard(
  tenantId: string,
  regulatoryBodyId: string
): Promise<TenantStandardSubscriptionDto> {
  const response = await apiClient.post<TenantStandardSubscriptionDto>(
    `/tenants/${tenantId}/standards/${regulatoryBodyId}`
  );
  return response.data;
}

export async function unsubscribeFromStandard(
  tenantId: string,
  regulatoryBodyId: string
): Promise<void> {
  await apiClient.delete(`/tenants/${tenantId}/standards/${regulatoryBodyId}`);
}
