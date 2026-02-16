import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";

export interface UserRole {
  id: string;
  name: string;
}

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  tenantId: string;
  isActive: boolean;
  roles: UserRole[];
  createdAt: string;
  employeeId?: string;
  employeeName?: string;
}

export type EmployeeLinkOption = "None" | "LinkExisting" | "CreateNew";

export interface CreateUserEmployeeDto {
  employeeCode: string;
  phone?: string;
  mobile?: string;
  jobTitle?: string;
  department?: string;
  primarySiteId?: string;
}

export interface CreateUserDto {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  confirmPassword: string;
  isActive: boolean;
  roleIds: string[];
  employeeLinkOption?: EmployeeLinkOption;
  existingEmployeeId?: string;
  newEmployee?: CreateUserEmployeeDto;
}

export interface UpdateUserDto {
  firstName: string;
  lastName: string;
  isActive: boolean;
  roleIds: string[];
}

export interface ResetPasswordDto {
  newPassword: string;
  confirmPassword: string;
}

export interface ChangePasswordDto {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface GetUsersParams {
  pageNumber?: number;
  pageSize?: number;
  sortColumn?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
  roleId?: string;
  isActive?: boolean;
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

export async function getUsers(
  params?: GetUsersParams
): Promise<PaginatedResponse<User>> {
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
  if (params?.roleId) {
    queryParams.append("roleId", params.roleId);
  }
  if (params?.isActive !== undefined) {
    queryParams.append("isActive", String(params.isActive));
  }

  const queryString = queryParams.toString();
  const url = queryString ? `/users?${queryString}` : "/users";

  const response = await apiClient.get<ApiResponse<PaginatedResponse<User>>>(url);

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

export async function getAllUsers(): Promise<User[]> {
  const response = await apiClient.get<ApiResponse<User[]>>("/users/all");
  return response.data.data ?? [];
}

export async function getUser(id: string): Promise<User> {
  const response = await apiClient.get<ApiResponse<User>>(`/users/${id}`);
  return response.data.data;
}

export async function createUser(data: CreateUserDto): Promise<User> {
  const response = await apiClient.post<ApiResponse<User>>("/users", data);
  return response.data.data;
}

export async function updateUser(id: string, data: UpdateUserDto): Promise<User> {
  const response = await apiClient.put<ApiResponse<User>>(`/users/${id}`, data);
  return response.data.data;
}

export async function deleteUser(id: string): Promise<void> {
  await apiClient.delete(`/users/${id}`);
}

export async function resetPassword(id: string, data: ResetPasswordDto): Promise<void> {
  await apiClient.post(`/users/${id}/reset-password`, data);
}

export async function changePassword(data: ChangePasswordDto): Promise<void> {
  await apiClient.post("/users/change-password", data);
}

// User linkage DTOs

export interface UnlinkedUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  isActive: boolean;
  roleNames: string[];
}

export interface LinkUserToEmployeeDto {
  employeeId: string;
}

export interface CreateEmployeeForUserDto {
  employeeCode: string;
  phone?: string;
  mobile?: string;
  jobTitle?: string;
  department?: string;
  primarySiteId?: string;
  preferredLanguage?: string;
}

// User linkage functions

export async function getUnlinkedUsers(): Promise<UnlinkedUser[]> {
  const response = await apiClient.get<ApiResponse<UnlinkedUser[]>>("/users/unlinked");
  return response.data.data ?? [];
}

export async function linkUserToEmployee(
  userId: string,
  data: LinkUserToEmployeeDto
): Promise<User> {
  const response = await apiClient.post<ApiResponse<User>>(
    `/users/${userId}/link-employee`,
    data
  );
  return response.data.data;
}

export async function createEmployeeForUser(
  userId: string,
  data: CreateEmployeeForUserDto
): Promise<User> {
  const response = await apiClient.post<ApiResponse<User>>(
    `/users/${userId}/create-employee`,
    data
  );
  return response.data.data;
}

export async function unlinkUserFromEmployee(userId: string): Promise<void> {
  await apiClient.delete(`/users/${userId}/unlink-employee`);
}
