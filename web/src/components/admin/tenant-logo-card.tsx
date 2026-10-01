"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { BrandLockup } from "@/components/branding/brand-lockup";
import { getBrandingErrorMessage } from "@/lib/api/branding/branding";
import {
  useDeleteTenantLogo,
  useTenantLogo,
  useUploadTenantLogo,
} from "@/lib/api/branding/use-branding";
import { getLogoWarnings, validateLogoFile } from "@/lib/api/branding/logo-validation";
import { AlertTriangle } from "lucide-react";
import { toast } from "sonner";

interface TenantLogoCardProps {
  /** The tenant being edited. Never the SuperUser's active tenant. */
  tenantId: string;
  tenantName: string;
}

interface PendingLogo {
  file: File;
  previewUrl: string;
  warnings: string[];
}

export function TenantLogoCard({ tenantId, tenantName }: TenantLogoCardProps) {
  const { data: branding, isLoading } = useTenantLogo(tenantId);
  const upload = useUploadTenantLogo(tenantId);
  const remove = useDeleteTenantLogo(tenantId);
  const inputRef = useRef<HTMLInputElement>(null);
  const [pending, setPending] = useState<PendingLogo | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);

  const currentLogoUrl = branding?.logoUrl ?? null;

  // Release the object URL when the pending file changes or the card unmounts.
  useEffect(() => {
    return () => {
      if (pending) URL.revokeObjectURL(pending.previewUrl);
    };
  }, [pending]);

  const clearInput = () => {
    if (inputRef.current) inputRef.current.value = "";
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    setFileError(null);
    setPending(null);
    if (!file) return;

    const error = validateLogoFile(file);
    if (error) {
      setFileError(error);
      clearInput();
      return;
    }

    const previewUrl = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      setPending({
        file,
        previewUrl,
        warnings: getLogoWarnings(img.naturalWidth, img.naturalHeight),
      });
    };
    img.onerror = () => {
      URL.revokeObjectURL(previewUrl);
      setFileError("This file could not be read as an image.");
      clearInput();
    };
    img.src = previewUrl;
  };

  const handleCancelPending = () => {
    setPending(null);
    setFileError(null);
    clearInput();
  };

  const handleUpload = async () => {
    if (!pending) return;
    try {
      await upload.mutateAsync(pending.file);
      toast.success("Logo updated");
      handleCancelPending();
    } catch (error) {
      setFileError(getBrandingErrorMessage(error, "Failed to upload logo"));
    }
  };

  const handleRemove = async () => {
    try {
      await remove.mutateAsync();
      toast.success("Logo removed");
    } catch (error) {
      toast.error(getBrandingErrorMessage(error, "Failed to remove logo"));
    } finally {
      setConfirmRemove(false);
    }
  };

  const previewLogoUrl = pending?.previewUrl ?? currentLogoUrl;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Logo</CardTitle>
        <CardDescription>
          Shown in the header for this tenant&apos;s users, with a &lsquo;Powered by
          CertifiedIQ&rsquo; badge.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">
          Use a horizontal logo, as a PNG with a transparent background, at least 80px tall
          (ideally 400x100 to 800x200), max 1 MB.
        </p>

        <div>
          <p className="mb-1.5 text-xs font-medium text-muted-foreground">
            {pending ? "Preview (not saved yet)" : "Header preview"}
          </p>
          <div className="flex h-14 items-center overflow-hidden rounded-md border bg-background px-4">
            {/* key resets the lockup's broken-image state when the URL changes */}
            <BrandLockup
              key={previewLogoUrl ?? "none"}
              logoUrl={previewLogoUrl}
              alt={tenantName}
              isLoading={isLoading && !pending}
            />
          </div>
        </div>

        {pending && pending.warnings.length > 0 && (
          <div className="space-y-1 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-200">
            {pending.warnings.map((w) => (
              <p key={w} className="flex items-start gap-2">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>{w}</span>
              </p>
            ))}
          </div>
        )}

        {fileError && (
          <p role="alert" className="text-sm text-destructive">
            {fileError}
          </p>
        )}

        <input
          ref={inputRef}
          type="file"
          accept="image/png,image/jpeg"
          className="hidden"
          onChange={handleFileChange}
          data-testid="logo-file-input"
        />

        <div className="flex flex-wrap items-center gap-2">
          {pending ? (
            <>
              <Button onClick={handleUpload} disabled={upload.isPending}>
                {upload.isPending ? "Uploading..." : "Upload logo"}
              </Button>
              <Button variant="outline" onClick={handleCancelPending} disabled={upload.isPending}>
                Cancel
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={() => inputRef.current?.click()}>
                {currentLogoUrl ? "Replace logo" : "Choose logo"}
              </Button>
              {currentLogoUrl && (
                <Button
                  variant="outline"
                  className="text-destructive"
                  onClick={() => setConfirmRemove(true)}
                  disabled={remove.isPending}
                >
                  Remove
                </Button>
              )}
            </>
          )}
        </div>
      </CardContent>

      <AlertDialog open={confirmRemove} onOpenChange={setConfirmRemove}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove logo?</AlertDialogTitle>
            <AlertDialogDescription>
              {tenantName} will go back to showing the CertifiedIQ logo in the header.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleRemove}>Remove</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}
