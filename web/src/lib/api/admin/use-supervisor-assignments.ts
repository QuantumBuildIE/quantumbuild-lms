import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getAssignedOperators,
  getAvailableOperators,
  assignOperators,
  unassignOperator,
  getMyOperators,
  type AssignOperatorsDto,
} from "./supervisor-assignments";
import { EMPLOYEES_KEY } from "./use-employees";

export const SUPERVISOR_ASSIGNMENTS_KEY = ["supervisor-assignments"];

export function useAssignedOperators(supervisorId: string) {
  return useQuery({
    queryKey: [...SUPERVISOR_ASSIGNMENTS_KEY, supervisorId, "assigned"],
    queryFn: () => getAssignedOperators(supervisorId),
    enabled: !!supervisorId,
  });
}

export function useAvailableOperators(supervisorId: string, enabled = true) {
  return useQuery({
    queryKey: [...SUPERVISOR_ASSIGNMENTS_KEY, supervisorId, "available"],
    queryFn: () => getAvailableOperators(supervisorId),
    enabled: !!supervisorId && enabled,
  });
}

export function useAssignOperators() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      supervisorId,
      data,
    }: {
      supervisorId: string;
      data: AssignOperatorsDto;
    }) => assignOperators(supervisorId, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: [...SUPERVISOR_ASSIGNMENTS_KEY, variables.supervisorId],
      });
    },
  });
}

export function useUnassignOperator() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      supervisorId,
      operatorId,
    }: {
      supervisorId: string;
      operatorId: string;
    }) => unassignOperator(supervisorId, operatorId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: [...SUPERVISOR_ASSIGNMENTS_KEY, variables.supervisorId],
      });
    },
  });
}

export function useMyOperators() {
  return useQuery({
    queryKey: [...SUPERVISOR_ASSIGNMENTS_KEY, "my-operators"],
    queryFn: getMyOperators,
  });
}

export type {
  SupervisorOperatorDto,
  SupervisorAssignmentDto,
  AssignOperatorsDto,
} from "./supervisor-assignments";
