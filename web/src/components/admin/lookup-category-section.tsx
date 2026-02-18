"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Pencil, Trash2, Plus, X, Check } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SortableList, SortableListItem, DragHandle, useSortableItem } from "@/components/ui/sortable";
import { DeleteConfirmationDialog } from "@/components/shared/delete-confirmation-dialog";
import {
  type LookupValue,
  type LookupCategory,
  useLookupValues,
  useCreateLookupValue,
  useUpdateLookupValue,
  useDeleteLookupValue,
} from "@/hooks/use-lookups";
import { Skeleton } from "@/components/ui/skeleton";

const CATEGORY_DISPLAY_NAMES: Record<string, { title: string; description: string }> = {
  TrainingCategory: {
    title: "Training Categories",
    description: "Categories used to classify learnings (e.g., Safety, Compliance, Orientation)",
  },
  Department: {
    title: "Departments",
    description: "Organisational departments for employee grouping",
  },
  JobTitle: {
    title: "Job Titles",
    description: "Job titles available for employees",
  },
};

interface LookupCategorySectionProps {
  category: LookupCategory;
}

export function LookupCategorySection({ category }: LookupCategorySectionProps) {
  const display = CATEGORY_DISPLAY_NAMES[category.name] ?? {
    title: category.name,
    description: `Manage ${category.name} lookup values`,
  };

  const { data: values, isLoading } = useLookupValues(category.name);
  const createMutation = useCreateLookupValue(category.name);
  const updateMutation = useUpdateLookupValue(category.name);
  const deleteMutation = useDeleteLookupValue(category.name);

  const [newName, setNewName] = useState("");
  const [newCode, setNewCode] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<LookupValue | null>(null);

  const handleAdd = () => {
    const name = newName.trim();
    if (!name) return;

    const code = newCode.trim() || name.toLowerCase().replace(/\s+/g, "-");
    const sortOrder = values ? values.length : 0;

    createMutation.mutate(
      { name, code, sortOrder },
      {
        onSuccess: () => {
          setNewName("");
          setNewCode("");
          toast.success(`Added "${name}"`);
        },
        onError: (error: any) => {
          toast.error(error?.response?.data?.message ?? "Failed to add value");
        },
      }
    );
  };

  const handleStartEdit = (value: LookupValue) => {
    setEditingId(value.id);
    setEditName(value.name);
  };

  const handleSaveEdit = (value: LookupValue) => {
    const name = editName.trim();
    if (!name || name === value.name) {
      setEditingId(null);
      return;
    }

    updateMutation.mutate(
      {
        id: value.id,
        code: value.code,
        name,
        sortOrder: value.sortOrder,
        isEnabled: true,
      },
      {
        onSuccess: () => {
          setEditingId(null);
          toast.success(`Renamed to "${name}"`);
        },
        onError: (error: any) => {
          toast.error(error?.response?.data?.message ?? "Failed to update");
        },
      }
    );
  };

  const handleDelete = () => {
    if (!deleteTarget) return;

    deleteMutation.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success(`Deleted "${deleteTarget.name}"`);
        setDeleteTarget(null);
      },
      onError: (error: any) => {
        toast.error(error?.response?.data?.message ?? "Failed to delete");
        setDeleteTarget(null);
      },
    });
  };

  const handleReorder = (reordered: LookupValue[]) => {
    // Update sortOrder for all reordered items
    reordered.forEach((item, index) => {
      if (item.sortOrder !== index && !item.isGlobal) {
        updateMutation.mutate({
          id: item.id,
          code: item.code,
          name: item.name,
          sortOrder: index,
          isEnabled: true,
        });
      }
    });
  };

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>{display.title}</CardTitle>
          <CardDescription>{display.description}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {isLoading ? (
            <div className="space-y-2">
              {[1, 2, 3].map((i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : values && values.length > 0 ? (
            <SortableList
              items={values}
              keyExtractor={(item) => item.id}
              onReorder={handleReorder}
            >
              <div className="space-y-1">
                {values.map((value) => (
                  <SortableValueRow
                    key={value.id}
                    value={value}
                    isEditing={editingId === value.id}
                    editName={editName}
                    onEditNameChange={setEditName}
                    onStartEdit={() => handleStartEdit(value)}
                    onSaveEdit={() => handleSaveEdit(value)}
                    onCancelEdit={() => setEditingId(null)}
                    onDelete={() => setDeleteTarget(value)}
                    isSaving={updateMutation.isPending}
                  />
                ))}
              </div>
            </SortableList>
          ) : (
            <p className="text-sm text-muted-foreground py-2">
              No values configured yet. Add your first value below.
            </p>
          )}

          {/* Add new value form */}
          {category.allowCustom && (
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
          )}
        </CardContent>
      </Card>

      <DeleteConfirmationDialog
        open={!!deleteTarget}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
        title="Delete Lookup Value"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This cannot be undone.`}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
      />
    </>
  );
}

// Sortable row component for individual lookup values
interface SortableValueRowProps {
  value: LookupValue;
  isEditing: boolean;
  editName: string;
  onEditNameChange: (name: string) => void;
  onStartEdit: () => void;
  onSaveEdit: () => void;
  onCancelEdit: () => void;
  onDelete: () => void;
  isSaving: boolean;
}

function SortableValueRow({
  value,
  isEditing,
  editName,
  onEditNameChange,
  onStartEdit,
  onSaveEdit,
  onCancelEdit,
  onDelete,
  isSaving,
}: SortableValueRowProps) {
  const { attributes, listeners, setNodeRef, style, isDragging } = useSortableItem({
    id: value.id,
    disabled: value.isGlobal,
  });

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`flex items-center gap-2 rounded-md border px-3 py-2 ${
        isDragging ? "bg-muted/50 shadow-sm" : "bg-background"
      }`}
    >
      {!value.isGlobal && (
        <DragHandle {...listeners} {...attributes} />
      )}
      {value.isGlobal && <div className="w-6" />}

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
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7"
            onClick={onSaveEdit}
            disabled={isSaving}
          >
            <Check className="h-3.5 w-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7"
            onClick={onCancelEdit}
          >
            <X className="h-3.5 w-3.5" />
          </Button>
        </>
      ) : (
        <>
          <span className="flex-1 text-sm">{value.name}</span>
          {value.code && (
            <span className="text-xs text-muted-foreground font-mono">
              {value.code}
            </span>
          )}
          {value.isGlobal && (
            <span className="text-xs text-muted-foreground bg-muted px-1.5 py-0.5 rounded">
              System
            </span>
          )}
          {!value.isGlobal && (
            <>
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7"
                onClick={onStartEdit}
              >
                <Pencil className="h-3.5 w-3.5" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7 text-destructive hover:text-destructive"
                onClick={onDelete}
              >
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
            </>
          )}
        </>
      )}
    </div>
  );
}
