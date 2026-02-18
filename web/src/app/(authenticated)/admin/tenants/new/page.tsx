"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { TenantForm } from "@/components/admin/tenant-form";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import { ChevronLeft } from "lucide-react";

export default function NewTenantPage() {
  const router = useRouter();
  const isSuperUser = useIsSuperUser();

  const handleSuccess = () => {
    router.push("/admin/tenants");
  };

  const handleCancel = () => {
    router.back();
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
            Add Tenant
          </h1>
          <p className="text-muted-foreground">Create a new tenant organization</p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Tenant Details</CardTitle>
          <CardDescription>
            Enter the details for the new tenant. If you provide a contact name
            and email, an admin account will be created automatically.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <TenantForm onSuccess={handleSuccess} onCancel={handleCancel} />
        </CardContent>
      </Card>
    </div>
  );
}
