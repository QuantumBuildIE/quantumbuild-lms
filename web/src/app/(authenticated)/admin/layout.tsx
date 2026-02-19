"use client";

import { useEffect, useMemo } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useAuth, useHasAnyPermission, useIsSuperUser } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

const adminNavItems = [
  { href: "/admin/employees", label: "Employees", superUserOnly: false, tenantScoped: true, permissions: ["Core.ManageEmployees"] },
  { href: "/admin/users", label: "Users", superUserOnly: false, tenantScoped: true, permissions: ["Core.ManageUsers"] },
  { href: "/admin/toolbox-talks", label: "Learnings", superUserOnly: false, tenantScoped: true, permissions: ["Learnings.View", "Learnings.Manage", "Learnings.Schedule", "Learnings.Admin"] },
  { href: "/admin/settings", label: "Settings", superUserOnly: false, tenantScoped: true, permissions: ["Learnings.Admin", "Core.ManageUsers"] },
  { href: "/admin/tenants", label: "Tenant Management", superUserOnly: true, tenantScoped: false },
];

const corePermissions = [
  "Core.ManageEmployees",
  "Core.ManageUsers",
  "Learnings.View",
  "Learnings.Manage",
  "Learnings.Schedule",
  "Learnings.Admin",
];

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const hasCorePermission = useHasAnyPermission(corePermissions);
  const isSuperUser = useIsSuperUser();
  const { user, activeTenantId } = useAuth();

  useEffect(() => {
    if (!hasCorePermission) {
      router.replace("/dashboard");
    }
  }, [hasCorePermission, router]);

  // Redirect SU to /admin/tenants if navigating to a tenant-scoped page without a tenant selected
  useEffect(() => {
    if (isSuperUser && !activeTenantId && !pathname.startsWith("/admin/tenants")) {
      router.replace("/admin/tenants");
    }
  }, [isSuperUser, activeTenantId, pathname, router]);

  const visibleNavItems = useMemo(
    () =>
      adminNavItems.filter((item) => {
        // Hide superUserOnly tabs from non-SU
        if (item.superUserOnly && !isSuperUser) return false;
        // Hide tenant-scoped tabs when SU has no tenant selected
        if (item.tenantScoped && isSuperUser && !activeTenantId) return false;
        // Hide tabs that require permissions the user doesn't have
        if (item.permissions && !isSuperUser) {
          if (!user || !item.permissions.some(p => user.permissions.includes(p))) return false;
        }
        return true;
      }),
    [isSuperUser, activeTenantId, user]
  );

  if (!hasCorePermission) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to access Administration.
        </div>
      </div>
    );
  }

  const isActive = (href: string) => {
    return pathname.startsWith(href);
  };

  return (
    <div className="space-y-6">
      <nav className="border-b bg-background -mx-4 px-4 sm:mx-0 sm:px-6">
        <div className="flex h-10 items-center gap-4 overflow-x-auto sm:gap-6 scrollbar-hide">
          {visibleNavItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "relative flex h-10 min-h-[44px] items-center px-1 text-sm font-medium transition-colors hover:text-foreground whitespace-nowrap sm:min-h-0 sm:px-0",
                isActive(item.href)
                  ? "text-foreground"
                  : "text-muted-foreground"
              )}
            >
              {item.label}
              {isActive(item.href) && (
                <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-primary" />
              )}
            </Link>
          ))}
        </div>
      </nav>
      <div>{children}</div>
    </div>
  );
}
