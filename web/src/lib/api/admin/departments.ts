import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";
import type { Department } from "@/types/admin";

export interface CreateDepartmentDto {
  name: string;
  code?: string;
  isActive?: boolean;
}

export interface UpdateDepartmentDto {
  name: string;
  code?: string;
  isActive?: boolean;
}

export interface GetDepartmentsParams {
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

export async function getDepartments(
  params?: GetDepartmentsParams
): Promise<PaginatedResponse<Department>> {
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
  const url = queryString ? `/departments?${queryString}` : "/departments";

  const response = await apiClient.get<ApiResponse<PaginatedResponse<Department>>>(url);

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

export async function getAllDepartments(): Promise<Department[]> {
  const response = await apiClient.get<ApiResponse<Department[]>>("/departments/all");
  return response.data.data ?? [];
}

export async function getDepartment(id: string): Promise<Department> {
  const response = await apiClient.get<ApiResponse<Department>>(`/departments/${id}`);
  return response.data.data;
}

export async function createDepartment(data: CreateDepartmentDto): Promise<Department> {
  const response = await apiClient.post<ApiResponse<Department>>("/departments", data);
  return response.data.data;
}

export async function updateDepartment(
  id: string,
  data: UpdateDepartmentDto
): Promise<Department> {
  const response = await apiClient.put<ApiResponse<Department>>(
    `/departments/${id}`,
    data
  );
  return response.data.data;
}
