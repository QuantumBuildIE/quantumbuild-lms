import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";
import type { Company, Contact } from "@/types/admin";

export interface ContactSummary {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  isPrimaryContact: boolean;
  isActive: boolean;
}

export interface CompanyWithContacts extends Company {
  tradingName?: string;
  registrationNumber?: string;
  vatNumber?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  county?: string;
  postalCode?: string;
  country?: string;
  website?: string;
  notes?: string;
  contactCount: number;
  contacts: ContactSummary[];
}

export interface CreateCompanyDto {
  companyCode: string;
  companyName: string;
  tradingName?: string;
  registrationNumber?: string;
  vatNumber?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  county?: string;
  postalCode?: string;
  country?: string;
  phone?: string;
  email?: string;
  website?: string;
  companyType?: string;
  isActive?: boolean;
  notes?: string;
}

export interface UpdateCompanyDto {
  companyCode: string;
  companyName: string;
  tradingName?: string | null;
  registrationNumber?: string | null;
  vatNumber?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  county?: string | null;
  postalCode?: string | null;
  country?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  companyType?: string | null;
  isActive?: boolean;
  notes?: string | null;
}

export interface GetCompaniesParams {
  pageNumber?: number;
  pageSize?: number;
  sortColumn?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
  companyType?: string;
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

export async function getCompanies(
  params?: GetCompaniesParams
): Promise<PaginatedResponse<CompanyWithContacts>> {
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
  if (params?.companyType) {
    queryParams.append("companyType", params.companyType);
  }

  const queryString = queryParams.toString();
  const url = queryString ? `/companies?${queryString}` : "/companies";

  const response = await apiClient.get<ApiResponse<PaginatedResponse<CompanyWithContacts>>>(url);

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

export async function getAllCompanies(): Promise<CompanyWithContacts[]> {
  const response = await apiClient.get<ApiResponse<CompanyWithContacts[]>>("/companies/all");
  return response.data.data ?? [];
}

export async function getCompany(id: string): Promise<CompanyWithContacts> {
  const response = await apiClient.get<ApiResponse<CompanyWithContacts>>(`/companies/${id}`);
  return response.data.data;
}

export async function createCompany(data: CreateCompanyDto): Promise<CompanyWithContacts> {
  const response = await apiClient.post<ApiResponse<CompanyWithContacts>>("/companies", data);
  return response.data.data;
}

export async function updateCompany(
  id: string,
  data: UpdateCompanyDto
): Promise<CompanyWithContacts> {
  const response = await apiClient.put<ApiResponse<CompanyWithContacts>>(
    `/companies/${id}`,
    data
  );
  return response.data.data;
}

export async function deleteCompany(id: string): Promise<void> {
  await apiClient.delete(`/companies/${id}`);
}
