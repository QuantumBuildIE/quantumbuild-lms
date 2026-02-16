import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";
import type { Site } from "@/types/admin";

export interface CreateSiteDto {
  siteCode: string;
  siteName: string;
  address?: string;
  city?: string;
  postalCode?: string;
  siteManagerId?: string;
  companyId?: string;
  phone?: string;
  email?: string;
  isActive?: boolean;
  notes?: string;
}

export interface UpdateSiteDto {
  siteCode: string;
  siteName: string;
  address?: string;
  city?: string;
  postalCode?: string;
  siteManagerId?: string | null;
  companyId?: string | null;
  phone?: string;
  email?: string;
  isActive?: boolean;
  notes?: string;
}

export interface GetSitesParams {
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

export async function getSites(
  params?: GetSitesParams
): Promise<PaginatedResponse<Site>> {
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
  const url = queryString ? `/sites?${queryString}` : "/sites";

  const response = await apiClient.get<ApiResponse<PaginatedResponse<Site>>>(url);

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

export async function getAllSites(): Promise<Site[]> {
  const response = await apiClient.get<ApiResponse<Site[]>>("/sites/all");
  return response.data.data ?? [];
}

export async function getSite(id: string): Promise<Site> {
  const response = await apiClient.get<ApiResponse<Site>>(`/sites/${id}`);
  return response.data.data;
}

export async function createSite(data: CreateSiteDto): Promise<Site> {
  const response = await apiClient.post<ApiResponse<Site>>("/sites", data);
  return response.data.data;
}

export async function updateSite(
  id: string,
  data: UpdateSiteDto
): Promise<Site> {
  const response = await apiClient.put<ApiResponse<Site>>(
    `/sites/${id}`,
    data
  );
  return response.data.data;
}

export async function deleteSite(id: string): Promise<void> {
  await apiClient.delete(`/sites/${id}`);
}
