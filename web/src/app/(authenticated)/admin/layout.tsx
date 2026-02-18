"use client";

import { useEffect, useMemo } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useHasAnyPermission, useIsSuperUser } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

const adminNavItems = [
  { href: "/admin/employees", label: "Employees", superUserOnly: false },
  { href: "/admin/users", label: "Users", superUserOnly: false },
  { href: "/admin/toolbox-talks", label: "Learnings", superUserOnly: false },
  { href: "/admin/tenants", label: "Tenant Management", superUserOnly: true },
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

  useEffect(() => {
    if (!hasCorePermission) {
      router.replace("/dashboard");
    }
  }, [hasCorePermission, router]);

  const visibleNavItems = useMemo(
    () => adminNavItems.filter((item) => !item.superUserOnly || isSuperUser),
    [isSuperUser]
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
