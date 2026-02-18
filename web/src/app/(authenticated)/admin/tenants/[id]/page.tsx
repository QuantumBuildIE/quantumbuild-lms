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
import { useTenant, useUpdateTenantStatus } from "@/lib/api/admin/use-tenants";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import type { TenantStatus } from "@/types/admin";
import { ChevronLeft } from "lucide-react";
import { toast } from "sonner";
import { format } from "date-fns";

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

      {/* Status Actions */}
      <Card className="max-w-2xl">
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

      {/* Edit Form */}
      <Card className="max-w-2xl">
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
  );
}
