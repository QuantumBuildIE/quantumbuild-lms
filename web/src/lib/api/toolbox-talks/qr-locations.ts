import { apiClient } from '@/lib/api/client';

export type ContentMode = 'ViewOnly' | 'Training' | 'Induction';

export interface QrLocationDto {
  id: string;
  name: string;
  description?: string;
  address?: string;
  isActive: boolean;
  createdAt: string;
  qrCodeCount: number;
}

export interface QrCodeDto {
  id: string;
  qrLocationId: string;
  toolboxTalkId?: string;
  talkTitle?: string;
  name: string;
  contentMode: ContentMode;
  codeToken: string;
  isActive: boolean;
  qrImageUrl?: string;
}

export interface QrCodePublicDto {
  codeToken: string;
  locationName: string;
  talkId?: string;
  talkTitle?: string;
  contentMode: ContentMode;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface CreateQrLocationRequest {
  name: string;
  description?: string;
  address?: string;
}

export interface UpdateQrLocationRequest {
  name: string;
  description?: string;
  address?: string;
  isActive: boolean;
}

export interface CreateQrCodeRequest {
  name: string;
  toolboxTalkId?: string;
  contentMode: string;
}

export interface UpdateQrCodeRequest {
  name: string;
  toolboxTalkId?: string;
  contentMode: string;
  isActive: boolean;
}

export async function getQrLocations(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
}): Promise<PaginatedResult<QrLocationDto>> {
  const response = await apiClient.get<PaginatedResult<QrLocationDto>>(
    '/qr-locations',
    { params }
  );
  return response.data;
}

export async function createQrLocation(
  request: CreateQrLocationRequest
): Promise<QrLocationDto> {
  const response = await apiClient.post<QrLocationDto>('/qr-locations', request);
  return response.data;
}

export async function updateQrLocation(
  id: string,
  request: UpdateQrLocationRequest
): Promise<void> {
  await apiClient.put(`/qr-locations/${id}`, request);
}

export async function deleteQrLocation(id: string): Promise<void> {
  await apiClient.delete(`/qr-locations/${id}`);
}

export async function getQrCodes(locationId: string): Promise<QrCodeDto[]> {
  const response = await apiClient.get<QrCodeDto[]>(
    `/qr-locations/${locationId}/codes`
  );
  return response.data;
}

export async function createQrCode(
  locationId: string,
  request: CreateQrCodeRequest
): Promise<QrCodeDto> {
  const response = await apiClient.post<QrCodeDto>(
    `/qr-locations/${locationId}/codes`,
    request
  );
  return response.data;
}

export async function updateQrCode(
  locationId: string,
  codeId: string,
  request: UpdateQrCodeRequest
): Promise<void> {
  await apiClient.put(`/qr-locations/${locationId}/codes/${codeId}`, request);
}

export async function deleteQrCode(
  locationId: string,
  codeId: string
): Promise<void> {
  await apiClient.delete(`/qr-locations/${locationId}/codes/${codeId}`);
}

// ── Session types ─────────────────────────────────────────────────────────────

export type QrSessionStatus = 'Active' | 'Completed' | 'Abandoned';

export interface QrSessionListItemDto {
  id: string;
  sessionToken: string;
  employeeName: string;
  locationName: string;
  talkTitle?: string;
  contentMode: ContentMode;
  language: string;
  status: QrSessionStatus;
  startedAt: string;
  completedAt?: string;
  score?: number;
}

export interface QrSessionsByLocation {
  locationName: string;
  count: number;
}

export interface QrSessionsByLanguage {
  language: string;
  count: number;
}

export interface QrSessionSummaryDto {
  totalSessions: number;
  completedSessions: number;
  abandonedSessions: number;
  activeSessions: number;
  averageScore?: number;
  sessionsByLocation: QrSessionsByLocation[];
  sessionsByLanguage: QrSessionsByLanguage[];
}

export interface QrSessionsParams {
  employeeId?: string;
  qrCodeId?: string;
  status?: QrSessionStatus;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function getQrSessions(
  params?: QrSessionsParams
): Promise<PaginatedResult<QrSessionListItemDto>> {
  const response = await apiClient.get<PaginatedResult<QrSessionListItemDto>>(
    '/qr-locations/sessions',
    { params }
  );
  return response.data;
}

export async function getQrSessionsSummary(): Promise<QrSessionSummaryDto> {
  const response = await apiClient.get<QrSessionSummaryDto>(
    '/qr-locations/sessions/summary'
  );
  return response.data;
}

// ── Employee training history types ──────────────────────────────────────────

export type TrainingHistoryItemType = 'ScheduledTalk' | 'QrSession';

export interface EmployeeTrainingHistoryItemDto {
  type: TrainingHistoryItemType;
  itemId: string;
  talkTitle: string;
  locationName?: string;
  contentMode?: ContentMode;
  score?: number;
  language?: string;
  completedAt: string;
}

export async function getEmployeeTrainingHistory(
  employeeId: string,
  params?: { page?: number; pageSize?: number }
): Promise<PaginatedResult<EmployeeTrainingHistoryItemDto>> {
  const response = await apiClient.get<PaginatedResult<EmployeeTrainingHistoryItemDto>>(
    `/employees/${employeeId}/training-history`,
    { params }
  );
  return response.data;
}
