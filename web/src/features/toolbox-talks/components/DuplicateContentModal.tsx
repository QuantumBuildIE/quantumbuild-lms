'use client';

import { useState } from 'react';
import { FileText, Video, Copy, RefreshCw, CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import type { SourceToolboxTalkInfo, FileHashType } from '@/types/toolbox-talks';
import { format } from 'date-fns';

interface DuplicateContentModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sourceToolboxTalk: SourceToolboxTalkInfo;
  fileType: FileHashType;
  onReuseContent: () => Promise<void>;
  onGenerateFresh: () => Promise<void>;
}

export function DuplicateContentModal({
  open,
  onOpenChange,
  sourceToolboxTalk,
  fileType,
  onReuseContent,
  onGenerateFresh,
}: DuplicateContentModalProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [action, setAction] = useState<'reuse' | 'fresh' | null>(null);

  const handleReuseContent = async () => {
    setIsLoading(true);
    setAction('reuse');
    try {
      await onReuseContent();
      onOpenChange(false);
    } finally {
      setIsLoading(false);
      setAction(null);
    }
  };

  const handleGenerateFresh = async () => {
    setIsLoading(true);
    setAction('fresh');
    try {
      await onGenerateFresh();
      onOpenChange(false);
    } finally {
      setIsLoading(false);
      setAction(null);
    }
  };

  const formattedDate = sourceToolboxTalk.processedAt
    ? format(new Date(sourceToolboxTalk.processedAt), 'd MMM yyyy')
    : 'Unknown';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <CheckCircle className="h-5 w-5 text-blue-500" />
            Content Already Exists
          </DialogTitle>
          <DialogDescription>
            This file was previously processed for another Learning.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {/* Source Learning Info */}
          <div className="bg-muted rounded-lg p-4 space-y-3">
            <div className="flex items-start gap-3">
              {fileType === 'PDF' ? (
                <FileText className="h-5 w-5 text-red-500 mt-0.5" />
              ) : (
                <Video className="h-5 w-5 text-blue-500 mt-0.5" />
              )}
              <div className="flex-1 min-w-0">
                <p className="font-medium text-foreground truncate">
                  &quot;{sourceToolboxTalk.title}&quot;
                </p>
                <p className="text-sm text-muted-foreground">
                  Processed: {formattedDate}
                </p>
              </div>
            </div>

            {/* Content Stats */}
            <div className="flex flex-wrap gap-2">
              <Badge variant="secondary">
                {sourceToolboxTalk.sectionCount} Section{sourceToolboxTalk.sectionCount !== 1 ? 's' : ''}
              </Badge>
              <Badge variant="secondary">
                {sourceToolboxTalk.questionCount} Question{sourceToolboxTalk.questionCount !== 1 ? 's' : ''}
              </Badge>
            </div>

            {/* Translation Languages */}
            {sourceToolboxTalk.translationLanguages.length > 0 && (
              <div>
                <p className="text-sm text-muted-foreground mb-1">Translations:</p>
                <div className="flex flex-wrap gap-1.5">
                  {sourceToolboxTalk.translationLanguages.map((lang) => (
                    <Badge key={lang} variant="outline" className="text-xs">
                      {lang}
                    </Badge>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Question */}
          <p className="text-sm text-muted-foreground">
            Would you like to reuse this content or generate fresh content?
          </p>
        </div>

        <DialogFooter className="flex flex-col sm:flex-row gap-2">
          <Button
            variant="default"
            onClick={handleReuseContent}
            disabled={isLoading}
            className="w-full sm:w-auto"
          >
            {isLoading && action === 'reuse' ? (
              <>
                <RefreshCw className="mr-2 h-4 w-4 animate-spin" />
                Copying...
              </>
            ) : (
              <>
                <Copy className="mr-2 h-4 w-4" />
                Reuse Content (Recommended)
              </>
            )}
          </Button>
          <Button
            variant="outline"
            onClick={handleGenerateFresh}
            disabled={isLoading}
            className="w-full sm:w-auto"
          >
            {isLoading && action === 'fresh' ? (
              <>
                <RefreshCw className="mr-2 h-4 w-4 animate-spin" />
                Starting...
              </>
            ) : (
              <>
                <RefreshCw className="mr-2 h-4 w-4" />
                Generate Fresh (Uses AI credits)
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
