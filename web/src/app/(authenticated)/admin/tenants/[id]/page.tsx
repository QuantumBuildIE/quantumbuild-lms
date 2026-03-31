"use client";

import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { TenantForm } from "@/components/admin/tenant-form";
import { TenantModulesCard } from "@/components/admin/tenant-modules-card";
import { TenantSectorsCard } from "@/components/admin/tenant-sectors-card";
import { TypeToConfirmDialog } from "@/components/shared/type-to-confirm-dialog";
import { useTenant, useUpdateTenantStatus, useResetTenantData } from "@/lib/api/admin/use-tenants";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import type { TenantStatus } from "@/types/admin";
import { ChevronLeft } from "lucide-react";
import { toast } from "sonner";
import { format } from "date-fns";
import { useState } from "react";

const statusVariant: Record<TenantStatus, "default" | "secondary" | "destructive"> = {
  Active: "default",
  Suspended: "destructive",
  Inactive: "secondary",
};

function getStatusLabel(status: TenantStatus | number): TenantStatus {
  if (typeof status === "number") {
    switch (status) {
      case 1: return "Active";
      case 2: return "Suspended";
      case 3: return "Inactive";
      default: return "Active";
    }
  }
  return status;
}

// Backend expects numeric enum values
function getStatusValue(status: TenantStatus): number {
  switch (status) {
    case "Active": return 1;
    case "Suspended": return 2;
    case "Inactive": return 3;
  }
}

export default function TenantDetailPage() {
  const params = useParams();
  const router = useRouter();
  const tenantId = params.id as string;
  const isSuperUser = useIsSuperUser();

  const { data: tenant, isLoading, error } = useTenant(tenantId);
  const updateStatus = useUpdateTenantStatus();
  const resetData = useResetTenantData();
  const [showResetDialog, setShowResetDialog] = useState(false);

  const handleSuccess = () => {
    router.push("/admin/tenants");
  };

  const handleCancel = () => {
    router.back();
  };

  const handleStatusChange = async (newStatus: TenantStatus) => {
    try {
      await updateStatus.mutateAsync({
        id: tenantId,
        // Send numeric value that backend expects
        data: { status: getStatusValue(newStatus) as unknown as TenantStatus },
      });
      toast.success(`Tenant ${newStatus === "Active" ? "activated" : newStatus === "Suspended" ? "suspended" : "deactivated"} successfully`);
    } catch {
      toast.error("Failed to update tenant status");
    }
  };

  if (!isSuperUser) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to access Tenant Management.
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/tenants">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              Tenant Details
            </h1>
            <p className="text-muted-foreground">Loading tenant details...</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="flex items-center justify-center">
              <div className="animate-pulse text-muted-foreground">
                Loading...
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !tenant) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/tenants">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              Tenant Details
            </h1>
            <p className="text-muted-foreground">Tenant not found</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="text-center">
              <p className="text-destructive">
                Failed to load tenant. The tenant may have been deleted.
              </p>
              <Button className="mt-4" asChild>
                <Link href="/admin/tenants">Back to Tenants</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const currentStatus = getStatusLabel(tenant.status);

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/admin/tenants">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div className="flex-1">
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight">
              {tenant.name}
            </h1>
            <Badge variant={statusVariant[currentStatus]}>
              {currentStatus}
            </Badge>
          </div>
          <p className="text-muted-foreground">
            Created {format(new Date(tenant.createdAt), "dd MMM yyyy")}
            {tenant.createdBy && ` by ${tenant.createdBy}`}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Left column: Status + Modules */}
        <div className="space-y-6">
          {/* Status Actions */}
          <Card>
            <CardHeader>
              <CardTitle>Status</CardTitle>
              <CardDescription>
                Activate or suspend this tenant. Suspended tenants cannot log in.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-3">
                {currentStatus !== "Active" && (
                  <Button
                    variant="default"
                    onClick={() => handleStatusChange("Active")}
                    disabled={updateStatus.isPending}
                  >
                    Activate
                  </Button>
                )}
                {currentStatus !== "Suspended" && (
                  <AlertDialog>
                    <AlertDialogTrigger asChild>
                      <Button
                        variant="destructive"
                        disabled={updateStatus.isPending}
                      >
                        Suspend
                      </Button>
                    </AlertDialogTrigger>
                    <AlertDialogContent>
                      <AlertDialogHeader>
                        <AlertDialogTitle>Suspend Tenant?</AlertDialogTitle>
                        <AlertDialogDescription>
                          Suspending this tenant will prevent all their users from
                          logging in. This can be reversed by activating the tenant
                          again.
                        </AlertDialogDescription>
                      </AlertDialogHeader>
                      <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                          onClick={() => handleStatusChange("Suspended")}
                        >
                          Suspend
                        </AlertDialogAction>
                      </AlertDialogFooter>
                    </AlertDialogContent>
                  </AlertDialog>
                )}
              </div>
            </CardContent>
          </Card>

          {/* Modules */}
          <TenantModulesCard tenantId={tenantId} />

          {/* Sectors */}
          <TenantSectorsCard tenantId={tenantId} />
        </div>

        {/* Right column: Edit Form */}
        <Card>
          <CardHeader>
            <CardTitle>Tenant Details</CardTitle>
            <CardDescription>Update tenant information</CardDescription>
          </CardHeader>
          <CardContent>
            <TenantForm
              tenant={tenant}
              onSuccess={handleSuccess}
              onCancel={handleCancel}
            />
          </CardContent>
        </Card>
      </div>

      {/* Danger Zone */}
      <Card className="border-destructive">
        <CardHeader>
          <CardTitle className="text-destructive">Danger Zone</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between">
            <div>
              <p className="font-medium">Reset Training Data</p>
              <p className="text-sm text-muted-foreground">
                Permanently deletes all talks, courses, schedules, assignments,
                completions, certificates and AI usage logs for this tenant.
                This cannot be undone.
              </p>
            </div>
            <Button
              variant="destructive"
              onClick={() => setShowResetDialog(true)}
            >
              Reset Training Data
            </Button>
          </div>
        </CardContent>
      </Card>

      <TypeToConfirmDialog
        open={showResetDialog}
        onOpenChange={setShowResetDialog}
        title="Reset Tenant Training Data"
        description={
          <span>
            This will permanently delete <strong>all training data</strong> for{" "}
            <strong>{tenant.name}</strong>, including talks, courses, schedules,
            assignments, completions, certificates, validation runs, AI usage
            logs, and all associated files in R2 storage. This action cannot be
            undone.
          </span>
        }
        confirmPhrase={tenant.name}
        confirmLabel="Reset Training Data"
        destructiveMessage="This is a destructive operation that cannot be reversed."
        isLoading={resetData.isPending}
        onConfirm={() => {
          resetData.mutate(tenantId, {
            onSuccess: () => {
              toast.success("Training data reset successfully");
              setShowResetDialog(false);
            },
            onError: (error) => {
              toast.error(
                error instanceof Error
                  ? error.message
                  : "Failed to reset training data"
              );
            },
          });
        }}
      />
    </div>
  );
}
