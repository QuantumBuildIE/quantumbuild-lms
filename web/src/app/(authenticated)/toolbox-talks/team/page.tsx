"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Users, Plus, UserMinus, UserX } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { DeleteConfirmationDialog } from "@/components/shared/delete-confirmation-dialog";
import { AssignOperatorsDialog } from "@/components/admin/assigned-operators-section";
import {
  useMyOperators,
  useUnassignOperator,
} from "@/lib/api/admin/use-supervisor-assignments";
import type { SupervisorOperatorDto } from "@/lib/api/admin/supervisor-assignments";
import { useAuth } from "@/lib/auth/use-auth";
import { toast } from "sonner";

export default function MyTeamPage() {
  const router = useRouter();
  const { user } = useAuth();
  const isSupervisor = user?.roles?.includes("Supervisor") ?? false;

  const { data: operators = [], isLoading } = useMyOperators();
  const unassignMutation = useUnassignOperator();

  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<SupervisorOperatorDto | null>(null);

  // Non-supervisors redirected to dashboard
  if (user && !isSupervisor) {
    router.replace("/toolbox-talks");
    return null;
  }

  const supervisorId = user?.employeeId;

  const handleRemoveConfirm = () => {
    if (!removeTarget || !supervisorId) return;

    unassignMutation.mutate(
      { supervisorId, operatorId: removeTarget.employeeId },
      {
        onSuccess: () => {
          toast.success(`${removeTarget.fullName} has been removed from your team`);
          setRemoveTarget(null);
        },
        onError: () => {
          toast.error("Failed to remove operator");
        },
      }
    );
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight flex items-center gap-2">
            My Team
            {!isLoading && operators.length > 0 && (
              <Badge variant="secondary">{operators.length}</Badge>
            )}
          </h1>
          <p className="text-muted-foreground">
            Manage operators assigned to you
          </p>
        </div>
        {supervisorId && (
          <Button onClick={() => setAssignDialogOpen(true)}>
            <Plus className="h-4 w-4 mr-1" />
            Assign Operators
          </Button>
        )}
      </div>

      {isLoading ? (
        <Card>
          <CardContent className="py-6">
            <div className="animate-pulse space-y-3">
              <div className="h-10 bg-muted rounded" />
              <div className="h-10 bg-muted rounded" />
              <div className="h-10 bg-muted rounded" />
            </div>
          </CardContent>
        </Card>
      ) : operators.length === 0 ? (
        <Card className="p-8">
          <div className="flex flex-col items-center gap-3 text-center">
            <UserX className="h-10 w-10 text-muted-foreground" />
            <div>
              <h3 className="font-medium">No operators assigned to you yet</h3>
              <p className="text-sm text-muted-foreground mt-1">
                Use the Assign Operators button to add team members.
              </p>
            </div>
          </div>
        </Card>
      ) : (
        <Card>
          <CardContent className="py-4">
            <div className="space-y-2">
              <div className="grid grid-cols-[1fr_1fr_1fr_1fr_auto] gap-4 px-3 py-2 text-sm font-medium text-muted-foreground border-b">
                <span>Name</span>
                <span>Code</span>
                <span>Department</span>
                <span>Job Title</span>
                <span className="w-8" />
              </div>
              {operators.map((operator) => (
                <div
                  key={operator.employeeId}
                  className="grid grid-cols-[1fr_1fr_1fr_1fr_auto] gap-4 items-center px-3 py-2 rounded-lg border bg-muted/50"
                >
                  <span className="font-medium">{operator.fullName}</span>
                  <span className="text-sm text-muted-foreground">
                    {operator.employeeCode}
                  </span>
                  <span className="text-sm text-muted-foreground">
                    {operator.department || "\u2014"}
                  </span>
                  <span className="text-sm text-muted-foreground">
                    {operator.jobTitle || "\u2014"}
                  </span>
                  <Button
                    size="icon"
                    variant="ghost"
                    className="h-8 w-8 text-destructive hover:text-destructive"
                    onClick={() => setRemoveTarget(operator)}
                  >
                    <UserMinus className="h-4 w-4" />
                  </Button>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {supervisorId && (
        <AssignOperatorsDialog
          supervisorId={supervisorId}
          open={assignDialogOpen}
          onOpenChange={setAssignDialogOpen}
        />
      )}

      <DeleteConfirmationDialog
        open={!!removeTarget}
        onOpenChange={(open) => !open && setRemoveTarget(null)}
        title="Remove Operator"
        description={`Remove ${removeTarget?.fullName} from your team?`}
        onConfirm={handleRemoveConfirm}
        isLoading={unassignMutation.isPending}
        confirmLabel="Remove"
        confirmLoadingLabel="Removing..."
      />
    </div>
  );
}
