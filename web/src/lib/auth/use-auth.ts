"use client";

import { useContext, useMemo } from "react";
import { AuthContext } from "./auth-context";

export function useAuth() {
  const context = useContext(AuthContext);

  if (context === undefined) {
    throw new Error("useAuth must be used within an AuthProvider");
  }

  return context;
}

export function usePermission(permission: string): boolean {
  const { user } = useAuth();

  return useMemo(() => {
    if (!user) return false;
    return user.permissions.includes(permission);
  }, [user, permission]);
}

export function usePermissions(permissions: string[]): Record<string, boolean> {
  const { user } = useAuth();

  return useMemo(() => {
    if (!user) {
      return permissions.reduce(
        (acc, perm) => {
          acc[perm] = false;
          return acc;
        },
        {} as Record<string, boolean>
      );
    }

    return permissions.reduce(
      (acc, perm) => {
        acc[perm] = user.permissions.includes(perm);
        return acc;
      },
      {} as Record<string, boolean>
    );
  }, [user, permissions]);
}

export function useHasAnyPermission(permissions: string[]): boolean {
  const { user } = useAuth();

  return useMemo(() => {
    if (!user) return false;
    return permissions.some((perm) => user.permissions.includes(perm));
  }, [user, permissions]);
}

export function useHasAllPermissions(permissions: string[]): boolean {
  const { user } = useAuth();

  return useMemo(() => {
    if (!user) return false;
    return permissions.every((perm) => user.permissions.includes(perm));
  }, [user, permissions]);
}
