import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";

export interface Role {
  id: string;
  name: string;
  description?: string;
  permissionCount: number;
}

export async function getRoles(): Promise<Role[]> {
  const response = await apiClient.get<ApiResponse<Role[]>>("/roles");
  return response.data.data ?? [];
}
