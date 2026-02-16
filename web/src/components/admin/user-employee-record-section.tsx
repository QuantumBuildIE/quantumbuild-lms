"use client";

import * as React from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import type { User } from "@/lib/api/admin/users";
import { Users, Link2, UserPlus, Unlink, ExternalLink } from "lucide-react";
import { LinkUserToEmployeeDialog } from "./link-user-to-employee-dialog";
import { CreateEmployeeForUserDialog } from "./create-employee-for-user-dialog";
import { UnlinkEmployeeDialog } from "./unlink-employee-dialog";

interface UserEmployeeRecordSectionProps {
  user: User;
}

export function UserEmployeeRecordSection({ user }: UserEmployeeRecordSectionProps) {
  const [showLinkDialog, setShowLinkDialog] = React.useState(false);
  const [showCreateDialog, setShowCreateDialog] = React.useState(false);
  const [showUnlinkDialog, setShowUnlinkDialog] = React.useState(false);

  const hasLinkedEmployee = !!user.employeeId;

  return (
    <>
      <div className="rounded-lg border bg-muted/50 p-4 space-y-4">
        <div className="flex items-center gap-2">
          <Users className="h-5 w-5 text-muted-foreground" />
          <h3 className="font-medium">Employee Record</h3>
        </div>

        {hasLinkedEmployee ? (
          // User HAS a linked employee record
          <div className="space-y-4">
            <div className="flex items-start gap-2 rounded-md bg-green-50 border border-green-200 p-3 text-sm text-green-800 dark:bg-green-950 dark:border-green-900 dark:text-green-300">
              <Users className="h-4 w-4 mt-0.5 shrink-0" />
              <div>
                <p className="font-medium">Linked to employee: {user.employeeName}</p>
                <p className="text-green-700 dark:text-green-400">
                  This user is associated with an employee record.
                </p>
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                asChild
              >
                <Link href={`/admin/employees/${user.employeeId}/edit`}>
                  <ExternalLink className="mr-2 h-4 w-4" />
                  View Employee
                </Link>
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setShowUnlinkDialog(true)}
                className="text-destructive hover:text-destructive"
              >
                <Unlink className="mr-2 h-4 w-4" />
                Unlink Employee
              </Button>
            </div>
          </div>
        ) : (
          // User does NOT have a linked employee record
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              This user does not have an employee record. You can create one or link to an existing employee.
            </p>

            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setShowCreateDialog(true)}
              >
                <UserPlus className="mr-2 h-4 w-4" />
                Create Employee Record
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setShowLinkDialog(true)}
              >
                <Link2 className="mr-2 h-4 w-4" />
                Link to Existing Employee
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Dialogs */}
      <LinkUserToEmployeeDialog
        open={showLinkDialog}
        onOpenChange={setShowLinkDialog}
        user={user}
      />
      <CreateEmployeeForUserDialog
        open={showCreateDialog}
        onOpenChange={setShowCreateDialog}
        user={user}
      />
      <UnlinkEmployeeDialog
        open={showUnlinkDialog}
        onOpenChange={setShowUnlinkDialog}
        user={user}
      />
    </>
  );
}
