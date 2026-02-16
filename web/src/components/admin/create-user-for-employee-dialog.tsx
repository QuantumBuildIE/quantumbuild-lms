"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useCreateUserForEmployee } from "@/lib/api/admin/use-employees";
import { useRoles } from "@/lib/api/admin/use-roles";
import { toast } from "sonner";
import type { Employee } from "@/types/admin";
import { getApiErrorMessage } from "@/lib/utils";

interface CreateUserForEmployeeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  employee: Employee;
}

export function CreateUserForEmployeeDialog({
  open,
  onOpenChange,
  employee,
}: CreateUserForEmployeeDialogProps) {
  const [selectedRoleIds, setSelectedRoleIds] = React.useState<string[]>([]);

  const createUserForEmployee = useCreateUserForEmployee();
  const { data: roles = [], isLoading: loadingRoles } = useRoles();

  // Reset form when dialog closes
  React.useEffect(() => {
    if (!open) {
      setSelectedRoleIds([]);
    }
  }, [open]);

  function handleRoleToggle(roleId: string) {
    setSelectedRoleIds((prev) =>
      prev.includes(roleId)
        ? prev.filter((id) => id !== roleId)
        : [...prev, roleId]
    );
  }

  async function handleCreate() {
    if (selectedRoleIds.length === 0) {
      toast.error("Please select at least one role");
      return;
    }

    try {
      await createUserForEmployee.mutateAsync({
        employeeId: employee.id,
        data: { roleIds: selectedRoleIds },
      });
      toast.success("User account created successfully", {
        description: `A password setup email has been sent to ${employee.email}`,
      });
      onOpenChange(false);
    } catch (error) {
      toast.error("Failed to create user account", {
        description: getApiErrorMessage(error),
      });
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Create User Account</DialogTitle>
          <DialogDescription>
            Create a login account for {employee.fullName}. They will receive an email to set their password.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Employee Info */}
          <div className="rounded-lg border p-4 bg-muted/50">
            <div className="grid grid-cols-2 gap-2 text-sm">
              <div className="text-muted-foreground">Employee:</div>
              <div className="font-medium">{employee.fullName}</div>
              <div className="text-muted-foreground">Code:</div>
              <div className="font-medium">{employee.employeeCode}</div>
              <div className="text-muted-foreground">Email:</div>
              <div className="font-medium">{employee.email}</div>
            </div>
          </div>

          {/* Role Selection */}
          <div className="space-y-2">
            <Label>Select Roles:</Label>
            {loadingRoles ? (
              <div className="flex items-center justify-center py-4">
                <LoadingSpinner className="h-6 w-6" />
              </div>
            ) : (
              <div className="space-y-2 rounded-md border p-4">
                {roles.map((role) => (
                  <div key={role.id} className="flex items-center space-x-2">
                    <Checkbox
                      id={role.id}
                      checked={selectedRoleIds.includes(role.id)}
                      onCheckedChange={() => handleRoleToggle(role.id)}
                    />
                    <Label htmlFor={role.id} className="cursor-pointer">
                      {role.name}
                    </Label>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Info Banner */}
          <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
            <strong>Note:</strong> The user will receive an email at{" "}
            <span className="font-medium">{employee.email}</span> with instructions
            to set their password.
          </div>
        </div>

        <DialogFooter className="pt-4">
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={createUserForEmployee.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleCreate}
            disabled={selectedRoleIds.length === 0 || createUserForEmployee.isPending}
          >
            {createUserForEmployee.isPending ? (
              <>
                <LoadingSpinner className="mr-2 h-4 w-4" />
                Creating...
              </>
            ) : (
              "Create Account"
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function LoadingSpinner({ className }: { className?: string }) {
  return (
    <svg
      className={`animate-spin ${className}`}
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
}
