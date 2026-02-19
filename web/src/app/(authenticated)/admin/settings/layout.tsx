"use client";

import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useHasAnyPermission } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

const settingsNavItems = [
  { href: "/admin/settings", label: "General", exact: true },
  { href: "/admin/settings/languages", label: "Languages" },
  { href: "/admin/settings/lookups", label: "Lookups" },
];

const settingsPermissions = [
  "Learnings.Admin",
  "Core.ManageUsers",
];

export default function AdminSettingsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const hasPermission = useHasAnyPermission(settingsPermissions);

  useEffect(() => {
    if (!hasPermission) {
      router.replace("/dashboard");
    }
  }, [hasPermission, router]);

  if (!hasPermission) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to access Settings.
        </div>
      </div>
    );
  }

  const isActive = (href: string, exact?: boolean) => {
    if (exact) {
      return pathname === href;
    }
    return pathname.startsWith(href);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Link href="/admin" className="hover:text-foreground">
          Administration
        </Link>
        <span>/</span>
        <span className="text-foreground">Settings</span>
      </div>

      <nav className="border-b bg-background">
        <div className="flex h-10 items-center gap-4 overflow-x-auto sm:gap-6 scrollbar-hide">
          {settingsNavItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "relative flex h-10 min-h-[44px] items-center px-1 text-sm font-medium transition-colors hover:text-foreground whitespace-nowrap sm:min-h-0 sm:px-0",
                isActive(item.href, item.exact)
                  ? "text-foreground"
                  : "text-muted-foreground"
              )}
            >
              {item.label}
              {isActive(item.href, item.exact) && (
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
