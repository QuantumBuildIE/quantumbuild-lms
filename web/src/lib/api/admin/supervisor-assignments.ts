import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";

export interface SupervisorOperatorDto {
  employeeId: string;
  employeeCode: string;
  fullName: string;
  department?: string;
  jobTitle?: string;
}

export interface SupervisorAssignmentDto {
  id: string;
  supervisorEmployeeId: string;
  supervisorName: string;
  operatorEmployeeId: string;
  operatorName: string;
  assignedAt: string;
  assignedBy: string;
}

export interface AssignOperatorsDto {
  operatorEmployeeIds: string[];
}

export async function getAssignedOperators(
  supervisorId: string
): Promise<SupervisorOperatorDto[]> {
  const response = await apiClient.get<ApiResponse<SupervisorOperatorDto[]>>(
    `/employees/${supervisorId}/operators`
  );
  return response.data.data ?? [];
}

export async function getAvailableOperators(
  supervisorId: string
): Promise<SupervisorOperatorDto[]> {
  const response = await apiClient.get<ApiResponse<SupervisorOperatorDto[]>>(
    `/employees/${supervisorId}/operators/available`
  );
  return response.data.data ?? [];
}

export async function assignOperators(
  supervisorId: string,
  data: AssignOperatorsDto
): Promise<SupervisorAssignmentDto[]> {
  const response = await apiClient.post<ApiResponse<SupervisorAssignmentDto[]>>(
    `/employees/${supervisorId}/operators`,
    data
  );
  return response.data.data ?? [];
}

export async function unassignOperator(
  supervisorId: string,
  operatorId: string
): Promise<void> {
  await apiClient.delete(`/employees/${supervisorId}/operators/${operatorId}`);
}

export async function getMyOperators(): Promise<SupervisorOperatorDto[]> {
  const response = await apiClient.get<ApiResponse<SupervisorOperatorDto[]>>(
    "/employees/my-operators"
  );
  return response.data.data ?? [];
}
