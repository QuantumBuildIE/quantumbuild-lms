"use client";

import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { EmployeeForm } from "@/components/admin/employee-form";
import { useEmployee } from "@/lib/api/admin/use-employees";
import { ChevronLeft } from "lucide-react";

export default function EditEmployeePage() {
  const params = useParams();
  const router = useRouter();
  const employeeId = params.id as string;

  const { data: employee, isLoading, error } = useEmployee(employeeId);

  const handleSuccess = () => {
    router.push(`/admin/employees/${employeeId}`);
  };

  const handleCancel = () => {
    router.push(`/admin/employees/${employeeId}`);
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href={`/admin/employees/${employeeId}`}>
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Edit Employee</h1>
            <p className="text-muted-foreground">Loading employee details...</p>
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
            <h1 className="text-2xl font-semibold tracking-tight">Edit Employee</h1>
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
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href={`/admin/employees/${employeeId}`}>
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Edit Employee</h1>
          <p className="text-muted-foreground">
            Editing: {employee.firstName} {employee.lastName} ({employee.employeeCode})
          </p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Employee Details</CardTitle>
          <CardDescription>
            Update the employee information
          </CardDescription>
        </CardHeader>
        <CardContent>
          <EmployeeForm employee={employee} onSuccess={handleSuccess} onCancel={handleCancel} />
        </CardContent>
      </Card>
    </div>
  );
}
