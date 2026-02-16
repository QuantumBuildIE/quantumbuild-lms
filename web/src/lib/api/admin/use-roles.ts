import { useQuery } from "@tanstack/react-query";
import { getRoles } from "./roles";

export const ROLES_KEY = ["roles"];

export function useRoles() {
  return useQuery({
    queryKey: ROLES_KEY,
    queryFn: getRoles,
  });
}

export type { Role } from "./roles";
