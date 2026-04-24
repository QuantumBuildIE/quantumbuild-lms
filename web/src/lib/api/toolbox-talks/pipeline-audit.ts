import { apiClient } from '../client';

const BASE = '/toolbox-talks/pipeline';

// ─── Types ────────────────────────────────────────────────────────────────────

export type PipelineChangeStatus = 'Draft' | 'ReadyForReview' | 'PendingApproval' | 'Approved' | 'BlockedRegression';

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
  status: PipelineChangeStatus;
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

// ─── Term Gate Types ──────────────────────────────────────────────────────────

export interface TermGateCheckRequest {
  sourceText: string;
  targetText: string;
  languageCode: string;
  sectorKey: string;
}

export interface TermGatePassingTerm {
  termId: string;
  englishTerm: string;
  approvedTranslation: string;
}

export interface TermGateFailure {
  termId: string;
  englishTerm: string;
  expectedTranslation: string;
  failureReason: 'missing_approved' | 'forbidden_present';
  forbiddenTermFound?: string;
}

export interface TermGateCheckResult {
  passed: boolean;
  checkedCount: number;
  failures: TermGateFailure[];
  passingTerms: TermGatePassingTerm[];
}

export interface TermGateSectorSummaryItem {
  sectorKey: string;
  sectorName: string;
  termCount: number;
}

export interface TermGateSummaryDto {
  totalTerms: number;
  criticalTerms: number;
  termsBySector: TermGateSectorSummaryItem[];
  languagesWithCoverage: string[];
}

// ─── Corpus Types ─────────────────────────────────────────────────────────────

export type CorpusVerdict = 'Pass' | 'Fail' | 'Inconclusive';
export type CorpusRunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';

export interface AuditCorpusEntryDto {
  id: string;
  entryRef: string;
  sectionTitle: string;
  originalText: string;
  translatedText: string;
  sourceLanguage: string;
  targetLanguage: string;
  sectorKey: string;
  passThreshold: number;
  expectedOutcome: 'Pass' | 'Review' | 'Fail';
  isSafetyCritical: boolean;
  tagsJson?: string;
  isActive: boolean;
  pipelineVersionIdAtFreeze?: string;
}

export interface AuditCorpusDto {
  id: string;
  corpusId: string;
  name: string;
  description?: string;
  sectorKey: string;
  languagePair: string;
  sourceTalkId?: string;
  sourceTalkTitle?: string;
  frozenFromPipelineVersionId?: string;
  isLocked: boolean;
  lockedAt?: string;
  lockedBy?: string;
  signedBy?: string;
  version: number;
  entryCount: number;
  activeEntryCount: number;
  createdAt: string;
  entries: AuditCorpusEntryDto[];
}

export interface CorpusRunResultDto {
  id: string;
  corpusEntryId: string;
  entryRef: string;
  sectionTitle: string;
  finalScore: number;
  outcome: 'Pass' | 'Review' | 'Fail';
  expectedOutcome: 'Pass' | 'Review' | 'Fail';
  isRegression: boolean;
  scoreDelta: number;
  roundsUsed: number;
  isSafetyCritical: boolean;
  effectiveThreshold: number;
  backTranslationA?: string;
  backTranslationB?: string;
  backTranslationC?: string;
  backTranslationD?: string;
  scoreA?: number;
  scoreB?: number;
  scoreC?: number;
  scoreD?: number;
  glossaryCorrectionsJson?: string;
  reviewReasonsJson?: string;
  wasCached: boolean;
}

export interface CorpusRunSummaryDto {
  id: string;
  corpusId: string;
  status: CorpusRunStatus;
  triggerType: string;
  triggeredBy: string;
  isSmokeTest: boolean;
  totalEntries: number;
  passedEntries: number;
  reviewEntries: number;
  failedEntries: number;
  regressionEntries: number;
  meanScore?: number;
  maxScoreDrop?: number;
  verdict?: CorpusVerdict;
  estimatedCostEur?: number;
  actualCostEur?: number;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
  linkedPipelineChangeId?: string;
  createdAt: string;
}

export interface CorpusRunDetailDto extends CorpusRunSummaryDto {
  results: CorpusRunResultDto[];
}

export interface CorpusRunDiffEntry {
  corpusEntryId: string;
  entryRef: string;
  sectionTitle: string;
  expectedOutcome: 'Pass' | 'Review' | 'Fail';
  currentOutcome: 'Pass' | 'Review' | 'Fail';
  previousOutcome?: 'Pass' | 'Review' | 'Fail';
  currentScore: number;
  previousScore?: number;
  scoreDelta: number;
  isRegression: boolean;
  isImprovement: boolean;
}

