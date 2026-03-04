'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Plus, Pencil, Trash2, Save, X, Check } from 'lucide-react';
import { toast } from 'sonner';
import {
  useTenantSettings,
  useUpdateTenantSettings,
} from '@/lib/api/admin/use-tenant-settings';

const SETTINGS_KEY = 'ValidationAuditPurposes';

const DEFAULT_PURPOSES = [
  'Regulatory Compliance',
  'Internal Quality Assurance',
  'Client Delivery Review',
  'Annual Audit',
];

export function AuditPurposeSection() {
  const { data: settings, isLoading } = useTenantSettings();
  const updateMutation = useUpdateTenantSettings();
  const [purposes, setPurposes] = useState<string[]>([]);
  const [newPurpose, setNewPurpose] = useState('');
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [editValue, setEditValue] = useState('');
  const [deleteIndex, setDeleteIndex] = useState<number | null>(null);
  const [isDirty, setIsDirty] = useState(false);

  // Load purposes from tenant settings
  useEffect(() => {
    if (settings) {
      const raw = settings[SETTINGS_KEY];
      if (raw) {
        try {
          const parsed = JSON.parse(raw);
          if (Array.isArray(parsed)) {
            setPurposes(parsed);
            return;
          }
        } catch {
          // invalid JSON, use defaults
        }
      }
      setPurposes([...DEFAULT_PURPOSES]);
    }
  }, [settings]);

  const handleAdd = () => {
    const trimmed = newPurpose.trim();
    if (!trimmed) {
      toast.error('Purpose cannot be empty');
      return;
    }
    if (purposes.includes(trimmed)) {
      toast.error('This purpose already exists');
      return;
    }
    setPurposes((prev) => [...prev, trimmed]);
    setNewPurpose('');
    setIsDirty(true);
  };

  const handleStartEdit = (index: number) => {
    setEditingIndex(index);
    setEditValue(purposes[index]);
  };

  const handleSaveEdit = () => {
    if (editingIndex === null) return;
    const trimmed = editValue.trim();
    if (!trimmed) {
      toast.error('Purpose cannot be empty');
      return;
    }
    // Check for duplicates (excluding current index)
    if (purposes.some((p, i) => i !== editingIndex && p === trimmed)) {
      toast.error('This purpose already exists');
      return;
    }
    setPurposes((prev) =>
      prev.map((p, i) => (i === editingIndex ? trimmed : p))
    );
    setEditingIndex(null);
    setEditValue('');
    setIsDirty(true);
  };

  const handleConfirmDelete = () => {
    if (deleteIndex === null) return;
    if (purposes.length <= 1) {
      toast.error('At least one audit purpose must remain');
      setDeleteIndex(null);
      return;
    }
    setPurposes((prev) => prev.filter((_, i) => i !== deleteIndex));
    setDeleteIndex(null);
    setIsDirty(true);
  };

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({
        settings: [
          { key: SETTINGS_KEY, value: JSON.stringify(purposes) },
        ],
      });
      setIsDirty(false);
      toast.success('Audit purposes saved');
    } catch {
      toast.error('Failed to save audit purposes');
    }
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Audit Purpose Options</CardTitle>
        </CardHeader>
        <CardContent>
          <Skeleton className="h-10 w-full" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Audit Purpose Options</CardTitle>
        <CardDescription>
          Configure the list of available audit purposes shown when starting a translation
          validation run.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Current purposes */}
        <div className="space-y-1">
          {purposes.map((purpose, index) => (
            <div
              key={index}
              className="flex items-center gap-2 py-1.5 px-3 rounded hover:bg-muted/50 group"
            >
              {editingIndex === index ? (
                <>
                  <Input
                    value={editValue}
                    onChange={(e) => setEditValue(e.target.value)}
                    className="flex-1 h-8"
                    autoFocus
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        handleSaveEdit();
                      }
                      if (e.key === 'Escape') {
                        setEditingIndex(null);
                      }
                    }}
                  />
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 w-7 p-0"
                    onClick={handleSaveEdit}
                  >
                    <Check className="h-3.5 w-3.5" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 w-7 p-0"
                    onClick={() => setEditingIndex(null)}
                  >
                    <X className="h-3.5 w-3.5" />
                  </Button>
                </>
              ) : (
                <>
                  <span className="flex-1 text-sm">{purpose}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100"
                    onClick={() => handleStartEdit(index)}
                  >
                    <Pencil className="h-3 w-3" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100 text-destructive hover:text-destructive"
                    disabled={purposes.length <= 1}
                    onClick={() => setDeleteIndex(index)}
                  >
                    <Trash2 className="h-3 w-3" />
                  </Button>
                </>
              )}
            </div>
          ))}
          {purposes.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-2">
              No audit purposes configured.
            </p>
          )}
        </div>

        {/* Add new purpose */}
        <div className="flex gap-2 items-center">
          <Input
            value={newPurpose}
            onChange={(e) => setNewPurpose(e.target.value)}
            placeholder="Enter new audit purpose"
            className="flex-1"
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                handleAdd();
              }
            }}
          />
          <Button variant="outline" size="sm" onClick={handleAdd}>
            <Plus className="h-3.5 w-3.5 mr-1" />
            Add
          </Button>
        </div>

        {/* Save button */}
        {isDirty && (
          <div className="flex justify-end">
            <Button onClick={handleSave} disabled={updateMutation.isPending}>
              <Save className="h-4 w-4 mr-2" />
              {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        )}
      </CardContent>

      {/* Delete confirmation dialog */}
      <AlertDialog open={deleteIndex !== null} onOpenChange={() => setDeleteIndex(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Audit Purpose</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete &ldquo;{deleteIndex !== null ? purposes[deleteIndex] : ''}&rdquo;?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}
