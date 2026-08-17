"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Pencil, Plus, X, Check } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import {
  useAllDepartments,
  useCreateDepartment,
  useUpdateDepartment,
} from "@/lib/api/admin/use-departments";
import { getApiErrorMessage } from "@/lib/utils";
import type { Department } from "@/types/admin";

export function DepartmentManagementSection() {
  const { data: departments, isLoading } = useAllDepartments();
  const createMutation = useCreateDepartment();
  const updateMutation = useUpdateDepartment();

  const [newName, setNewName] = useState("");
  const [newCode, setNewCode] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");
  const [editCode, setEditCode] = useState("");

  const handleAdd = () => {
    const name = newName.trim();
    if (!name) return;

    createMutation.mutate(
      { name, code: newCode.trim() || undefined, isActive: true },
      {
        onSuccess: () => {
          setNewName("");
          setNewCode("");
          toast.success(`Added "${name}"`);
        },
        onError: (error) => {
          toast.error(getApiErrorMessage(error, "Failed to add department"));
        },
      }
    );
  };

  const handleStartEdit = (department: Department) => {
    setEditingId(department.id);
    setEditName(department.name);
    setEditCode(department.code ?? "");
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setEditName("");
    setEditCode("");
  };

  const handleSaveEdit = (department: Department) => {
    const name = editName.trim();
    if (!name) return;

    const code = editCode.trim() || undefined;
    if (name === department.name && code === department.code) {
      handleCancelEdit();
      return;
    }

    updateMutation.mutate(
      { id: department.id, data: { name, code, isActive: department.isActive } },
      {
        onSuccess: () => {
          toast.success(`Updated "${name}"`);
          handleCancelEdit();
        },
        onError: (error) => {
          toast.error(getApiErrorMessage(error, "Failed to update department"));
        },
      }
    );
  };

  const handleToggleActive = (department: Department, isActive: boolean) => {
    updateMutation.mutate(
      {
        id: department.id,
        data: { name: department.name, code: department.code, isActive },
      },
      {
        onSuccess: () => {
          toast.success(`${department.name} ${isActive ? "activated" : "deactivated"}`);
        },
        onError: (error) => {
          toast.error(getApiErrorMessage(error, "Failed to update department"));
        },
      }
    );
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Departments</CardTitle>
        <CardDescription>
          Manage your organization&apos;s departments. Departments are available for
          selection on employee records; deactivating one removes it from new
          selections without affecting employees already assigned to it.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <div className="space-y-2">
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : departments && departments.length > 0 ? (
          <div className="space-y-1">
            {departments.map((department) => (
              <DepartmentRow
                key={department.id}
                department={department}
                isEditing={editingId === department.id}
                editName={editName}
                editCode={editCode}
                onEditNameChange={setEditName}
                onEditCodeChange={setEditCode}
                onStartEdit={() => handleStartEdit(department)}
                onSaveEdit={() => handleSaveEdit(department)}
                onCancelEdit={handleCancelEdit}
                onToggleActive={(isActive) => handleToggleActive(department, isActive)}
                isSaving={updateMutation.isPending}
              />
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground py-2">
            No departments configured yet. Add your first department below.
          </p>
        )}

        {/* Add new department form */}
        <div className="flex items-center gap-2 pt-2 border-t">
          <Input
            placeholder="Name"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") handleAdd();
            }}
            className="flex-1"
          />
          <Input
            placeholder="Code (optional)"
            value={newCode}
            onChange={(e) => setNewCode(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") handleAdd();
            }}
            className="w-40"
          />
          <Button
            size="sm"
            onClick={handleAdd}
            disabled={!newName.trim() || createMutation.isPending}
          >
            <Plus className="h-4 w-4 mr-1" />
            Add
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

interface DepartmentRowProps {
  department: Department;
  isEditing: boolean;
  editName: string;
  editCode: string;
  onEditNameChange: (name: string) => void;
  onEditCodeChange: (code: string) => void;
  onStartEdit: () => void;
  onSaveEdit: () => void;
  onCancelEdit: () => void;
  onToggleActive: (isActive: boolean) => void;
  isSaving: boolean;
}

function DepartmentRow({
  department,
  isEditing,
  editName,
  editCode,
  onEditNameChange,
  onEditCodeChange,
  onStartEdit,
  onSaveEdit,
  onCancelEdit,
  onToggleActive,
  isSaving,
}: DepartmentRowProps) {
  return (
    <div
      className={`flex items-center gap-2 rounded-md border px-3 py-2 ${
        department.isActive ? "bg-background" : "bg-muted/30"
      }`}
    >
      {isEditing ? (
        <>
          <Input
            value={editName}
            onChange={(e) => onEditNameChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") onSaveEdit();
              if (e.key === "Escape") onCancelEdit();
            }}
            className="h-7 flex-1"
            autoFocus
          />
          <Input
            value={editCode}
            onChange={(e) => onEditCodeChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") onSaveEdit();
              if (e.key === "Escape") onCancelEdit();
            }}
            placeholder="Code"
            className="h-7 w-32"
          />
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7"
            onClick={onSaveEdit}
            disabled={isSaving || !editName.trim()}
          >
            <Check className="h-3.5 w-3.5" />
          </Button>
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onCancelEdit}>
            <X className="h-3.5 w-3.5" />
          </Button>
        </>
      ) : (
        <>
          <span
            className={`flex-1 text-sm ${!department.isActive ? "text-muted-foreground" : ""}`}
          >
            {department.name}
          </span>
          {department.code && (
            <span className="text-xs text-muted-foreground font-mono">{department.code}</span>
          )}
          {!department.isActive && (
            <span className="text-xs text-muted-foreground bg-muted px-1.5 py-0.5 rounded">
              Inactive
            </span>
          )}
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onStartEdit}>
            <Pencil className="h-3.5 w-3.5" />
          </Button>
          <Switch
            checked={department.isActive}
            onCheckedChange={onToggleActive}
            disabled={isSaving}
            aria-label={department.isActive ? "Deactivate department" : "Activate department"}
          />
        </>
      )}
    </div>
  );
}
