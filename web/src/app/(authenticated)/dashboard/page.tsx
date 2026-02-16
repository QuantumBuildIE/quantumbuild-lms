"use client";

import { useMemo } from "react";
import Link from "next/link";
import { useAuth } from "@/lib/auth/use-auth";
import { availableModules } from "@/types/modules";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ArrowRight } from "lucide-react";

const corePermissions = [
  "Core.ManageSites",
  "Core.ManageEmployees",
  "Core.ManageCompanies",
  "Core.ManageUsers",
  "Core.Admin",
];

export default function DashboardPage() {
  const { user } = useAuth();

  const accessibleModules = useMemo(() => {
    if (!user) return [];
    return availableModules.filter((module) => {
      // Special handling for admin module - check for any Core permission
      if (module.id === "admin") {
        return corePermissions.some((perm) => user.permissions.includes(perm));
      }
      return user.permissions.includes(module.requiredPermission);
    });
  }, [user]);

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">
          Welcome back, {user?.firstName}!
        </h1>
        <p className="text-muted-foreground mt-1">
          Select a module to get started
        </p>
      </div>

      {accessibleModules.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center">
            <p className="text-muted-foreground">
              You don&apos;t have access to any modules yet. Please contact your administrator.
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {accessibleModules.map((module) => {
            const Icon = module.icon;
            return (
              <Link key={module.id} href={module.path} className="group">
                <Card className="h-full transition-all duration-200 hover:shadow-lg hover:border-primary/50 group-hover:-translate-y-1">
                  <CardHeader className="pb-3">
                    <div className="flex items-center gap-3">
                      <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10 text-primary group-hover:bg-primary group-hover:text-primary-foreground transition-colors">
                        <Icon className="h-6 w-6" />
                      </div>
                      <CardTitle className="text-lg">{module.name}</CardTitle>
                    </div>
                  </CardHeader>
                  <CardContent className="pt-0">
                    <CardDescription className="text-sm mb-4">
                      {module.description}
                    </CardDescription>
                    <Button
                      variant="ghost"
                      className="p-0 h-auto font-medium text-primary hover:text-primary/80 hover:bg-transparent"
                    >
                      Open module
                      <ArrowRight className="ml-2 h-4 w-4 transition-transform group-hover:translate-x-1" />
                    </Button>
                  </CardContent>
                </Card>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
