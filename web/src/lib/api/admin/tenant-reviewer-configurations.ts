import { apiClient } from "@/lib/api/client";
import type {
  TenantReviewerConfigurationDto,
  CreateTenantReviewerConfigurationRequest,
  UpdateTenantReviewerConfigurationRequest,
} from "@/types/admin";

export async function getTenantReviewerConfigurations(): Promise<TenantReviewerConfigurationDto[]> {
  const response = await apiClient.get<TenantReviewerConfigurationDto[]>(
    "/tenant-reviewer-configurations"
  );
  return response.data;
}

export async function createTenantReviewerConfiguration(
  data: CreateTenantReviewerConfigurationRequest
): Promise<TenantReviewerConfigurationDto> {
  const response = await apiClient.post<TenantReviewerConfigurationDto>(
    "/tenant-reviewer-configurations",
    data
  );
  return response.data;
}

export async function updateTenantReviewerConfiguration(
  id: string,
  data: UpdateTenantReviewerConfigurationRequest
): Promise<TenantReviewerConfigurationDto> {
  const response = await apiClient.put<TenantReviewerConfigurationDto>(
    `/tenant-reviewer-configurations/${id}`,
    data
  );
  return response.data;
}

export async function deleteTenantReviewerConfiguration(id: string): Promise<void> {
  await apiClient.delete(`/tenant-reviewer-configurations/${id}`);
}
