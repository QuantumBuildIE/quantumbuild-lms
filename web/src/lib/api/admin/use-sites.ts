import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getSites,
  getAllSites,
  getSite,
  createSite,
  updateSite,
  deleteSite,
  type CreateSiteDto,
  type UpdateSiteDto,
  type GetSitesParams,
} from "./sites";

export const SITES_KEY = ["sites"];

export function useSites(params?: GetSitesParams) {
  return useQuery({
    queryKey: [...SITES_KEY, params],
    queryFn: () => getSites(params),
  });
}

export function useAllSites() {
  return useQuery({
    queryKey: [...SITES_KEY, "all"],
    queryFn: getAllSites,
  });
}

export function useSite(id: string) {
  return useQuery({
    queryKey: [...SITES_KEY, id],
    queryFn: () => getSite(id),
    enabled: !!id,
  });
}

export function useCreateSite() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateSiteDto) => createSite(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SITES_KEY });
    },
  });
}

export function useUpdateSite() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateSiteDto }) =>
      updateSite(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SITES_KEY });
    },
  });
}

export function useDeleteSite() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteSite(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SITES_KEY });
    },
  });
}

export type { CreateSiteDto, UpdateSiteDto, GetSitesParams } from "./sites";
export type { PaginatedResponse } from "./sites";
