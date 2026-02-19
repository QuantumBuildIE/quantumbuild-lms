import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getTenantSettings,
  updateTenantSettings,
  type UpdateTenantSettingsDto,
} from "./tenant-settings";

export const TENANT_SETTINGS_KEY = ["tenant-settings"];

export function useTenantSettings() {
  return useQuery({
    queryKey: TENANT_SETTINGS_KEY,
    queryFn: () => getTenantSettings(),
  });
}

export function useUpdateTenantSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateTenantSettingsDto) => updateTenantSettings(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TENANT_SETTINGS_KEY });
    },
  });
}

export type { UpdateTenantSettingsDto } from "./tenant-settings";
export type { TenantSettings } from "./tenant-settings";
