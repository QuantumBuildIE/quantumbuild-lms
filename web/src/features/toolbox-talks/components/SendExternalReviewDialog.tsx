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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

interface SendExternalReviewDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (email: string) => void;
  isLoading?: boolean;
  flaggedWordCount: number;
  languageName: string;
}

export function SendExternalReviewDialog({
  open,
  onOpenChange,
  onConfirm,
  isLoading = false,
  flaggedWordCount,
  languageName,
}: SendExternalReviewDialogProps) {
  const [email, setEmail] = useState('');

  useEffect(() => {
    if (open) {
      setEmail('');
    }
  }, [open]);

  const isValidEmail = email.includes('@') && email.trim().length > 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Send for External Review</DialogTitle>
          <DialogDescription>
            Send the <span className="font-medium text-foreground">{languageName}</span> translation
            to a third-party reviewer.{' '}
            {flaggedWordCount > 0
              ? `${flaggedWordCount} flagged word${flaggedWordCount === 1 ? '' : 's'} will be included in the review.`
              : 'There are no flagged words; the reviewer will see the full translation.'}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2 py-2">
          <Label htmlFor="reviewer-email">Reviewer email</Label>
          <Input
            id="reviewer-email"
            type="email"
            placeholder="reviewer@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            disabled={isLoading}
          />
        </div>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isLoading}
          >
            Cancel
          </Button>
          <Button
            type="button"
            onClick={() => onConfirm(email.trim())}
            disabled={isLoading || !isValidEmail}
          >
            {isLoading ? 'Sending…' : 'Send invitation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
