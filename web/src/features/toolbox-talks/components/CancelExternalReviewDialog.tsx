'use client';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

interface CancelExternalReviewDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  isLoading?: boolean;
}

export function CancelExternalReviewDialog({
  open,
  onOpenChange,
  onConfirm,
  isLoading = false,
}: CancelExternalReviewDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cancel external review invitation?</DialogTitle>
          <DialogDescription>
            This will revoke the active invitation. The reviewer will no longer be able to access
            the portal.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isLoading}
          >
            Don&apos;t cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={onConfirm}
            disabled={isLoading}
          >
            {isLoading ? 'Cancelling…' : 'Cancel invitation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
