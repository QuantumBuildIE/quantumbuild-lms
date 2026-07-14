'use client';

import { useRef, useState } from 'react';
import { ImageIcon, Upload, X } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { useUploadCoverImage } from '../hooks/useUploadCoverImage';
import { useRemoveCoverImage } from '../hooks/useRemoveCoverImage';

const MAX_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB
const ALLOWED_TYPES = ['image/png', 'image/jpeg'];

interface CoverImageUploadProps {
  talkId: string;
  currentUrl: string | null;
}

export function CoverImageUpload({ talkId, currentUrl }: CoverImageUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);

  const uploadMutation = useUploadCoverImage(talkId);
  const removeMutation = useRemoveCoverImage(talkId);

  function validateAndUpload(file: File) {
    if (!ALLOWED_TYPES.includes(file.type)) {
      toast.error('Only PNG and JPEG images are accepted.');
      return;
    }
    if (file.size > MAX_SIZE_BYTES) {
      toast.error('Image must be 5 MB or smaller.');
      return;
    }
    uploadMutation.mutate(file, {
      onSuccess: () => toast.success('Cover image uploaded.'),
      onError: () => toast.error('Failed to upload cover image. Please try again.'),
    });
  }

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (file) validateAndUpload(file);
    // Reset so the same file can be re-selected after a remove
    e.target.value = '';
  }

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) validateAndUpload(file);
  }

  function handleRemove() {
    removeMutation.mutate(undefined, {
      onSuccess: () => toast.success('Cover image removed.'),
      onError: () => toast.error('Failed to remove cover image. Please try again.'),
    });
  }

  const isBusy = uploadMutation.isPending || removeMutation.isPending;

  if (currentUrl) {
    return (
      <div className="relative inline-block">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={currentUrl}
          alt="Cover image preview"
          className="h-40 w-full max-w-xs rounded-lg border object-cover"
        />
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button
              type="button"
              variant="destructive"
              size="icon"
              className="absolute right-2 top-2 h-7 w-7"
              aria-label="Remove cover image"
              disabled={isBusy}
            >
              <X className="h-4 w-4" />
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Remove cover image?</AlertDialogTitle>
              <AlertDialogDescription>
                The image will be permanently deleted from storage.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={handleRemove} disabled={isBusy}>
                Remove
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    );
  }

  return (
    <div
      role="button"
      tabIndex={0}
      aria-label="Upload cover image — click or drag and drop a PNG or JPEG"
      className={[
        'flex flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed p-8 transition-colors',
        isDragging ? 'border-primary bg-primary/5' : 'border-muted-foreground/25 hover:border-primary',
        isBusy ? 'pointer-events-none opacity-50' : 'cursor-pointer',
      ].join(' ')}
      onClick={() => inputRef.current?.click()}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          inputRef.current?.click();
        }
      }}
      onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
      onDragLeave={() => setIsDragging(false)}
      onDrop={handleDrop}
    >
      {isBusy ? (
        <span className="text-sm text-muted-foreground">Uploading…</span>
      ) : (
        <>
          <div className="rounded-full bg-muted p-3">
            {isDragging ? (
              <Upload className="h-5 w-5 text-primary" />
            ) : (
              <ImageIcon className="h-5 w-5 text-muted-foreground" />
            )}
          </div>
          <p className="text-sm font-medium">
            {isDragging ? 'Drop to upload' : 'Click or drag to upload'}
          </p>
          <p className="text-xs text-muted-foreground">PNG or JPEG · max 5 MB</p>
        </>
      )}
      <input
        ref={inputRef}
        type="file"
        accept="image/png,image/jpeg"
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        onChange={handleFileChange}
      />
    </div>
  );
}
