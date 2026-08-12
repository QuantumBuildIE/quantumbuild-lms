"use client";

import { DepartmentManagementSection } from "@/components/admin/department-management-section";

export default function AdminSettingsDepartmentsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Departments</h1>
        <p className="text-muted-foreground">
          Create, edit, and deactivate departments for your organization.
        </p>
      </div>

      <DepartmentManagementSection />
    </div>
  );
}
