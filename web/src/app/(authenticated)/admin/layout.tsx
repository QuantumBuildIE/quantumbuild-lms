"use client";

import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useHasAnyPermission } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

const adminNavItems = [
  { href: "/admin/employees", label: "Employees" },
  { href: "/admin/users", label: "Users" },
  { href: "/admin/toolbox-talks", label: "Toolbox Talks" },
];

const corePermissions = [
  "Core.ManageEmployees",
  "Core.ManageUsers",
  "Core.Admin",
  "ToolboxTalks.Admin",
  "ToolboxTalks.Create",
  "ToolboxTalks.Edit",
  "ToolboxTalks.Schedule",
];

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const hasCorePermission = useHasAnyPermission(corePermissions);

  useEffect(() => {
    if (!hasCorePermission) {
      router.replace("/dashboard");
    }
  }, [hasCorePermission, router]);

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
      <nav className="border-b bg-background -mx-4 px-4 sm:mx-0 sm:px-0">
        <div className="flex h-10 items-center gap-4 overflow-x-auto sm:gap-6 scrollbar-hide">
          {adminNavItems.map((item) => (
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
