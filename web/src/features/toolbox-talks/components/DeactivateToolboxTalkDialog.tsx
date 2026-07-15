'use client';

import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useToggleToolboxTalkActive } from '@/lib/api/toolbox-talks';
import { toast } from 'sonner';

interface DeactivateToolboxTalkDialogProps {
  talkId: string;
  talkTitle: string;
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
}

function getErrorMessage(err: unknown): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const axiosError = err as { response?: { data?: { message?: string } } };
    if (axiosError.response?.data?.message) {
      return axiosError.response.data.message;
    }
  }
  if (err instanceof Error) {
    return err.message;
  }
  return 'Failed to deactivate learning';
}

export function DeactivateToolboxTalkDialog({
  talkId,
  talkTitle,
  isOpen,
  onOpenChange,
}: DeactivateToolboxTalkDialogProps) {
  const toggleMutation = useToggleToolboxTalkActive();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Reset transient failure state each time the dialog is (re)opened.
  useEffect(() => {
    if (isOpen) {
      setErrorMessage(null);
    }
  }, [isOpen]);

  const isDeactivating = toggleMutation.isPending;

  const handleDeactivate = async () => {
    setErrorMessage(null);
    try {
      await toggleMutation.mutateAsync({ id: talkId, active: false });
      toast.success('Learning deactivated');
      onOpenChange(false);
    } catch (err) {
      setErrorMessage(getErrorMessage(err));
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !isDeactivating && onOpenChange(open)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Deactivate &ldquo;{talkTitle}&rdquo;?</DialogTitle>
          <DialogDescription>
            This prevents new schedules from being created for this learning. Existing
            scheduled assignments, refresher reminders, and operator visibility are unchanged.
          </DialogDescription>
        </DialogHeader>

        {errorMessage && (
          <Alert variant="destructive">
            <AlertDescription>{errorMessage}</AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isDeactivating}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={handleDeactivate}
            disabled={isDeactivating}
          >
            {isDeactivating ? 'Deactivating…' : 'Deactivate'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
