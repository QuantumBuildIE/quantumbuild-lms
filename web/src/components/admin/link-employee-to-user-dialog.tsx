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
import { useLinkEmployeeToUser } from "@/lib/api/admin/use-employees";
import { useUnlinkedUsers } from "@/lib/api/admin/use-users";
import { toast } from "sonner";
import type { Employee } from "@/types/admin";
import { cn, getApiErrorMessage } from "@/lib/utils";

interface LinkEmployeeToUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  employee: Employee;
}

export function LinkEmployeeToUserDialog({
  open,
  onOpenChange,
  employee,
}: LinkEmployeeToUserDialogProps) {
  const [search, setSearch] = React.useState("");
  const [selectedUserId, setSelectedUserId] = React.useState<string | null>(null);

  const linkEmployeeToUser = useLinkEmployeeToUser();
  const { data: users, isLoading: loadingUsers } = useUnlinkedUsers();

  // Filter users based on search
  const filteredUsers = React.useMemo(() => {
    if (!users) return [];
    if (!search.trim()) return users;

    const searchLower = search.toLowerCase();
    return users.filter(
      (user) =>
        user.fullName.toLowerCase().includes(searchLower) ||
        user.email.toLowerCase().includes(searchLower)
    );
  }, [users, search]);

  // Reset form when dialog closes
  React.useEffect(() => {
    if (!open) {
      setSearch("");
      setSelectedUserId(null);
    }
  }, [open]);

  async function handleLink() {
    if (!selectedUserId) {
      toast.error("Please select a user");
      return;
    }

    try {
      await linkEmployeeToUser.mutateAsync({
        employeeId: employee.id,
        data: { userId: selectedUserId },
      });
      toast.success("User linked successfully", {
        description: `${employee.fullName} has been linked to the selected user account`,
      });
      onOpenChange(false);
    } catch (error) {
      toast.error("Failed to link user", {
        description: getApiErrorMessage(error),
      });
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Link to Existing User</DialogTitle>
          <DialogDescription>
            Link {employee.fullName} to an existing user account that doesn&apos;t have an employee record.
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
              {employee.email && (
                <>
                  <div className="text-muted-foreground">Email:</div>
                  <div className="font-medium">{employee.email}</div>
                </>
              )}
            </div>
          </div>

          {/* User Selection */}
          <div className="space-y-2">
            <Label>Select User Account:</Label>
            <div className="relative">
              <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search users..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="pl-9"
              />
            </div>

            <div className="h-[240px] overflow-y-auto rounded-md border">
              {loadingUsers ? (
                <div className="flex items-center justify-center h-full">
                  <LoadingSpinner className="h-6 w-6" />
                </div>
              ) : filteredUsers.length === 0 ? (
                <div className="flex items-center justify-center h-full text-muted-foreground text-center p-4">
                  {search ? "No users found" : "No unlinked user accounts available"}
                </div>
              ) : (
                <div className="p-2 space-y-1">
                  {filteredUsers.map((user) => (
                    <div
                      key={user.id}
                      className={cn(
                        "flex items-center space-x-3 rounded-lg p-3 cursor-pointer transition-colors",
                        selectedUserId === user.id
                          ? "bg-primary/10 border border-primary"
                          : "hover:bg-muted/50"
                      )}
                      onClick={() => setSelectedUserId(user.id)}
                    >
                      <div
                        className={cn(
                          "h-4 w-4 rounded-full border-2 flex items-center justify-center",
                          selectedUserId === user.id
                            ? "border-primary"
                            : "border-muted-foreground"
                        )}
                      >
                        {selectedUserId === user.id && (
                          <div className="h-2 w-2 rounded-full bg-primary" />
                        )}
                      </div>
                      <div className="flex-1">
                        <div className="font-medium">{user.fullName}</div>
                        <div className="text-sm text-muted-foreground">
                          {user.email}
                        </div>
                        {user.roleNames.length > 0 && (
                          <div className="text-xs text-muted-foreground">
                            Roles: {user.roleNames.join(", ")}
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
            disabled={linkEmployeeToUser.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleLink}
            disabled={!selectedUserId || linkEmployeeToUser.isPending}
          >
            {linkEmployeeToUser.isPending ? (
              <>
                <LoadingSpinner className="mr-2 h-4 w-4" />
                Linking...
              </>
            ) : (
              "Link User"
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
