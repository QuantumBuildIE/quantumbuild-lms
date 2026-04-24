import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getQrLocations,
  createQrLocation,
  updateQrLocation,
  deleteQrLocation,
  getQrCodes,
  createQrCode,
  updateQrCode,
  deleteQrCode,
  getQrSessions,
  getQrSessionsSummary,
  getEmployeeTrainingHistory,
  type CreateQrLocationRequest,
  type UpdateQrLocationRequest,
  type CreateQrCodeRequest,
  type UpdateQrCodeRequest,
  type QrSessionsParams,
} from './qr-locations';

const QR_LOCATIONS_KEY = ['qr-locations'];

export function useQrLocations(params?: { page?: number; pageSize?: number; search?: string }) {
  return useQuery({
    queryKey: [...QR_LOCATIONS_KEY, params],
    queryFn: () => getQrLocations(params),
  });
}

export function useQrCodes(locationId: string | null) {
  return useQuery({
    queryKey: [...QR_LOCATIONS_KEY, locationId, 'codes'],
    queryFn: () => getQrCodes(locationId!),
    enabled: !!locationId,
  });
}

export function useCreateQrLocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateQrLocationRequest) => createQrLocation(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QR_LOCATIONS_KEY });
    },
  });
}

export function useUpdateQrLocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateQrLocationRequest }) =>
      updateQrLocation(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QR_LOCATIONS_KEY });
    },
  });
}

export function useDeleteQrLocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteQrLocation(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QR_LOCATIONS_KEY });
    },
  });
}

export function useCreateQrCode(locationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateQrCodeRequest) => createQrCode(locationId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QR_LOCATIONS_KEY, locationId, 'codes'] });
      queryClient.invalidateQueries({ queryKey: QR_LOCATIONS_KEY });
    },
  });
}

export function useUpdateQrCode(locationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ codeId, data }: { codeId: string; data: UpdateQrCodeRequest }) =>
      updateQrCode(locationId, codeId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QR_LOCATIONS_KEY, locationId, 'codes'] });
    },
  });
}

export function useDeleteQrCode(locationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (codeId: string) => deleteQrCode(locationId, codeId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...QR_LOCATIONS_KEY, locationId, 'codes'] });
      queryClient.invalidateQueries({ queryKey: QR_LOCATIONS_KEY });
    },
  });
}

export function useQrSessions(params?: QrSessionsParams) {
  return useQuery({
    queryKey: [...QR_LOCATIONS_KEY, 'sessions', params],
    queryFn: () => getQrSessions(params),
  });
}

export function useQrSessionsSummary() {
  return useQuery({
    queryKey: [...QR_LOCATIONS_KEY, 'sessions', 'summary'],
    queryFn: () => getQrSessionsSummary(),
  });
}

export function useEmployeeTrainingHistory(
  employeeId: string,
  params?: { page?: number; pageSize?: number }
) {
  return useQuery({
    queryKey: ['employee-training-history', employeeId, params],
    queryFn: () => getEmployeeTrainingHistory(employeeId, params),
    enabled: !!employeeId,
  });
}
