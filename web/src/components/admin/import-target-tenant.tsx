"use client";

import { Building2 } from "lucide-react";
import { useAuth } from "@/lib/auth/use-auth";
import { useTenants } from "@/lib/api/admin/use-tenants";

/**
 * True when a SuperUser has no active tenant selected, so a tenant-scoped
 * import cannot be targeted. Tenant users are never blocked.
 */
export function useImportTenantMissing(): boolean {
  const { user, activeTenantId } = useAuth();
  return (user?.isSuperUser ?? false) && !activeTenantId;
}

/**
 * Shows which tenant a bulk import will apply to. Imports always use the
 * active tenant (the header switcher), via the apiClient interceptor.
 * Rendered for SuperUsers only; renders nothing for tenant users.
 */
export function ImportTargetTenant() {
  const { user, activeTenantId } = useAuth();
  const isSuperUser = user?.isSuperUser ?? false;

  if (!isSuperUser) return null;

  if (!activeTenantId) {
    return (
      <div
        role="alert"
        className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900"
      >
        Select a tenant using the tenant switcher in the header before importing.
      </div>
    );
  }

  return <ActiveTenantBanner tenantId={activeTenantId} />;
}

// Isolated so useTenants only runs for a SuperUser with an active tenant.
function ActiveTenantBanner({ tenantId }: { tenantId: string }) {
  const { data } = useTenants({ pageNumber: 1, pageSize: 200 });
  const name = data?.items.find((t) => t.id === tenantId)?.name;

  return (
    <div className="flex items-center gap-2 rounded-md border bg-muted/50 p-3">
      <Building2 className="h-4 w-4 shrink-0 text-muted-foreground" />
      <p className="text-sm">
        Importing into:{" "}
        <span className="font-semibold">{name ?? "Loading tenant..."}</span>
      </p>
    </div>
  );
}
