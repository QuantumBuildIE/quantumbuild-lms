"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";
import { useResendInvite } from "@/lib/api/admin/use-employees";
import { toast } from "sonner";
import type { Employee } from "@/types/admin";
import { User as UserIcon, Link2, UserPlus, Unlink, Mail } from "lucide-react";
import { LinkEmployeeToUserDialog } from "./link-employee-to-user-dialog";
import { CreateUserForEmployeeDialog } from "./create-user-for-employee-dialog";
import { UnlinkUserDialog } from "./unlink-user-dialog";
import { getApiErrorMessage } from "@/lib/utils";

interface EmployeeUserAccountSectionProps {
  employee: Employee;
}

export function EmployeeUserAccountSection({ employee }: EmployeeUserAccountSectionProps) {
  const [showLinkDialog, setShowLinkDialog] = React.useState(false);
  const [showCreateDialog, setShowCreateDialog] = React.useState(false);
  const [showUnlinkDialog, setShowUnlinkDialog] = React.useState(false);

  const resendInvite = useResendInvite();

  const hasLinkedUser = employee.hasUserAccount;
  const hasEmail = !!employee.email;

  async function handleResendInvite() {
    try {
      await resendInvite.mutateAsync(employee.id);
      toast.success("Invite sent", {
        description: `Password setup email has been sent to ${employee.email}`,
      });
    } catch (error) {
      toast.error("Failed to send invite", {
        description: getApiErrorMessage(error),
      });
    }
  }

  return (
    <>
      <div className="rounded-lg border bg-muted/50 p-4 space-y-4">
        <div className="flex items-center gap-2">
          <UserIcon className="h-5 w-5 text-muted-foreground" />
          <h3 className="font-medium">User Account</h3>
        </div>

        {hasLinkedUser ? (
          // Employee HAS a linked user account
          <div className="space-y-4">
            <div className="flex items-start gap-2 rounded-md bg-green-50 border border-green-200 p-3 text-sm text-green-800 dark:bg-green-950 dark:border-green-900 dark:text-green-300">
              <UserIcon className="h-4 w-4 mt-0.5 shrink-0" />
              <div>
                <p className="font-medium">This employee has a login account</p>
                <p className="text-green-700 dark:text-green-400">
                  Changes to email or name will be synced to the user account.
                </p>
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              {hasEmail && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleResendInvite}
                  disabled={resendInvite.isPending}
                >
                  <Mail className="mr-2 h-4 w-4" />
                  {resendInvite.isPending ? "Sending..." : "Resend Invite"}
                </Button>
              )}
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setShowUnlinkDialog(true)}
                className="text-destructive hover:text-destructive"
              >
                <Unlink className="mr-2 h-4 w-4" />
                Unlink Account
              </Button>
            </div>
          </div>
        ) : (
          // Employee does NOT have a linked user account
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              This employee does not have a login account. You can create one or link to an existing user.
            </p>

            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setShowCreateDialog(true)}
                disabled={!hasEmail}
                title={!hasEmail ? "Employee must have an email address" : undefined}
              >
                <UserPlus className="mr-2 h-4 w-4" />
                Create User Account
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setShowLinkDialog(true)}
              >
                <Link2 className="mr-2 h-4 w-4" />
                Link to Existing User
              </Button>
            </div>

            {!hasEmail && (
              <p className="text-xs text-amber-600">
                Add an email address to enable creating a user account.
              </p>
            )}
          </div>
        )}
      </div>

      {/* Dialogs */}
      <LinkEmployeeToUserDialog
        open={showLinkDialog}
        onOpenChange={setShowLinkDialog}
        employee={employee}
      />
      <CreateUserForEmployeeDialog
        open={showCreateDialog}
        onOpenChange={setShowCreateDialog}
        employee={employee}
      />
      <UnlinkUserDialog
        open={showUnlinkDialog}
        onOpenChange={setShowUnlinkDialog}
        employee={employee}
      />
    </>
  );
}
