"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { usePermission } from "@/lib/auth/use-auth";

export default function AdminSettingsLocationsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const hasPermission = usePermission("Core.ManageSites");

  useEffect(() => {
    if (!hasPermission) {
      router.replace("/admin/settings");
    }
  }, [hasPermission, router]);

  if (!hasPermission) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to manage Locations.
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
