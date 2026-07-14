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
import { Checkbox } from '@/components/ui/checkbox';

interface SendExternalReviewDialogSection {
  title: string;
}

interface SendExternalReviewDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (email: string, editableSectionIndices: number[]) => void;
  isLoading?: boolean;
  flaggedWordCount: number;
  languageName: string;
  sections: SendExternalReviewDialogSection[];
}

export function SendExternalReviewDialog({
  open,
  onOpenChange,
  onConfirm,
  isLoading = false,
  flaggedWordCount,
  languageName,
  sections,
}: SendExternalReviewDialogProps) {
  const [email, setEmail] = useState('');
  const [selectedIndices, setSelectedIndices] = useState<Set<number>>(new Set());

  useEffect(() => {
    if (open) {
      setEmail('');
      setSelectedIndices(new Set(sections.map((_, index) => index)));
    }
    // sections is derived from the talk and stable for the dialog's lifetime; re-running
    // this effect on every sections reference change would reset the admin's selection.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const isValidEmail = email.includes('@') && email.trim().length > 0;
  const hasSelection = selectedIndices.size > 0;

  const toggleSection = (index: number) => {
    setSelectedIndices((prev) => {
      const next = new Set(prev);
      if (next.has(index)) {
        next.delete(index);
      } else {
        next.add(index);
      }
      return next;
    });
  };

  const selectAll = () => setSelectedIndices(new Set(sections.map((_, index) => index)));
  const deselectAll = () => setSelectedIndices(new Set());

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Send for External Review</DialogTitle>
          <DialogDescription>
            Send the <span className="font-medium text-foreground">{languageName}</span> translation
            to a third-party reviewer. The reviewer will see the entire translation — only the
            sections you select below will be editable; the rest will be shown as read-only
            context.{' '}
            {flaggedWordCount > 0
              ? `${flaggedWordCount} flagged word${flaggedWordCount === 1 ? '' : 's'} will be included in the review.`
              : 'There are no flagged words in this translation.'}
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

        {sections.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label>Editable sections</Label>
              <span className="text-xs text-muted-foreground">
                {selectedIndices.size} of {sections.length} selected
              </span>
            </div>
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 px-2 text-xs"
                onClick={selectAll}
                disabled={isLoading}
              >
                Select all
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 px-2 text-xs"
                onClick={deselectAll}
                disabled={isLoading}
              >
                Deselect all
              </Button>
            </div>
            <div className="max-h-56 space-y-1 overflow-y-auto rounded-md border p-2">
              {sections.map((section, index) => (
                <label
                  key={index}
                  className="flex items-center gap-2 rounded-sm px-1 py-1.5 text-sm hover:bg-muted/50"
                >
                  <Checkbox
                    checked={selectedIndices.has(index)}
                    onCheckedChange={() => toggleSection(index)}
                    disabled={isLoading}
                  />
                  <span className="text-xs text-muted-foreground tabular-nums">{index + 1}.</span>
                  <span className="truncate">
                    {section.title || <span className="italic text-muted-foreground">Untitled section</span>}
                  </span>
                </label>
              ))}
            </div>
            {!hasSelection && (
              <p className="text-xs text-destructive">Select at least one section to send for review.</p>
            )}
          </div>
        )}

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
            onClick={() => onConfirm(email.trim(), Array.from(selectedIndices).sort((a, b) => a - b))}
            disabled={isLoading || !isValidEmail || !hasSelection}
          >
            {isLoading ? 'Sending…' : 'Send invitation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
