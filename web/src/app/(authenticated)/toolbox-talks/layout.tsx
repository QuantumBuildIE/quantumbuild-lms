"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import Link from "next/link";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

// Employee-only navigation - simplified view for completing assigned talks
const myTalksNavItems = [
  { href: "/toolbox-talks", label: "My Learnings", exact: true },
  { href: "/toolbox-talks/certificates", label: "My Certificates" },
];

export default function ToolboxTalksLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const router = useRouter();
  const isSuperUser = useIsSuperUser();

  // SuperUser is a platform role, not a learner — redirect to admin
  useEffect(() => {
    if (isSuperUser) {
      router.replace("/admin/tenants");
    }
  }, [isSuperUser, router]);

  const isActive = (href: string, exact?: boolean) => {
    if (exact) {
      return pathname === href;
    }
    return pathname.startsWith(href);
  };

  return (
    <div className="space-y-6">
      <nav className="border-b bg-background -mx-4 px-4 sm:mx-0 sm:px-6">
        <div className="flex h-10 items-center gap-4 overflow-x-auto sm:gap-6 scrollbar-hide">
          {myTalksNavItems.map((item) => (
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
