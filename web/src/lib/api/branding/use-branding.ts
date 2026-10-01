import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/auth/use-auth";
import {
  deleteTenantLogo,
  getCurrentBranding,
  getTenantBranding,
  uploadTenantLogo,
} from "./branding";

export const BRANDING_KEY = ["branding"] as const;

export const brandingKeys = {
  current: (tenantId: string | null) => [...BRANDING_KEY, "current", tenantId] as const,
  tenant: (tenantId: string) => [...BRANDING_KEY, "tenant", tenantId] as const,
};

/**
 * The tenant the header is showing: the switcher's selection for a SuperUser (null = All Tenants),
 * the user's own tenant for everyone else. A SuperUser's user.tenantId is their home tenant, so it
 * must not be used for them.
 */
export function getEffectiveTenantId(
  user: { isSuperUser: boolean; tenantId: string } | null,
  activeTenantId: string | null
): string | null {
  if (!user) return null;
  return user.isSuperUser ? activeTenantId : user.tenantId;
}

const BRANDING_STALE_TIME = 10 * 60 * 1000;

export function useCurrentBranding() {
  const { user, activeTenantId } = useAuth();
  const tenantId = getEffectiveTenantId(user, activeTenantId);

  const query = useQuery({
    queryKey: brandingKeys.current(tenantId),
    queryFn: () => getCurrentBranding(),
    // No tenant in context (SuperUser on All Tenants): nothing to fetch, CertifiedIQ logo applies.
    enabled: tenantId !== null,
    staleTime: BRANDING_STALE_TIME,
  });

  return {
    logoUrl: tenantId === null ? null : (query.data?.logoUrl ?? null),
    tenantName: tenantId === null ? null : (query.data?.tenantName ?? null),
    isLoading: tenantId !== null && query.isLoading,
  };
}

/** Logo of a specific tenant (the one being edited), regardless of the active tenant. */
export function useTenantLogo(tenantId: string) {
  return useQuery({
    queryKey: brandingKeys.tenant(tenantId),
    queryFn: () => getTenantBranding(tenantId),
    enabled: !!tenantId,
    staleTime: BRANDING_STALE_TIME,
  });
}

export function useUploadTenantLogo(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => uploadTenantLogo(tenantId, file),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: BRANDING_KEY }),
  });
}

export function useDeleteTenantLogo(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => deleteTenantLogo(tenantId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: BRANDING_KEY }),
  });
}
