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
  checkTermGate,
  getTermGateSummary,
  getCorpora,
  freezeCorpus,
  getCorpus,
  lockCorpus,
  addCorpusEntry,
  removeCorpusEntry,
  getCorpusRuns,
  triggerCorpusRun,
  confirmCorpusRun,
  getCorpusRunDetail,
  getCorpusRunDiff,
  updateChangeStatus,
  type CreateDeviationRequest,
  type UpdateDeviationStatusRequest,
  type CreatePipelineChangeRecordRequest,
  type TermGateCheckRequest,
  type FreezeCorpusRequest,
  type LockCorpusRequest,
  type AddCorpusEntryRequest,
  type TriggerCorpusRunRequest,
  type UpdateChangeStatusRequest,
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

export function useTermGateSummary() {
  return useQuery({
    queryKey: ['pipeline-audit', 'term-gate-summary'],
    queryFn: () => getTermGateSummary(),
    staleTime: 5 * 60 * 1000,
  });
}

export function useTermGateCheck() {
  return useMutation({
    mutationFn: (request: TermGateCheckRequest) => checkTermGate(request),
  });
}

// ─── Corpus hooks ─────────────────────────────────────────────────────────────

export function useCorpora(params: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: ['pipeline-audit', 'corpora', params],
    queryFn: () => getCorpora(params),
    staleTime: 30_000,
  });
}

export function useCorpus(id: string | null) {
  return useQuery({
    queryKey: ['pipeline-audit', 'corpus', id],
    queryFn: () => getCorpus(id!),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCorpusRuns(corpusId: string | null) {
  return useQuery({
    queryKey: ['pipeline-audit', 'corpus-runs', corpusId],
    queryFn: () => getCorpusRuns(corpusId!),
    enabled: !!corpusId,
    staleTime: 15_000,
  });
}

export function useCorpusRunDetail(runId: string | null) {
  return useQuery({
    queryKey: ['pipeline-audit', 'corpus-run-detail', runId],
    queryFn: () => getCorpusRunDetail(runId!),
    enabled: !!runId,
    staleTime: 15_000,
  });
}

export function useCorpusRunDiff(runId: string | null) {
  return useQuery({
    queryKey: ['pipeline-audit', 'corpus-run-diff', runId],
    queryFn: () => getCorpusRunDiff(runId!),
    enabled: !!runId,
    staleTime: 30_000,
  });
}

export function useFreezeCorpus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: FreezeCorpusRequest) => freezeCorpus(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'corpora'] });
    },
  });
}

export function useLockCorpus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: LockCorpusRequest }) =>
      lockCorpus(id, request),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'corpus', id] });
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'corpora'] });
    },
  });
}

export function useAddCorpusEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ corpusId, request }: { corpusId: string; request: AddCorpusEntryRequest }) =>
      addCorpusEntry(corpusId, request),
    onSuccess: (_data, { corpusId }) => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'corpus', corpusId] });
    },
  });
}

export function useRemoveCorpusEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ corpusId, entryId }: { corpusId: string; entryId: string }) =>
      removeCorpusEntry(corpusId, entryId),
    onSuccess: (_data, { corpusId }) => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'corpus', corpusId] });
    },
  });
}

export function useTriggerCorpusRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ corpusId, request }: { corpusId: string; request: TriggerCorpusRunRequest }) =>
      triggerCorpusRun(corpusId, request),
    onSuccess: (_data, { corpusId }) => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'corpus-runs', corpusId] });
    },
  });
}

export function useConfirmCorpusRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ corpusId, runId }: { corpusId: string; runId: string }) =>
      confirmCorpusRun(corpusId, runId),
    onSuccess: (_data, { corpusId }) => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'corpus-runs', corpusId] });
    },
  });
}

export function useUpdateChangeStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateChangeStatusRequest }) =>
      updateChangeStatus(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'changes'] });
      queryClient.invalidateQueries({ queryKey: ['pipeline-audit', 'dashboard'] });
    },
  });
}