export interface CorpusRunDiffDto {
  currentRunId: string;
  previousRunId?: string;
  totalEntries: number;
  regressionCount: number;
  improvementCount: number;
  unchangedCount: number;
  entries: CorpusRunDiffEntry[];
}

export interface FreezeCorpusRequest {
  talkId: string;
  name: string;
  description?: string;
  sectionIndexes: number[];
}

export interface LockCorpusRequest {
  signedBy?: string;
}

export interface AddCorpusEntryRequest {
  sectionTitle: string;
  originalText: string;
  translatedText: string;
  sourceLanguage: string;
  targetLanguage: string;
  sectorKey: string;
  passThreshold: number;
  expectedOutcome: 'Pass' | 'Review' | 'Fail';
  isSafetyCritical: boolean;
  tagsJson?: string;
}

export interface TriggerCorpusRunRequest {
  isSmokeTest?: boolean;
  linkedPipelineChangeId?: string;
}

export interface TriggerCorpusRunResponse {
  runId: string;
  estimatedCostEur: number;
  estimatedEntries: number;
  requiresConfirmation: boolean;
  requiresSuperUserApproval: boolean;
  message: string;
}

export interface UpdateChangeStatusRequest {
  status: PipelineChangeStatus;
  justification?: string;
}

// ─── Corpus API functions ─────────────────────────────────────────────────────

export async function getCorpora(params: {
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<AuditCorpusDto>> {
  const res = await apiClient.get(`${BASE}/corpus`, { params });
  return res.data;
}

export async function freezeCorpus(
  request: FreezeCorpusRequest
): Promise<AuditCorpusDto> {
  const res = await apiClient.post(`${BASE}/corpus/freeze`, request);
  return res.data;
}

export async function getCorpus(id: string): Promise<AuditCorpusDto> {
  const res = await apiClient.get(`${BASE}/corpus/${id}`);
  return res.data;
}

export async function lockCorpus(
  id: string,
  request: LockCorpusRequest
): Promise<AuditCorpusDto> {
  const res = await apiClient.put(`${BASE}/corpus/${id}/lock`, request);
  return res.data;
}

export async function addCorpusEntry(
  corpusId: string,
  request: AddCorpusEntryRequest
): Promise<AuditCorpusEntryDto> {
  const res = await apiClient.post(`${BASE}/corpus/${corpusId}/entries`, request);
  return res.data;
}

export async function removeCorpusEntry(
  corpusId: string,
  entryId: string
): Promise<void> {
  await apiClient.delete(`${BASE}/corpus/${corpusId}/entries/${entryId}`);
}

export async function getCorpusRuns(
  corpusId: string
): Promise<CorpusRunSummaryDto[]> {
  const res = await apiClient.get(`${BASE}/corpus/${corpusId}/runs`);
  return res.data;
}

export async function triggerCorpusRun(
  corpusId: string,
  request: TriggerCorpusRunRequest
): Promise<TriggerCorpusRunResponse> {
  const res = await apiClient.post(`${BASE}/corpus/${corpusId}/runs`, request);
  return res.data;
}

export async function confirmCorpusRun(
  corpusId: string,
  runId: string
): Promise<{ runId: string; message: string }> {
  const res = await apiClient.post(`${BASE}/corpus/${corpusId}/runs/confirm`, { runId });
  return res.data;
}

export async function getCorpusRunDetail(runId: string): Promise<CorpusRunDetailDto> {
  const res = await apiClient.get(`${BASE}/corpus/runs/${runId}`);
  return res.data;
}

export async function getCorpusRunDiff(runId: string): Promise<CorpusRunDiffDto> {
  const res = await apiClient.get(`${BASE}/corpus/runs/${runId}/diff`);
  return res.data;
}

export async function updateChangeStatus(
  id: string,
  request: UpdateChangeStatusRequest
): Promise<PipelineChangeRecordDto> {
  const res = await apiClient.put(`${BASE}/changes/${id}/status`, request);
  return res.data;
}

// ─── Term Gate API functions ──────────────────────────────────────────────────

export async function checkTermGate(
  request: TermGateCheckRequest
): Promise<TermGateCheckResult> {
  const res = await apiClient.post(`${BASE}/term-gate/check`, request);
  return res.data;
}

export async function getTermGateSummary(): Promise<TermGateSummaryDto> {
  const res = await apiClient.get(`${BASE}/term-gate/summary`);
  return res.data;
}
