import { apiClient } from "@/lib/api/client";

export type TenantSettings = Record<string, string>;

export interface UpdateTenantSettingsDto {
  settings: { key: string; value: string }[];
}

export async function getTenantSettings(): Promise<TenantSettings> {
  const response = await apiClient.get<TenantSettings>("/tenant-settings");
  return response.data;
}

export async function updateTenantSettings(
  data: UpdateTenantSettingsDto
): Promise<TenantSettings> {
  const response = await apiClient.put<TenantSettings>(
    "/tenant-settings",
    data
  );
  return response.data;
}
