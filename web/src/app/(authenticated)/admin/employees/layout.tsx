"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useHasAnyPermission } from "@/lib/auth/use-auth";

export default function EmployeesLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const hasPermission = useHasAnyPermission([
    "Core.ManageEmployees",
    "Core.ManageUsers",
  ]);

  useEffect(() => {
    if (!hasPermission) {
      router.replace("/admin/toolbox-talks");
    }
  }, [hasPermission, router]);

  if (!hasPermission) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to manage Employees.
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
