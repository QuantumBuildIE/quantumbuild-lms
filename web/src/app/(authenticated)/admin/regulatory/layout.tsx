"use client";

import { useEffect, useMemo } from "react";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { usePermission, useIsSuperUser } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

const regulatoryNavItems = [
  { href: "/admin/regulatory/regulations", label: "Regulations", superUserOnly: false },
  { href: "/admin/regulatory/compliance", label: "Compliance", superUserOnly: false },
  { href: "/admin/regulatory/mappings", label: "Mappings", superUserOnly: false },
  { href: "/admin/regulatory/my-sectors", label: "My Sectors", superUserOnly: false },
  { href: "/admin/regulatory/system", label: "System Administration", superUserOnly: true },
];

export default function RegulatoryLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const hasLearningsAdmin = usePermission("Learnings.Admin");
  const isSuperUser = useIsSuperUser();

  useEffect(() => {
    if (!hasLearningsAdmin && !isSuperUser) {
      router.replace("/dashboard");
    }
  }, [hasLearningsAdmin, isSuperUser, router]);

  const visibleNavItems = useMemo(
    () =>
      regulatoryNavItems.filter((item) => {
        if (item.superUserOnly) return isSuperUser;
        return true;
      }),
    [isSuperUser]
  );

  if (!hasLearningsAdmin && !isSuperUser) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to access Regulatory.
        </div>
      </div>
    );
  }

  const isActive = (href: string) => pathname.startsWith(href);

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Link href="/admin" className="hover:text-foreground">
          Administration
        </Link>
        <span>/</span>
        <span className="text-foreground">Regulatory</span>
      </div>

      <nav className="border-b bg-background">
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
