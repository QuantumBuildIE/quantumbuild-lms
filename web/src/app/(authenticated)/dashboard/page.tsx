"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { BookOpen, FileSearch, type LucideIcon } from "lucide-react";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/lib/auth/use-auth";
import { MODULE_CONFIG, type ModuleName } from "@/lib/modules";

const ICON_MAP: Record<string, LucideIcon> = {
  BookOpen,
  FileSearch,
};

export default function DashboardPage() {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  const modules = user?.enabledModules ?? [];

  useEffect(() => {
    if (isLoading || !user) return;

    if (user.isSuperUser) {
      router.replace("/admin/tenants");
      return;
    }

    if (modules.length === 1) {
      const config = MODULE_CONFIG[modules[0] as ModuleName];
      if (config) {
        router.replace(config.href);
      }
    }
  }, [isLoading, user, modules, router]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <div className="animate-pulse text-muted-foreground">Loading...</div>
      </div>
    );
  }

  // Single module or superuser — redirect handled by useEffect
  if (modules.length <= 1 || user?.isSuperUser) {
    return null;
  }

  if (modules.length === 0) {
    return (
      <div className="flex items-center justify-center py-24">
        <div className="text-center space-y-2">
          <h2 className="text-xl font-semibold">No Modules Assigned</h2>
          <p className="text-muted-foreground">
            Your account does not have any modules enabled. Please contact your
            administrator.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center py-12 px-4">
      <div className="max-w-3xl w-full space-y-8">
        <div className="text-center space-y-2">
          <h1 className="text-3xl font-bold tracking-tight">
            Welcome, {user?.firstName}
          </h1>
          <p className="text-muted-foreground">
            Select a module to get started
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          {modules.map((moduleName) => {
            const config = MODULE_CONFIG[moduleName as ModuleName];
            if (!config) return null;

            const Icon = ICON_MAP[config.icon];

            return (
              <Link key={moduleName} href={config.href}>
                <Card className="transition-colors hover:border-primary hover:shadow-md cursor-pointer h-full">
                  <CardHeader className="flex flex-row items-center gap-4">
                    {Icon && (
                      <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                        <Icon className="h-6 w-6 text-primary" />
                      </div>
                    )}
                    <div className="space-y-1">
                      <CardTitle className="text-lg">{config.label}</CardTitle>
                      <CardDescription>{config.description}</CardDescription>
                    </div>
                  </CardHeader>
                </Card>
              </Link>
            );
          })}
        </div>
      </div>
    </div>
  );
}
