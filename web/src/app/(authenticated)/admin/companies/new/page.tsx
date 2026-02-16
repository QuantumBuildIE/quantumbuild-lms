"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { CompanyForm } from "@/components/admin/company-form";
import { ChevronLeft } from "lucide-react";

export default function NewCompanyPage() {
  const router = useRouter();

  const handleSuccess = () => {
    router.push("/admin/companies");
  };

  const handleCancel = () => {
    router.back();
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/admin/companies">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Add Company</h1>
          <p className="text-muted-foreground">Create a new company record</p>
        </div>
      </div>

      <div className="max-w-3xl">
        <CompanyForm onSuccess={handleSuccess} onCancel={handleCancel} />
      </div>
    </div>
  );
}
