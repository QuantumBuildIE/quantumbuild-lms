"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { EmployeeCertificatesSection } from "@/components/admin/employee-certificates-section";
import { AssignedOperatorsSection } from "@/components/admin/assigned-operators-section";
import { useEmployee } from "@/lib/api/admin/use-employees";
import { useUser } from "@/lib/api/admin/use-users";
import { useLookupValues } from "@/hooks/use-lookups";
import { ChevronLeft, Pencil } from "lucide-react";
import { format } from "date-fns";

export default function EmployeeDetailPage() {
  const params = useParams();
  const employeeId = params.id as string;

  const { data: employee, isLoading, error } = useEmployee(employeeId);
  const linkedUserId = employee?.linkedUserId ?? "";
  const { data: linkedUser } = useUser(linkedUserId);
  const isSupervisor = linkedUser?.roles?.some((r) => r.name === "Supervisor") ?? false;
  const { data: languages = [] } = useLookupValues("Language");

  const getLanguageName = (code: string) => {
    const lang = languages.find((l) => l.code === code);
    if (!lang) return code;
    const meta = lang.metadata ? JSON.parse(lang.metadata) : null;
    return meta?.nativeName ? `${lang.name} (${meta.nativeName})` : lang.name;
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/employees">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Employee</h1>
            <p className="text-muted-foreground">Loading employee details...</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="flex items-center justify-center">
              <div className="animate-pulse text-muted-foreground">Loading...</div>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !employee) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/employees">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Employee</h1>
            <p className="text-muted-foreground">Employee not found</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="text-center">
              <p className="text-destructive">
                Failed to load employee. The employee may have been deleted.
              </p>
              <Button className="mt-4" asChild>
                <Link href="/admin/employees">Back to Employees</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/employees">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {employee.firstName} {employee.lastName}
            </h1>
            <p className="text-muted-foreground">{employee.employeeCode}</p>
          </div>
          <Badge variant={employee.isActive ? "default" : "secondary"}>
            {employee.isActive ? "Active" : "Inactive"}
          </Badge>
        </div>
        <Button asChild>
          <Link href={`/admin/employees/${employeeId}/edit`}>
            <Pencil className="h-4 w-4 mr-2" />
            Edit
          </Link>
        </Button>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Employee Details</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-4">
            <DetailItem label="First Name" value={employee.firstName} />
            <DetailItem label="Last Name" value={employee.lastName} />
            <DetailItem label="Employee Code" value={employee.employeeCode} />
            <DetailItem label="Email" value={employee.email} />
            <DetailItem label="Phone" value={employee.phone} />
            <DetailItem label="Mobile" value={employee.mobile} />
            <DetailItem label="Job Title" value={employee.jobTitle} />
            <DetailItem label="Department" value={employee.department} />
            <DetailItem label="Default Site" value={employee.primarySiteName} />
            <DetailItem
              label="Preferred Language"
              value={employee.preferredLanguage ? getLanguageName(employee.preferredLanguage) : undefined}
            />
            <DetailItem
              label="Start Date"
              value={employee.startDate ? format(new Date(employee.startDate), "MMM d, yyyy") : undefined}
            />
            <DetailItem
              label="End Date"
              value={employee.endDate ? format(new Date(employee.endDate), "MMM d, yyyy") : undefined}
            />
            {employee.notes && (
              <div className="sm:col-span-2">
                <DetailItem label="Notes" value={employee.notes} />
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      <div className="max-w-2xl">
        <EmployeeCertificatesSection employeeId={employeeId} />
      </div>

      {isSupervisor && (
        <div className="max-w-2xl">
          <AssignedOperatorsSection employeeId={employeeId} />
        </div>
      )}
    </div>
  );
}

function DetailItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <dt className="text-sm font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-sm">
        {value || <span className="text-muted-foreground">-</span>}
      </dd>
    </div>
  );
}
