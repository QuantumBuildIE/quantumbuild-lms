'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import axios from 'axios';
import { toast } from 'sonner';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { DeleteConfirmationDialog } from '@/components/shared/delete-confirmation-dialog';
import { Pencil, Trash2, Plus, Loader2 } from 'lucide-react';
import { usePermission } from '@/lib/auth/use-auth';
import { useLookupValues } from '@/hooks/use-lookups';
import {
  useTenantReviewerConfigurations,
  useCreateTenantReviewerConfiguration,
  useUpdateTenantReviewerConfiguration,
  useDeleteTenantReviewerConfiguration,
} from '@/lib/api/admin/use-tenant-reviewer-configurations';
import type { TenantReviewerConfigurationDto } from '@/types/admin';

const ALL_LANGUAGES_VALUE = '__all__';

const reviewerFormSchema = z.object({
  languageCode: z.string().min(1, 'Select a language'),
  reviewerEmail: z.string().min(1, 'Reviewer email is required').email('Must be a valid email address'),
  reviewerName: z.string().optional(),
});

type ReviewerFormValues = z.infer<typeof reviewerFormSchema>;

function extractErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { error?: string; errors?: string[] } | undefined;
    if (data?.error) return data.error;
    if (data?.errors?.length) return data.errors[0];
  }
  return fallback;
}

export function ExternalReviewersSection() {
  const canManage = usePermission('Learnings.Admin');
  const { data: configurations, isLoading } = useTenantReviewerConfigurations();
  const { data: languages = [] } = useLookupValues('Language');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingConfig, setEditingConfig] = useState<TenantReviewerConfigurationDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<TenantReviewerConfigurationDto | null>(null);

  const createMutation = useCreateTenantReviewerConfiguration();
  const updateMutation = useUpdateTenantReviewerConfiguration();
  const deleteMutation = useDeleteTenantReviewerConfiguration();

  const languageNameByCode = new Map(languages.map((l) => [l.code.toLowerCase(), l.name]));

  const openAddDialog = () => {
    setEditingConfig(null);
    setDialogOpen(true);
  };

  const openEditDialog = (config: TenantReviewerConfigurationDto) => {
    setEditingConfig(config);
    setDialogOpen(true);
  };

  const handleSubmit = async (values: ReviewerFormValues) => {
    const languageCode = values.languageCode === ALL_LANGUAGES_VALUE ? null : values.languageCode;

    try {
      if (editingConfig) {
        await updateMutation.mutateAsync({
          id: editingConfig.id,
          data: {
            reviewerEmail: values.reviewerEmail,
            reviewerName: values.reviewerName || null,
          },
        });
        toast.success('Reviewer configuration updated');
      } else {
        await createMutation.mutateAsync({
          languageCode,
          reviewerEmail: values.reviewerEmail,
          reviewerName: values.reviewerName || null,
        });
        toast.success('Reviewer configuration added');
      }
      setDialogOpen(false);
    } catch (error) {
      toast.error(
        extractErrorMessage(error, editingConfig ? 'Failed to update reviewer' : 'Failed to add reviewer')
      );
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;

    try {
      await deleteMutation.mutateAsync(deleteTarget.id);
      toast.success('Reviewer configuration deleted');
      setDeleteTarget(null);
    } catch (error) {
      toast.error(extractErrorMessage(error, 'Failed to delete reviewer configuration'));
    }
  };

  if (!canManage) {
    return null;
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between space-y-0">
        <div>
          <CardTitle>External Reviewers</CardTitle>
          <CardDescription>
            Configure who receives external review invitations for each language. Set a
            language-specific reviewer or a fallback that applies to all languages.
          </CardDescription>
        </div>
        <Button size="sm" onClick={openAddDialog}>
          <Plus className="mr-1.5 h-4 w-4" />
          Add reviewer
        </Button>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : !configurations || configurations.length === 0 ? (
          <p className="text-sm text-muted-foreground py-4 text-center">
            No reviewers configured yet. Add one to enable automated external review from the
            learnings list.
          </p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Language</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Name</TableHead>
                <TableHead className="w-[100px] text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {configurations.map((config) => (
                <TableRow key={config.id}>
                  <TableCell>
                    {config.languageCode === null ? (
                      <span className="font-medium">All languages</span>
                    ) : (
                      languageNameByCode.get(config.languageCode.toLowerCase()) ?? config.languageCode
                    )}
                  </TableCell>
                  <TableCell>{config.reviewerEmail}</TableCell>
                  <TableCell>{config.reviewerName ?? '—'}</TableCell>
                  <TableCell className="text-right">
                    <Button variant="ghost" size="icon" onClick={() => openEditDialog(config)}>
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button variant="ghost" size="icon" onClick={() => setDeleteTarget(config)}>
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <ReviewerFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        editingConfig={editingConfig}
        existingConfigurations={configurations ?? []}
        languages={languages}
        onSubmit={handleSubmit}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
      />

      <DeleteConfirmationDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title={
          deleteTarget?.languageCode === null
            ? 'Delete fallback reviewer configuration?'
            : `Delete reviewer configuration for ${
                deleteTarget ? languageNameByCode.get(deleteTarget.languageCode!.toLowerCase()) ?? deleteTarget.languageCode : ''
              }?`
        }
        description="This cannot be undone. Employees and learnings are unaffected — only the reviewer routing for this language is removed."
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
      />
    </Card>
  );
}

interface ReviewerFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editingConfig: TenantReviewerConfigurationDto | null;
  existingConfigurations: TenantReviewerConfigurationDto[];
  languages: { code: string; name: string }[];
  onSubmit: (values: ReviewerFormValues) => void | Promise<void>;
  isSubmitting: boolean;
}

