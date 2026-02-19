"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { DeleteConfirmationDialog } from "@/components/shared/delete-confirmation-dialog";
import {
  useAssignedOperators,
  useAvailableOperators,
  useAssignOperators,
  useUnassignOperator,
} from "@/lib/api/admin/use-supervisor-assignments";
import type { SupervisorOperatorDto } from "@/lib/api/admin/supervisor-assignments";
import { useAuth, usePermission, useIsSuperUser } from "@/lib/auth/use-auth";
import { toast } from "sonner";
import { Users, Plus, Trash2 } from "lucide-react";

interface AssignedOperatorsSectionProps {
  employeeId: string;
}

export function AssignedOperatorsSection({ employeeId }: AssignedOperatorsSectionProps) {
  const { user } = useAuth();
  const isSuperUser = useIsSuperUser();
  const hasManageEmployees = usePermission("Core.ManageEmployees");
  const isOwnProfile = user?.employeeId === employeeId;
  const canManage = isSuperUser || hasManageEmployees || isOwnProfile;
  const { data: operators = [], isLoading } = useAssignedOperators(employeeId);

  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<SupervisorOperatorDto | null>(null);

  const unassignMutation = useUnassignOperator();

  const handleRemoveConfirm = () => {
    if (!removeTarget) return;

    unassignMutation.mutate(
      { supervisorId: employeeId, operatorId: removeTarget.employeeId },
      {
        onSuccess: () => {
          toast.success(`${removeTarget.fullName} has been unassigned`);
          setRemoveTarget(null);
        },
        onError: () => {
          toast.error("Failed to unassign operator");
        },
      }
    );
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Users className="h-5 w-5" />
            Assigned Operators
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="animate-pulse space-y-2">
            <div className="h-12 bg-muted rounded" />
            <div className="h-12 bg-muted rounded" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="flex items-center gap-2">
              <Users className="h-5 w-5" />
              Assigned Operators
              {operators.length > 0 && (
                <Badge variant="secondary">{operators.length}</Badge>
              )}
            </CardTitle>
            {canManage && (
              <Button size="sm" onClick={() => setAssignDialogOpen(true)}>
                <Plus className="h-4 w-4 mr-1" />
                Assign Operators
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent>
          {operators.length === 0 ? (
            <p className="text-sm text-muted-foreground">No operators assigned yet.</p>
          ) : (
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
                  <span className="text-sm text-muted-foreground">{operator.employeeCode}</span>
                  <span className="text-sm text-muted-foreground">{operator.department || "—"}</span>
                  <span className="text-sm text-muted-foreground">{operator.jobTitle || "—"}</span>
                  {canManage && (
                    <Button
                      size="icon"
                      variant="ghost"
                      className="h-8 w-8 text-destructive hover:text-destructive"
                      onClick={() => setRemoveTarget(operator)}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <AssignOperatorsDialog
        supervisorId={employeeId}
        open={assignDialogOpen}
        onOpenChange={setAssignDialogOpen}
      />

      <DeleteConfirmationDialog
        open={!!removeTarget}
        onOpenChange={(open) => !open && setRemoveTarget(null)}
        title="Remove Operator"
        description={`Are you sure you want to remove ${removeTarget?.fullName} from this supervisor's assigned operators?`}
        onConfirm={handleRemoveConfirm}
        isLoading={unassignMutation.isPending}
      />
    </>
  );
}

export function AssignOperatorsDialog({
  supervisorId,
  open,
  onOpenChange,
}: {
  supervisorId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { data: available = [], isLoading } = useAvailableOperators(supervisorId, open);
  const assignMutation = useAssignOperators();

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [search, setSearch] = useState("");

  const filtered = available.filter((op) => {
    if (!search) return true;
    const q = search.toLowerCase();
    return (
      op.fullName.toLowerCase().includes(q) ||
      op.employeeCode.toLowerCase().includes(q) ||
      op.department?.toLowerCase().includes(q) ||
      op.jobTitle?.toLowerCase().includes(q)
    );
  });

  const toggleOperator = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleAssign = () => {
    if (selected.size === 0) return;

    assignMutation.mutate(
      {
        supervisorId,
        data: { operatorEmployeeIds: Array.from(selected) },
      },
      {
        onSuccess: (result) => {
          toast.success(`${result.length} operator(s) assigned successfully`);
          setSelected(new Set());
          setSearch("");
          onOpenChange(false);
        },
        onError: () => {
          toast.error("Failed to assign operators");
        },
      }
    );
  };

  const handleClose = (value: boolean) => {
    if (!value) {
      setSelected(new Set());
      setSearch("");
    }
    onOpenChange(value);
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-lg max-h-[80vh] flex flex-col">
        <DialogHeader>
          <DialogTitle>Assign Operators</DialogTitle>
          <DialogDescription>
            Select operators to assign to this supervisor.
          </DialogDescription>
        </DialogHeader>

        <Input
          placeholder="Search by name, code, department..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />

        <div className="flex-1 overflow-y-auto min-h-0 max-h-[400px] space-y-1">
          {isLoading ? (
            <div className="animate-pulse space-y-2 p-2">
              <div className="h-10 bg-muted rounded" />
              <div className="h-10 bg-muted rounded" />
              <div className="h-10 bg-muted rounded" />
            </div>
          ) : filtered.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">
              {available.length === 0
                ? "No available operators to assign."
                : "No operators match your search."}
            </p>
          ) : (
            filtered.map((operator) => (
              <label
                key={operator.employeeId}
                className="flex items-center gap-3 px-3 py-2 rounded-lg border cursor-pointer hover:bg-muted/50 transition-colors"
              >
                <Checkbox
                  checked={selected.has(operator.employeeId)}
                  onCheckedChange={() => toggleOperator(operator.employeeId)}
                />
                <div className="flex-1 min-w-0">
                  <div className="font-medium text-sm">{operator.fullName}</div>
                  <div className="text-xs text-muted-foreground">
                    {operator.employeeCode}
                    {operator.department && ` · ${operator.department}`}
                    {operator.jobTitle && ` · ${operator.jobTitle}`}
                  </div>
                </div>
              </label>
            ))
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleClose(false)}>
            Cancel
          </Button>
          <Button
            onClick={handleAssign}
            disabled={selected.size === 0 || assignMutation.isPending}
          >
            {assignMutation.isPending
              ? "Assigning..."
              : `Assign${selected.size > 0 ? ` (${selected.size})` : ""}`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
