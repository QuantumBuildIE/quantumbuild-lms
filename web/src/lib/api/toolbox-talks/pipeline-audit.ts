import { apiClient } from '../client';

const BASE = '/api/toolbox-talks/pipeline';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface TranslationDeviationDto {
  id: string;
  deviationId: string;
  detectedAt: string;
  detectedBy: string;
  validationRunId?: string;
  validationResultId?: string;
  moduleRef?: string;
  lessonRef?: string;
  languagePair?: string;
  sourceExcerpt?: string;
  targetExcerpt?: string;
  nature: string;
  rootCauseCategory: string;
  rootCauseDetail?: string;
  correctiveAction?: string;
  preventiveAction?: string;
  approver?: string;
  status: 'Open' | 'InProgress' | 'Closed';
  closedBy?: string;
  closedAt?: string;
  pipelineVersionAtTime?: string;
  createdAt: string;
}

export interface CreateDeviationRequest {
  detectedBy: string;
  validationRunId?: string;
  validationResultId?: string;
  moduleRef?: string;
  lessonRef?: string;
  languagePair?: string;
  sourceExcerpt?: string;
  targetExcerpt?: string;
  nature: string;
  rootCauseCategory: string;
  rootCauseDetail?: string;
  correctiveAction?: string;
  preventiveAction?: string;
  approver?: string;
}

export interface UpdateDeviationStatusRequest {
  status: string;
  closedBy?: string;
}

export interface PipelineChangeRecordDto {
  id: string;
  changeId: string;
  component: string;
  changeFrom: string;
  changeTo: string;
  justification: string;
  impactAssessment?: string;
  priorModulesAction?: string;
  approver?: string;
  deployedAt: string;
  pipelineVersionId: string;
  pipelineVersionHash?: string;
  pipelineVersionLabel?: string;
  previousPipelineVersionId?: string;
  createdAt: string;
}

export interface CreatePipelineChangeRecordRequest {
  component: string;
  changeFrom: string;
  changeTo: string;
  justification: string;
  impactAssessment?: string;
  priorModulesAction?: string;
  approver?: string;
  newVersionLabel: string;
}

export interface ModuleOutcomeDto {
  runId: string;
  toolboxTalkId?: string;
  talkTitle?: string;
  courseId?: string;
  courseTitle?: string;
  languageCode: string;
  sectorKey?: string;
  overallScore: number;
  overallOutcome: 'Pass' | 'Review' | 'Fail';
  isSafetyCritical: boolean;
  totalSections: number;
  passedSections: number;
  reviewSections: number;
  failedSections: number;
  completedAt?: string;
  pipelineVersionHash?: string;
  acceptedDecisions: number;
  rejectedDecisions: number;
  pendingDecisions: number;
}

export interface PipelineAuditDashboardDto {
  openDeviations: number;
  inProgressDeviations: number;
  closedDeviations: number;
  changeRecords: number;
  lockedTerms: number;
  moduleOutcomes: number;
  activePipelineVersion: string;
  activePipelineHash: string;
  mostRecentChangeRecord?: PipelineChangeRecordDto;
  topOpenDeviations: TranslationDeviationDto[];
}

export interface PaginatedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

// ─── API functions ─────────────────────────────────────────────────────────

export async function getPipelineAuditDashboard(
  tenantId?: string
): Promise<PipelineAuditDashboardDto> {
  const headers: Record<string, string> = {};
  if (tenantId) headers['X-Tenant-Id'] = tenantId;
  const res = await apiClient.get(`${BASE}/dashboard`, { headers });
  return res.data;
}

export async function getModuleOutcomes(params: {
  outcome?: string;
  languageCode?: string;
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<ModuleOutcomeDto>> {
  const res = await apiClient.get(`${BASE}/runs`, { params });
  return res.data;
}

export async function getDeviations(params: {
  status?: string;
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<TranslationDeviationDto>> {
  const res = await apiClient.get(`${BASE}/deviations`, { params });
  return res.data;
}

export async function getDeviation(id: string): Promise<TranslationDeviationDto> {
  const res = await apiClient.get(`${BASE}/deviations/${id}`);
  return res.data;
}

export async function createDeviation(
  request: CreateDeviationRequest
): Promise<TranslationDeviationDto> {
  const res = await apiClient.post(`${BASE}/deviations`, request);
  return res.data;
}

export async function updateDeviationStatus(
  id: string,
  request: UpdateDeviationStatusRequest
): Promise<TranslationDeviationDto> {
  const res = await apiClient.put(`${BASE}/deviations/${id}/status`, request);
  return res.data;
}

export async function getChangeRecords(params: {
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<PipelineChangeRecordDto>> {
  const res = await apiClient.get(`${BASE}/changes`, { params });
  return res.data;
}

export async function createChangeRecord(
  request: CreatePipelineChangeRecordRequest
): Promise<PipelineChangeRecordDto> {
  const res = await apiClient.post(`${BASE}/changes`, request);
  return res.data;
}

export async function getActivePipelineVersion(): Promise<{
  id?: string;
  version: string;
  hash: string;
  computedAt?: string;
  componentsJson?: string;
}> {
  const res = await apiClient.get(`${BASE}/version`);
  return res.data;
}
