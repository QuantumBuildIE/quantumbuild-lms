import { apiClient } from "@/lib/api/client";

/** Returns DTOs directly (not the Result<T> envelope): read response.data. */
export interface BrandingResponse {
  logoUrl: string | null;
}

export interface CurrentBrandingResponse extends BrandingResponse {
  /** Resolved tenant's name; null when no tenant is in context (SuperUser on All Tenants). */
  tenantName: string | null;
}

/** Branding for the caller's current tenant (JWT tenant, or the SuperUser's X-Tenant-Id tenant). */
export async function getCurrentBranding(): Promise<CurrentBrandingResponse> {
  const response = await apiClient.get<CurrentBrandingResponse>("/branding/current");
  return response.data;
}

/** Logo of the tenant in the route (Tenant.Manage), independent of the active tenant header. */
export async function getTenantBranding(tenantId: string): Promise<BrandingResponse> {
  const response = await apiClient.get<BrandingResponse>(`/tenants/${tenantId}/branding`);
  return response.data;
}

export async function uploadTenantLogo(tenantId: string, file: File): Promise<BrandingResponse> {
  const formData = new FormData();
  formData.append("file", file);
  // Clear the instance-default Content-Type so the browser sets the multipart boundary.
  const response = await apiClient.put<BrandingResponse>(
    `/tenants/${tenantId}/branding/logo`,
    formData,
    { headers: { "Content-Type": undefined } }
  );
  return response.data;
}

export async function deleteTenantLogo(tenantId: string): Promise<void> {
  await apiClient.delete(`/tenants/${tenantId}/branding/logo`);
}

/** Server rejections come back as { error: string } with a 400. */
export function getBrandingErrorMessage(error: unknown, fallback: string): string {
  const message = (error as { response?: { data?: { error?: unknown } } })?.response?.data?.error;
  return typeof message === "string" && message ? message : fallback;
}
