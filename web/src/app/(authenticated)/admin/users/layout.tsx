"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useHasAnyPermission } from "@/lib/auth/use-auth";

export default function UsersLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const hasPermission = useHasAnyPermission(["Core.ManageUsers"]);

  useEffect(() => {
    if (!hasPermission) {
      router.replace("/admin");
    }
  }, [hasPermission, router]);

  if (!hasPermission) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to manage Users.
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
