import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getCompanyContacts,
  getCompanyContact,
  createCompanyContact,
  updateCompanyContact,
  deleteCompanyContact,
  getContacts,
  getAllContacts,
  getContact,
  createContact,
  updateContact,
  deleteContact,
  type CreateContactDto,
  type UpdateContactDto,
  type GetContactsParams,
} from "./contacts";
import { COMPANIES_KEY } from "./use-companies";

export const CONTACTS_KEY = ["contacts"];

// Company contacts hooks
export function useCompanyContacts(companyId: string) {
  return useQuery({
    queryKey: [...CONTACTS_KEY, "company", companyId],
    queryFn: () => getCompanyContacts(companyId),
    enabled: !!companyId,
  });
}

export function useCompanyContact(companyId: string, id: string) {
  return useQuery({
    queryKey: [...CONTACTS_KEY, "company", companyId, id],
    queryFn: () => getCompanyContact(companyId, id),
    enabled: !!companyId && !!id,
  });
}

export function useCreateCompanyContact(companyId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateContactDto) => createCompanyContact(companyId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...CONTACTS_KEY, "company", companyId] });
      queryClient.invalidateQueries({ queryKey: CONTACTS_KEY });
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export function useUpdateCompanyContact(companyId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateContactDto }) =>
      updateCompanyContact(companyId, id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...CONTACTS_KEY, "company", companyId] });
      queryClient.invalidateQueries({ queryKey: CONTACTS_KEY });
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export function useDeleteCompanyContact(companyId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteCompanyContact(companyId, id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...CONTACTS_KEY, "company", companyId] });
      queryClient.invalidateQueries({ queryKey: CONTACTS_KEY });
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

// All contacts hooks (independent of company)
export function useContacts(params?: GetContactsParams) {
  return useQuery({
    queryKey: [...CONTACTS_KEY, params],
    queryFn: () => getContacts(params),
  });
}

export function useAllContacts() {
  return useQuery({
    queryKey: [...CONTACTS_KEY, "all"],
    queryFn: getAllContacts,
  });
}

export function useContact(id: string) {
  return useQuery({
    queryKey: [...CONTACTS_KEY, id],
    queryFn: () => getContact(id),
    enabled: !!id,
  });
}

export function useCreateContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateContactDto) => createContact(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CONTACTS_KEY });
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export function useUpdateContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateContactDto }) =>
      updateContact(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CONTACTS_KEY });
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export function useDeleteContact() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteContact(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CONTACTS_KEY });
      queryClient.invalidateQueries({ queryKey: COMPANIES_KEY });
    },
  });
}

export type { CreateContactDto, UpdateContactDto, GetContactsParams, ContactDto } from "./contacts";
export type { PaginatedResponse } from "./contacts";
