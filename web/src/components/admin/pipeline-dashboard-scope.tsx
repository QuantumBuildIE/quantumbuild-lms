"use client";

import { Lock } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader } from "@/components/ui/card";
import { useCurrentBranding } from "@/lib/api/branding/use-branding";

/**
 * Names the tenant the dashboard is scoped to. The name comes from
 * GET /api/branding/current (via useCurrentBranding), which resolves the effective
 * tenant: the active tenant for a SuperUser, the user's own tenant for everyone else.
 */
export function DashboardTenantLabel() {
  const { tenantName, isLoading } = useCurrentBranding();
  if (!tenantName && !isLoading) return null;

  return (
    <p className="text-sm text-muted-foreground">
      Showing: <span className="font-semibold text-foreground">{tenantName ?? "Loading tenant..."}</span>
    </p>
  );
}

/** Small marker for dashboard figures that are not filtered by tenant. */
export function AllTenantsScope() {
  return <span className="text-[10px] font-medium uppercase text-muted-foreground">All tenants</span>;
}

export function GlossaryTermsCard({
  systemCount,
  overrideCount,
}: {
  systemCount: number;
  overrideCount: number;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardDescription className="flex items-center justify-between">
          Locked Terms
          <Lock className="h-5 w-5 text-blue-400" />
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-1">
        <p className="text-sm">
          System glossary (your sectors):{" "}
          <span className="text-lg font-semibold text-blue-600">{systemCount}</span>
        </p>
        <p className="text-sm">
          Your overrides (your sectors):{" "}
          <span className="text-lg font-semibold text-blue-600">{overrideCount}</span>
        </p>
        <p className="pt-1 text-xs text-muted-foreground">
          Overrides replace matching system terms. Each validation run uses one selected sector.
        </p>
      </CardContent>
    </Card>
  );
}
