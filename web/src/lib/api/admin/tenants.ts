import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";
import type { TenantListItem, TenantDetail, TenantStatus } from "@/types/admin";

export interface CreateTenantDto {
  name: string;
  code?: string;
  companyName?: string;
  contactEmail?: string;
  contactName?: string;
}

export interface UpdateTenantDto {
  name: string;
  code?: string;
  companyName?: string;
  contactEmail?: string;
  contactName?: string;
}

export interface UpdateTenantStatusDto {
  status: TenantStatus;
}

export interface GetTenantsParams {
  pageNumber?: number;
  pageSize?: number;
  sortColumn?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export async function getTenants(
  params?: GetTenantsParams
): Promise<PaginatedResponse<TenantListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.pageNumber) {
    queryParams.append("pageNumber", String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append("pageSize", String(params.pageSize));
  }
  if (params?.sortColumn) {
    queryParams.append("sortColumn", params.sortColumn);
  }
  if (params?.sortDirection) {
    queryParams.append("sortDirection", params.sortDirection);
  }
  if (params?.search) {
    queryParams.append("search", params.search);
  }

  const queryString = queryParams.toString();
  const url = queryString ? `/tenants?${queryString}` : "/tenants";

  const response = await apiClient.get<
    ApiResponse<PaginatedResponse<TenantListItem>>
  >(url);

  const data = response.data.data;
  if (!data) {
    return {
      items: [],
      pageNumber: params?.pageNumber || 1,
      pageSize: params?.pageSize || 20,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    };
  }

  return data;
}

export async function getTenant(id: string): Promise<TenantDetail> {
  const response = await apiClient.get<ApiResponse<TenantDetail>>(
    `/tenants/${id}`
  );
  return response.data.data;
}

export async function createTenant(
  data: CreateTenantDto
): Promise<TenantDetail> {
  const response = await apiClient.post<ApiResponse<TenantDetail>>(
    "/tenants",
    data
  );
  return response.data.data;
}

export async function updateTenant(
  id: string,
  data: UpdateTenantDto
): Promise<TenantDetail> {
  const response = await apiClient.put<ApiResponse<TenantDetail>>(
    `/tenants/${id}`,
    data
  );
  return response.data.data;
}

export async function updateTenantStatus(
  id: string,
  data: UpdateTenantStatusDto
): Promise<TenantDetail> {
  const response = await apiClient.put<ApiResponse<TenantDetail>>(
    `/tenants/${id}/status`,
    data
  );
  return response.data.data;
}
