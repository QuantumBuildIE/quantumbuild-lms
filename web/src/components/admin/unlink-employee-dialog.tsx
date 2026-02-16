"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { useUnlinkUserFromEmployee } from "@/lib/api/admin/use-users";
import { toast } from "sonner";
import type { User } from "@/lib/api/admin/users";

interface UnlinkEmployeeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  user: User;
}

export function UnlinkEmployeeDialog({
  open,
  onOpenChange,
  user,
}: UnlinkEmployeeDialogProps) {
  const unlinkEmployee = useUnlinkUserFromEmployee();

  async function handleUnlink() {
    try {
      await unlinkEmployee.mutateAsync(user.id);
      toast.success("Employee record unlinked", {
        description: `${user.fullName} is no longer linked to an employee record`,
      });
      onOpenChange(false);
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error("Failed to unlink employee", {
        description: message,
      });
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Unlink Employee Record</AlertDialogTitle>
          <AlertDialogDescription className="space-y-2">
            <p>
              Are you sure you want to unlink the employee record from{" "}
              <span className="font-medium">{user.fullName}</span>?
            </p>
            {user.employeeName && (
              <p>
                Currently linked to employee:{" "}
                <span className="font-medium">{user.employeeName}</span>
              </p>
            )}
            <p className="text-amber-600">
              Note: The employee record will still exist but will no longer be
              associated with this user account.
            </p>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={unlinkEmployee.isPending}
          >
            Cancel
          </Button>
          <Button
            variant="destructive"
            onClick={handleUnlink}
            disabled={unlinkEmployee.isPending}
          >
            {unlinkEmployee.isPending ? (
              <>
                <LoadingSpinner className="mr-2 h-4 w-4" />
                Unlinking...
              </>
            ) : (
              "Unlink Employee"
            )}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
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
