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
import { useUnlinkEmployeeFromUser } from "@/lib/api/admin/use-employees";
import { toast } from "sonner";
import type { Employee } from "@/types/admin";
import { getApiErrorMessage } from "@/lib/utils";

interface UnlinkUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  employee: Employee;
}

export function UnlinkUserDialog({
  open,
  onOpenChange,
  employee,
}: UnlinkUserDialogProps) {
  const unlinkUser = useUnlinkEmployeeFromUser();

  async function handleUnlink() {
    try {
      await unlinkUser.mutateAsync(employee.id);
      toast.success("User account unlinked", {
        description: `${employee.fullName} is no longer linked to a user account`,
      });
      onOpenChange(false);
    } catch (error) {
      toast.error("Failed to unlink user", {
        description: getApiErrorMessage(error),
      });
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Unlink User Account</AlertDialogTitle>
          <AlertDialogDescription className="space-y-2">
            <p>
              Are you sure you want to unlink the user account from{" "}
              <span className="font-medium">{employee.fullName}</span>?
            </p>
            <p className="text-amber-600">
              Warning: The user will lose the ability to log in to the system
              as this employee. The user account will still exist but will no
              longer be associated with this employee record.
            </p>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={unlinkUser.isPending}
          >
            Cancel
          </Button>
          <Button
            variant="destructive"
            onClick={handleUnlink}
            disabled={unlinkUser.isPending}
          >
            {unlinkUser.isPending ? (
              <>
                <LoadingSpinner className="mr-2 h-4 w-4" />
                Unlinking...
              </>
            ) : (
              "Unlink Account"
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
