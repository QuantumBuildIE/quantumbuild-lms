import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";

export interface ContactDto {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  companyId?: string;
  companyName?: string;
  siteId?: string;
  siteName?: string;
  isPrimaryContact: boolean;
  isActive: boolean;
  notes?: string;
}

export interface CreateContactDto {
  firstName: string;
  lastName: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  companyId?: string;
  siteId?: string;
  isPrimaryContact?: boolean;
  isActive?: boolean;
  notes?: string;
}

export interface UpdateContactDto {
  firstName: string;
  lastName: string;
  jobTitle?: string | null;
  email?: string | null;
  phone?: string | null;
  mobile?: string | null;
  companyId?: string | null;
  siteId?: string | null;
  isPrimaryContact?: boolean;
  isActive?: boolean;
  notes?: string | null;
}

export interface GetContactsParams {
  pageNumber?: number;
  pageSize?: number;
  sortColumn?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
  companyId?: string;
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

// Contacts nested under company
export async function getCompanyContacts(companyId: string): Promise<ContactDto[]> {
  const response = await apiClient.get<ApiResponse<ContactDto[]>>(
    `/companies/${companyId}/contacts`
  );
  return response.data.data ?? [];
}

export async function getCompanyContact(companyId: string, id: string): Promise<ContactDto> {
  const response = await apiClient.get<ApiResponse<ContactDto>>(
    `/companies/${companyId}/contacts/${id}`
  );
  return response.data.data;
}

export async function createCompanyContact(
  companyId: string,
  data: CreateContactDto
): Promise<ContactDto> {
  const response = await apiClient.post<ApiResponse<ContactDto>>(
    `/companies/${companyId}/contacts`,
    data
  );
  return response.data.data;
}

export async function updateCompanyContact(
  companyId: string,
  id: string,
  data: UpdateContactDto
): Promise<ContactDto> {
  const response = await apiClient.put<ApiResponse<ContactDto>>(
    `/companies/${companyId}/contacts/${id}`,
    data
  );
  return response.data.data;
}

export async function deleteCompanyContact(companyId: string, id: string): Promise<void> {
  await apiClient.delete(`/companies/${companyId}/contacts/${id}`);
}

// All contacts (independent of company)
export async function getContacts(
  params?: GetContactsParams
): Promise<PaginatedResponse<ContactDto>> {
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
  if (params?.companyId) {
    queryParams.append("companyId", params.companyId);
  }

  const queryString = queryParams.toString();
  const url = queryString ? `/contacts?${queryString}` : "/contacts";

  const response = await apiClient.get<ApiResponse<PaginatedResponse<ContactDto>>>(url);

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

export async function getAllContacts(): Promise<ContactDto[]> {
  const response = await apiClient.get<ApiResponse<ContactDto[]>>("/contacts/all");
  return response.data.data ?? [];
}

export async function getContact(id: string): Promise<ContactDto> {
  const response = await apiClient.get<ApiResponse<ContactDto>>(`/contacts/${id}`);
  return response.data.data;
}

export async function createContact(data: CreateContactDto): Promise<ContactDto> {
  const response = await apiClient.post<ApiResponse<ContactDto>>("/contacts", data);
  return response.data.data;
}

export async function updateContact(id: string, data: UpdateContactDto): Promise<ContactDto> {
  const response = await apiClient.put<ApiResponse<ContactDto>>(`/contacts/${id}`, data);
  return response.data.data;
}

export async function deleteContact(id: string): Promise<void> {
  await apiClient.delete(`/contacts/${id}`);
}