function ReviewerFormDialog({
  open,
  onOpenChange,
  editingConfig,
  existingConfigurations,
  languages,
  onSubmit,
  isSubmitting,
}: ReviewerFormDialogProps) {
  const isEditing = !!editingConfig;

  const form = useForm<ReviewerFormValues>({
    resolver: zodResolver(reviewerFormSchema),
    defaultValues: {
      languageCode: '',
      reviewerEmail: '',
      reviewerName: '',
    },
  });

  useEffect(() => {
    if (!open) return;

    form.reset({
      languageCode: editingConfig
        ? editingConfig.languageCode ?? ALL_LANGUAGES_VALUE
        : '',
      reviewerEmail: editingConfig?.reviewerEmail ?? '',
      reviewerName: editingConfig?.reviewerName ?? '',
    });
  }, [open, editingConfig, form]);

  const usedCodes = new Set(
    existingConfigurations
      .filter((c) => c.id !== editingConfig?.id)
      .map((c) => c.languageCode?.toLowerCase() ?? ALL_LANGUAGES_VALUE)
  );

  const fallbackTaken = usedCodes.has(ALL_LANGUAGES_VALUE);
  const availableLanguages = languages.filter((l) => !usedCodes.has(l.code.toLowerCase()));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>{isEditing ? 'Edit Reviewer' : 'Add Reviewer'}</DialogTitle>
          <DialogDescription>
            {isEditing
              ? 'Update the reviewer email or name. The language cannot be changed — delete and recreate to change it.'
              : 'Choose a language, or configure an "All languages" fallback used when no language-specific reviewer is set.'}
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="languageCode"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Language</FormLabel>
                  <Select
                    value={field.value}
                    onValueChange={field.onChange}
                    disabled={isEditing}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select a language…" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {!fallbackTaken || isEditing ? (
                        <SelectItem value={ALL_LANGUAGES_VALUE}>All languages</SelectItem>
                      ) : null}
                      {availableLanguages
                        .sort((a, b) => a.name.localeCompare(b.name))
                        .map((lang) => (
                          <SelectItem key={lang.code} value={lang.code}>
                            {lang.name}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="reviewerEmail"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Reviewer email</FormLabel>
                  <FormControl>
                    <Input type="email" placeholder="reviewer@example.com" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="reviewerName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Reviewer name (optional)</FormLabel>
                  <FormControl>
                    <Input placeholder="Jane Doe" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Saving…
                  </>
                ) : (
                  'Save'
                )}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
