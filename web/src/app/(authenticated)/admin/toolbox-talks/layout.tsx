"use client";

import { useEffect, useMemo } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useAuth, useHasAnyPermission } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const leafLabels: Record<string, string> = {
  drafts: "Drafts",
  new: "New Learning",
  parse: "Parse",
  quiz: "Quiz",
  settings: "Settings",
  translate: "Translate",
  validate: "Validate",
  publish: "Publish",
};

const adminToolboxTalksNavItems = [
  { href: "/admin/toolbox-talks", label: "Overview", exact: true },
  { href: "/admin/toolbox-talks/talks", label: "Learnings" },
  { href: "/admin/toolbox-talks/courses", label: "Courses" },
  { href: "/admin/toolbox-talks/schedules", label: "Schedules" },
  { href: "/admin/toolbox-talks/assignments", label: "Assignments" },
  { href: "/admin/toolbox-talks/reports", label: "Reports" },
  { href: "/admin/toolbox-talks/certificates", label: "Certificates" },
  { href: "/admin/toolbox-talks/qr-locations", label: "QR Locations", permissions: ["Learnings.Admin"] },
  { href: "/admin/toolbox-talks/pipeline", label: "Pipeline Audit" },
  { href: "/admin/toolbox-talks/settings", label: "Settings", permissions: ["Learnings.Admin"] },
];

// Permissions that grant access to admin learnings management
const learningsAdminPermissions = [
  "Learnings.View",
  "Learnings.Manage",
  "Learnings.Schedule",
  "Learnings.Admin",
];

export default function AdminToolboxTalksLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const { user } = useAuth();
  const hasAdminPermission = useHasAnyPermission(learningsAdminPermissions);

  const visibleNavItems = useMemo(
    () =>
      adminToolboxTalksNavItems.filter((item) => {
        if (!item.permissions) return true;
        if (user?.isSuperUser) return true;
        return user ? item.permissions.some(p => user.permissions.includes(p)) : false;
      }),
    [user]
  );

  useEffect(() => {
    if (!hasAdminPermission) {
      router.replace("/dashboard");
    }
  }, [hasAdminPermission, router]);

  if (!hasAdminPermission) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to manage Learnings.
        </div>
      </div>
    );
  }

  const isActive = (href: string, exact?: boolean) => {
    if (exact) {
      return pathname === href;
    }
    // The learning wizard lives at /admin/toolbox-talks/learnings/** but belongs
    // to the Learnings tab (which links to /admin/toolbox-talks/talks).
    if (href === '/admin/toolbox-talks/talks' && pathname.startsWith('/admin/toolbox-talks/learnings')) {
      return true;
    }
    return pathname.startsWith(href);
  };

  const leaf = (() => {
    const segments = pathname.split('/');
    const learningsIdx = segments.indexOf('learnings');
    if (learningsIdx < 0) return null;
    const next = segments[learningsIdx + 1];
    const candidate = next && UUID_REGEX.test(next)
      ? segments[learningsIdx + 2]
      : next;
    return candidate ? (leafLabels[candidate] ?? null) : null;
  })();

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Link href="/admin" className="hover:text-foreground">
          Administration
        </Link>
        <span>/</span>
        {leaf ? (
          <Link href="/admin/toolbox-talks/learnings/drafts" className="hover:text-foreground">
            Learnings
          </Link>
        ) : (
          <span className="text-foreground">Learnings</span>
        )}
        {leaf && (
          <>
            <span>/</span>
            <span className="text-foreground">{leaf}</span>
          </>
        )}
      </div>

      <nav className="border-b bg-background">
        <div className="flex h-10 items-center gap-4 overflow-x-auto sm:gap-6 scrollbar-hide">
          {visibleNavItems.map((item) => (
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
