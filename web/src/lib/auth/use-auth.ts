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

export function useIsSuperUser(): boolean {
  const { user } = useAuth();
  return user?.isSuperUser ?? false;
}

export function usePermission(permission: string): boolean {
  const { user } = useAuth();

  return useMemo(() => {
    if (!user) return false;
    if (user.isSuperUser) return true;
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
        acc[perm] = user.isSuperUser || user.permissions.includes(perm);
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
    if (user.isSuperUser) return true;
    return permissions.some((perm) => user.permissions.includes(perm));
  }, [user, permissions]);
}

export function useHasAllPermissions(permissions: string[]): boolean {
  const { user } = useAuth();

  return useMemo(() => {
    if (!user) return false;
    if (user.isSuperUser) return true;
    return permissions.every((perm) => user.permissions.includes(perm));
  }, [user, permissions]);
}
