import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getCompanies,
  getAllCompanies,
  getCompany,
  createCompany,
  updateCompany,
  deleteCompany,
  type CreateCompanyDto,
  type UpdateCompanyDto,
  type GetCompaniesParams,
} from "./companies";

export const COMPANIES_KEY = ["companies"];

export function useCompanies(params?: GetCompaniesParams) {
  return useQuery({
    queryKey: [...COMPANIES_KEY, params],
    queryFn: () => getCompanies(params),
  });
}

export function useAllCompanies() {
  return useQuery({
    queryKey: [...COMPANIES_KEY, "all"],
    queryFn: getAllCompanies,
  });
}

export function useCompany(id: string) {
  return useQuery({
    queryKey: [...COMPANIES_KEY, id],
    queryFn: () => getCompany(id),
    enabled: !!id,
  });
}

export function useCreateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateCompanyDto) => createCompany(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export function useUpdateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateCompanyDto }) =>
      updateCompany(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export function useDeleteCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteCompany(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export type { CreateCompanyDto, UpdateCompanyDto, GetCompaniesParams } from "./companies";
export type { PaginatedResponse, CompanyWithContacts, ContactSummary } from "./companies";
