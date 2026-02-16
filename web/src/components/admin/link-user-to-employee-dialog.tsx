"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useLinkUserToEmployee } from "@/lib/api/admin/use-users";
import { useUnlinkedEmployees } from "@/lib/api/admin/use-employees";
import { toast } from "sonner";
import type { User } from "@/lib/api/admin/users";
import { cn } from "@/lib/utils";

interface LinkUserToEmployeeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  user: User;
}

export function LinkUserToEmployeeDialog({
  open,
  onOpenChange,
  user,
}: LinkUserToEmployeeDialogProps) {
  const [search, setSearch] = React.useState("");
  const [selectedEmployeeId, setSelectedEmployeeId] = React.useState<string | null>(null);

  const linkUserToEmployee = useLinkUserToEmployee();
  const { data: employees, isLoading: loadingEmployees } = useUnlinkedEmployees();

  // Filter employees based on search
  const filteredEmployees = React.useMemo(() => {
    if (!employees) return [];
    if (!search.trim()) return employees;

    const searchLower = search.toLowerCase();
    return employees.filter(
      (emp) =>
        emp.fullName.toLowerCase().includes(searchLower) ||
        emp.email?.toLowerCase().includes(searchLower) ||
        emp.employeeCode.toLowerCase().includes(searchLower)
    );
  }, [employees, search]);

  // Reset form when dialog closes
  React.useEffect(() => {
    if (!open) {
      setSearch("");
      setSelectedEmployeeId(null);
    }
  }, [open]);

  async function handleLink() {
    if (!selectedEmployeeId) {
      toast.error("Please select an employee");
      return;
    }

    try {
      await linkUserToEmployee.mutateAsync({
        userId: user.id,
        data: { employeeId: selectedEmployeeId },
      });
      toast.success("Employee linked successfully", {
        description: `${user.fullName} has been linked to the selected employee record`,
      });
      onOpenChange(false);
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error("Failed to link employee", {
        description: message,
      });
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Link to Existing Employee</DialogTitle>
          <DialogDescription>
            Link {user.fullName} to an existing employee record that doesn&apos;t have a user account.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* User Info */}
          <div className="rounded-lg border p-4 bg-muted/50">
            <div className="grid grid-cols-2 gap-2 text-sm">
              <div className="text-muted-foreground">User:</div>
              <div className="font-medium">{user.fullName}</div>
              <div className="text-muted-foreground">Email:</div>
              <div className="font-medium">{user.email}</div>
              <div className="text-muted-foreground">Roles:</div>
              <div className="font-medium">
                {user.roles.map((r) => r.name).join(", ") || "None"}
              </div>
            </div>
          </div>

          {/* Employee Selection */}
          <div className="space-y-2">
            <Label>Select Employee:</Label>
            <div className="relative">
              <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search employees..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="pl-9"
              />
            </div>

            <div className="h-[240px] overflow-y-auto rounded-md border">
              {loadingEmployees ? (
                <div className="flex items-center justify-center h-full">
                  <LoadingSpinner className="h-6 w-6" />
                </div>
              ) : filteredEmployees.length === 0 ? (
                <div className="flex items-center justify-center h-full text-muted-foreground text-center p-4">
                  {search ? "No employees found" : "No unlinked employees available"}
                </div>
              ) : (
                <div className="p-2 space-y-1">
                  {filteredEmployees.map((employee) => (
                    <div
                      key={employee.id}
                      className={cn(
                        "flex items-center space-x-3 rounded-lg p-3 cursor-pointer transition-colors",
                        selectedEmployeeId === employee.id
                          ? "bg-primary/10 border border-primary"
                          : "hover:bg-muted/50"
                      )}
                      onClick={() => setSelectedEmployeeId(employee.id)}
                    >
                      <div
                        className={cn(
                          "h-4 w-4 rounded-full border-2 flex items-center justify-center",
                          selectedEmployeeId === employee.id
                            ? "border-primary"
                            : "border-muted-foreground"
                        )}
                      >
                        {selectedEmployeeId === employee.id && (
                          <div className="h-2 w-2 rounded-full bg-primary" />
                        )}
                      </div>
                      <div className="flex-1">
                        <div className="font-medium">{employee.fullName}</div>
                        <div className="text-sm text-muted-foreground">
                          {employee.employeeCode}
                          {employee.email && ` - ${employee.email}`}
                        </div>
                        {employee.jobTitle && (
                          <div className="text-xs text-muted-foreground">
                            {employee.jobTitle}
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        <DialogFooter className="pt-4">
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={linkUserToEmployee.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleLink}
            disabled={!selectedEmployeeId || linkUserToEmployee.isPending}
          >
            {linkUserToEmployee.isPending ? (
              <>
                <LoadingSpinner className="mr-2 h-4 w-4" />
                Linking...
              </>
            ) : (
              "Link Employee"
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function SearchIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      fill="none"
      stroke="currentColor"
      viewBox="0 0 24 24"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth={2}
        d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
      />
    </svg>
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
