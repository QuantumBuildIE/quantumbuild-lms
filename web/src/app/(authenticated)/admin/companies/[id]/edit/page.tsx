"use client";

import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { CompanyForm } from "@/components/admin/company-form";
import { useCompany } from "@/lib/api/admin/use-companies";
import { ChevronLeft } from "lucide-react";

export default function EditCompanyPage() {
  const params = useParams();
  const router = useRouter();
  const companyId = params.id as string;

  const { data: company, isLoading, error } = useCompany(companyId);

  const handleSuccess = () => {
    router.push(`/admin/companies/${companyId}`);
  };

  const handleCancel = () => {
    router.back();
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/companies">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Edit Company</h1>
            <p className="text-muted-foreground">Loading company details...</p>
          </div>
        </div>
        <div className="max-w-3xl">
          <div className="rounded-lg border bg-card p-8">
            <div className="flex items-center justify-center">
              <div className="animate-pulse text-muted-foreground">
                Loading...
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (error || !company) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/companies">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Edit Company</h1>
            <p className="text-muted-foreground">Company not found</p>
          </div>
        </div>
        <div className="max-w-3xl">
          <div className="rounded-lg border bg-card p-8">
            <div className="text-center">
              <p className="text-destructive">
                Failed to load company. The company may have been deleted.
              </p>
              <Button className="mt-4" asChild>
                <Link href="/admin/companies">Back to Companies</Link>
              </Button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/admin/companies">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Edit Company</h1>
          <p className="text-muted-foreground">
            Editing: {company.companyName} ({company.companyCode})
          </p>
        </div>
      </div>

      <div className="max-w-3xl">
        <CompanyForm company={company} onSuccess={handleSuccess} onCancel={handleCancel} />
      </div>
    </div>
  );
}
