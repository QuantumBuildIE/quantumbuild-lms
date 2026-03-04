'use client';

import { useCallback, useRef, useState } from 'react';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { ImagePlus, X, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import type { ContentCreationSettings } from '@/types/content-creation';

interface TitleDescriptionPanelProps {
  settings: ContentCreationSettings;
  onChange: (settings: ContentCreationSettings) => void;
  onUploadCoverImage: (file: File) => void;
  isUploading?: boolean;
  isSaving?: boolean;
}

const ACCEPTED_TYPES = ['image/png', 'image/jpeg', 'image/jpg'];
const MAX_SIZE = 5 * 1024 * 1024; // 5MB

export function TitleDescriptionPanel({
  settings,
  onChange,
  onUploadCoverImage,
  isUploading,
  isSaving,
}: TitleDescriptionPanelProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);

  const handleFileSelect = useCallback(
    (file: File) => {
      if (!ACCEPTED_TYPES.includes(file.type)) {
        toast.error('Please select a PNG or JPG image');
        return;
      }
      if (file.size > MAX_SIZE) {
        toast.error('Image must be under 5MB');
        return;
      }
      onUploadCoverImage(file);
    },
    [onUploadCoverImage]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragOver(false);
      const file = e.dataTransfer.files[0];
      if (file) handleFileSelect(file);
    },
    [handleFileSelect]
  );

  const handleInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) handleFileSelect(file);
      // Reset input so same file can be selected again
      e.target.value = '';
    },
    [handleFileSelect]
  );

  return (
    <div className="rounded-lg border bg-muted/30 p-4 space-y-4">
      <h3 className="text-sm font-semibold">Title & Description</h3>

      {/* Title */}
      <div className="space-y-1.5">
        <Label htmlFor="settings-title" className="text-sm">
          Title *
        </Label>
        <Input
          id="settings-title"
          value={settings.title}
          onChange={(e) => onChange({ ...settings, title: e.target.value })}
          placeholder="Enter a title for this learning"
          disabled={isSaving}
        />
      </div>

      {/* Description */}
      <div className="space-y-1.5">
        <Label htmlFor="settings-description" className="text-sm">
          Description
        </Label>
        <Textarea
          id="settings-description"
          value={settings.description}
          onChange={(e) => onChange({ ...settings, description: e.target.value })}
          placeholder="Enter a description (optional)"
          rows={3}
          disabled={isSaving}
        />
      </div>

      {/* Cover Image */}
      <div className="space-y-1.5">
        <Label className="text-sm">Cover Image</Label>
        <p className="text-xs text-muted-foreground">
          PNG or JPG, up to 5MB. 16:9 recommended.
        </p>

        {settings.coverImageUrl ? (
          <div className="relative group w-full max-w-sm">
            <img
              src={settings.coverImageUrl}
              alt="Cover"
              className="rounded-md border object-cover w-full aspect-video"
            />
            <Button
              variant="destructive"
              size="icon"
              className="absolute top-2 right-2 h-7 w-7 opacity-0 group-hover:opacity-100 transition-opacity"
              onClick={() => onChange({ ...settings, coverImageUrl: null })}
              disabled={isSaving}
            >
              <X className="h-3.5 w-3.5" />
            </Button>
          </div>
        ) : (
          <div
            className={`
              flex flex-col items-center justify-center rounded-md border-2 border-dashed p-6 cursor-pointer
              transition-colors max-w-sm
              ${dragOver ? 'border-primary bg-primary/5' : 'border-muted-foreground/25 hover:border-muted-foreground/50'}
            `}
            onDragOver={(e) => {
              e.preventDefault();
              setDragOver(true);
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
          >
            {isUploading ? (
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            ) : (
              <>
                <ImagePlus className="h-8 w-8 text-muted-foreground mb-2" />
                <span className="text-sm text-muted-foreground">
                  Drop an image or click to browse
                </span>
              </>
            )}
          </div>
        )}

        <input
          ref={fileInputRef}
          type="file"
          accept=".png,.jpg,.jpeg"
          className="hidden"
          onChange={handleInputChange}
        />
      </div>
    </div>
  );
}
