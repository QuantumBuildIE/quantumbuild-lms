'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getPipelineAuditDashboard,
  getModuleOutcomes,
  getDeviations,
  getDeviation,
  createDeviation,
  updateDeviationStatus,
  getChangeRecords,
  createChangeRecord,
  getActivePipelineVersion,
  type CreateDeviationRequest,
  type UpdateDeviationStatusRequest,
  type CreatePipelineChangeRecordRequest,
} from './pipeline-audit';

export function usePipelineAuditDashboard(tenantId?: string) {
  return useQuery({
    queryKey: ['pipeline-audit', 'dashboard', tenantId],
    queryFn: () => getPipelineAuditDashboard(tenantId),
    staleTime: 30_000,
  });
}

export function useModuleOutcomes(params: {
  outcome?: string;
  languageCode?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: ['pipeline-audit', 'runs', params],
    queryFn: () => getModuleOutcomes(params),
    staleTime: 30_000,
  });
}

export function useDeviations(params: {
  status?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: ['pipeline-audit', 'deviations', params],
    queryFn: () => getDeviations(params),
    staleTime: 30_000,
  });
}

export function useDeviation(id: string | null) {
  return useQuery({
    queryKey: ['pipeline-audit', 'deviation', id],
    queryFn: () => getDeviation(id!),
    enabled: !!id,
  });
}

export function useCreateDeviation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateDeviationRequest) => createDeviation(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit'] });
    },
  });
}

export function useUpdateDeviationStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      request,
    }: {
      id: string;
      request: UpdateDeviationStatusRequest;
    }) => updateDeviationStatus(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit'] });
    },
  });
}

export function useChangeRecords(params: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: ['pipeline-audit', 'changes', params],
    queryFn: () => getChangeRecords(params),
    staleTime: 60_000,
  });
}

export function useCreateChangeRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePipelineChangeRecordRequest) =>
      createChangeRecord(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit'] });
    },
  });
}

export function useActivePipelineVersion() {
  return useQuery({
    queryKey: ['pipeline-audit', 'version'],
    queryFn: () => getActivePipelineVersion(),
    staleTime: 60_000,
  });
}
