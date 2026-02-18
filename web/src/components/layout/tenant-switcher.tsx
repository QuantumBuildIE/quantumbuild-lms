"use client";

import { useAuth } from "@/lib/auth/use-auth";
import { useTenants } from "@/lib/api/admin/use-tenants";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Building2 } from "lucide-react";

const ALL_TENANTS_VALUE = "__all__";

export function TenantSwitcher() {
  const { user, activeTenantId, setActiveTenantId } = useAuth();
  const { data: tenantsData, isLoading } = useTenants({ pageSize: 100 });

  if (!user?.isSuperUser) return null;

  const tenants = tenantsData?.items ?? [];

  const handleChange = (value: string) => {
    if (value === ALL_TENANTS_VALUE) {
      setActiveTenantId(null);
    } else {
      setActiveTenantId(value);
    }
  };

  const activeTenantName = activeTenantId
    ? tenants.find((t) => t.id === activeTenantId)?.name
    : null;

  return (
    <div className="flex items-center gap-2">
      <Building2 className="h-4 w-4 text-muted-foreground shrink-0" />
      <Select
        value={activeTenantId ?? ALL_TENANTS_VALUE}
        onValueChange={handleChange}
      >
        <SelectTrigger size="sm" className="h-8 min-w-[160px] max-w-[240px] border-dashed">
          <SelectValue placeholder={isLoading ? "Loading..." : "Select tenant"}>
            {activeTenantName ?? "All Tenants"}
          </SelectValue>
        </SelectTrigger>
        <SelectContent position="popper" align="start">
          <SelectItem value={ALL_TENANTS_VALUE}>
            All Tenants
          </SelectItem>
          {tenants.length > 0 && <SelectSeparator />}
          {tenants.map((tenant) => (
            <SelectItem key={tenant.id} value={tenant.id}>
              {tenant.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
