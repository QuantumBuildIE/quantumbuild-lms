import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";
import type { Employee } from "@/types/admin";

export interface CreateEmployeeDto {
  /** Employee code is auto-generated on the backend - any value sent will be ignored */
  employeeCode?: string;
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  mobile?: string;
  jobTitle?: string;
  /** Legacy free-text department, no longer sent by any employee form */
  department?: string;
  departmentId?: string;
  primarySiteId?: string;
  startDate?: string;
  endDate?: string;
  isActive?: boolean;
  notes?: string;
  /** If true, creates a linked User account when Email is provided */
  createUserAccount?: boolean;
  /** Optional role name to assign to the created user */
  userRole?: string;
  /** Preferred language for Learning subtitles and notifications (ISO 639-1 code) */
  preferredLanguage?: string;
}

export interface UpdateEmployeeDto {
  employeeCode: string;
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  mobile?: string;
  jobTitle?: string;
  /** Legacy free-text department, no longer sent by any employee form */
  department?: string;
  departmentId?: string | null;
  primarySiteId?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  isActive?: boolean;
  notes?: string;
  /** Preferred language for Learning subtitles and notifications (ISO 639-1 code) */
  preferredLanguage?: string;
}

export interface GetEmployeesParams {
  pageNumber?: number;
  pageSize?: number;
  sortColumn?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
  /** Filter to a specific department. Ignored when departmentUnassigned is true. */
  departmentId?: string;
  /** Filter to employees with no department assigned. */
  departmentUnassigned?: boolean;
  /** Filter to a specific site/location. Ignored when siteUnassigned is true. */
  siteId?: string;
  /** Filter to employees with no site/location assigned. */
  siteUnassigned?: boolean;
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

export async function getEmployees(
  params?: GetEmployeesParams
): Promise<PaginatedResponse<Employee>> {
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
  if (params?.departmentUnassigned) {
    queryParams.append("departmentUnassigned", "true");
  } else if (params?.departmentId) {
    queryParams.append("departmentId", params.departmentId);
  }
  if (params?.siteUnassigned) {
    queryParams.append("siteUnassigned", "true");
  } else if (params?.siteId) {
    queryParams.append("siteId", params.siteId);
  }

  const queryString = queryParams.toString();
  const url = queryString ? `/employees?${queryString}` : "/employees";

  const response = await apiClient.get<ApiResponse<PaginatedResponse<Employee>>>(url);

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

export async function getAllEmployees(): Promise<Employee[]> {
  const response = await apiClient.get<ApiResponse<Employee[]>>("/employees/all");
  return response.data.data ?? [];
}

export async function getUnlinkedEmployees(): Promise<Employee[]> {
  const response = await apiClient.get<ApiResponse<Employee[]>>("/employees/unlinked");
  return response.data.data ?? [];
}

export async function getEmployee(id: string): Promise<Employee> {
  const response = await apiClient.get<ApiResponse<Employee>>(`/employees/${id}`);
  return response.data.data;
}

export async function createEmployee(data: CreateEmployeeDto): Promise<Employee> {
  const response = await apiClient.post<ApiResponse<Employee>>("/employees", data);
  return response.data.data;
}

export async function updateEmployee(
  id: string,
  data: UpdateEmployeeDto
): Promise<Employee> {
  const response = await apiClient.put<ApiResponse<Employee>>(
    `/employees/${id}`,
    data
  );
  return response.data.data;
}

export async function deleteEmployee(id: string): Promise<void> {
  await apiClient.delete(`/employees/${id}`);
}

export async function resendInvite(id: string): Promise<void> {
  await apiClient.post(`/employees/${id}/resend-invite`);
}

// Employee linkage DTOs

export interface LinkEmployeeToUserDto {
  userId: string;
}

export interface CreateUserForEmployeeDto {
  roleIds: string[];
}

// Employee linkage functions

export async function linkEmployeeToUser(
  employeeId: string,
  data: LinkEmployeeToUserDto
): Promise<Employee> {
  const response = await apiClient.post<ApiResponse<Employee>>(
    `/employees/${employeeId}/link-user`,
    data
  );
  return response.data.data;
}

export async function createUserForEmployee(
  employeeId: string,
  data: CreateUserForEmployeeDto
): Promise<Employee> {
  const response = await apiClient.post<ApiResponse<Employee>>(
    `/employees/${employeeId}/create-user`,
    data
  );
  return response.data.data;
}

export async function unlinkEmployeeFromUser(employeeId: string): Promise<void> {
  await apiClient.delete(`/employees/${employeeId}/unlink-user`);
}

export async function resetEmployeePin(employeeId: string): Promise<void> {
  await apiClient.post(`/employees/${employeeId}/reset-pin`);
}
