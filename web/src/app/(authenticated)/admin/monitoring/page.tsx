"use client";

import Link from "next/link";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Users } from "lucide-react";

export default function MonitoringPage() {
  const isSuperUser = useIsSuperUser();

  if (!isSuperUser) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to access Monitoring.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold tracking-tight">Monitoring</h2>
        <p className="text-sm text-muted-foreground">
          Platform health and customer activity tools
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Link href="/admin/monitoring/customer-usage">
          <Card className="cursor-pointer hover:bg-muted/50 transition-colors">
            <CardHeader className="pb-2">
              <div className="flex items-center gap-2">
                <Users className="h-5 w-5 text-muted-foreground" />
                <CardTitle className="text-base">Customer Usage Report</CardTitle>
              </div>
            </CardHeader>
            <CardContent>
              <CardDescription>
                Per-tenant activity summary: employees, learnings, completions, and at-risk accounts.
              </CardDescription>
            </CardContent>
          </Card>
        </Link>
      </div>
    </div>
  );
}
