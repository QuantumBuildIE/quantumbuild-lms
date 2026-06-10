'use client';

import { format } from 'date-fns';
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

interface DeleteDraftDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  talkTitle: string;
  lastEditedAt: string | Date | null;
  onConfirm: () => void;
  isDeleting?: boolean;
}

export function DeleteDraftDialog({
  open,
  onOpenChange,
  talkTitle,
  lastEditedAt,
  onConfirm,
  isDeleting = false,
}: DeleteDraftDialogProps) {
  const formattedDate = lastEditedAt
    ? format(new Date(lastEditedAt), 'dd MMM yyyy, HH:mm')
    : null;

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete draft?</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-2">
              <p>
                <span className="font-medium text-foreground">{talkTitle}</span>
                {' '}will be permanently deleted. This cannot be undone.
              </p>
              {formattedDate && (
                <p className="text-xs text-muted-foreground">
                  Last edited {formattedDate}
                </p>
              )}
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isDeleting}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            disabled={isDeleting}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isDeleting ? 'Deleting…' : 'Delete draft'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
