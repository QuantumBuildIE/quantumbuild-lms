"use client";

import { useRef, useState } from "react";
import { FileText, Upload, ExternalLink } from "lucide-react";
import { toast } from "sonner";
import { useUploadRegulatoryDocument } from "@/lib/api/admin/use-regulatory-ingestion";

const MAX_SIZE_BYTES = 50 * 1024 * 1024; // 50 MB — matches the backend's MaxSourceDocumentSizeBytes
const ALLOWED_TYPES = ["application/pdf"];

interface RegulatoryDocumentUploadProps {
  documentId: string;
  currentSourceUrl: string | null;
  onUploaded: (sourceUrl: string, fileName: string) => void;
}

export function RegulatoryDocumentUpload({
  documentId,
  currentSourceUrl,
  onUploaded,
}: RegulatoryDocumentUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [uploadedFileName, setUploadedFileName] = useState<string | null>(null);

  const uploadMutation = useUploadRegulatoryDocument(documentId);

  function validateAndUpload(file: File) {
    if (!ALLOWED_TYPES.includes(file.type)) {
      toast.error("Only PDF files are accepted.");
      return;
    }
    if (file.size === 0) {
      toast.error("The selected file is empty.");
      return;
    }
    if (file.size > MAX_SIZE_BYTES) {
      toast.error("PDF must be 50 MB or smaller.");
      return;
    }
    uploadMutation.mutate(file, {
      onSuccess: (result) => {
        setUploadedFileName(result.fileName);
        onUploaded(result.sourceUrl, result.fileName);
        toast.success("PDF uploaded — Source URL updated.");
      },
      onError: () => toast.error("Failed to upload PDF. Please try again."),
    });
  }

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (file) validateAndUpload(file);
    // Reset so the same file can be re-selected after a failed attempt
    e.target.value = "";
  }

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) validateAndUpload(file);
  }

  const isBusy = uploadMutation.isPending;

  return (
    <div className="space-y-2">
      <div
        role="button"
        tabIndex={0}
        aria-label="Upload a source PDF — click or drag and drop"
        className={[
          "flex items-center gap-3 rounded-lg border-2 border-dashed px-4 py-3 transition-colors",
          isDragging ? "border-primary bg-primary/5" : "border-muted-foreground/25 hover:border-primary",
          isBusy ? "pointer-events-none opacity-50" : "cursor-pointer",
        ].join(" ")}
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            inputRef.current?.click();
          }
        }}
        onDragOver={(e) => {
          e.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
      >
        {isDragging ? (
          <Upload className="h-5 w-5 shrink-0 text-primary" />
        ) : (
          <FileText className="h-5 w-5 shrink-0 text-muted-foreground" />
        )}
        <div className="flex-1 text-sm">
          {isBusy ? (
            <span className="text-muted-foreground">Uploading…</span>
          ) : (
            <>
              <span className="font-medium">
                {isDragging ? "Drop to upload" : "Click or drag a PDF to upload"}
              </span>
              <span className="ml-2 text-xs text-muted-foreground">PDF · max 50 MB</span>
            </>
          )}
        </div>
        <input
          ref={inputRef}
          type="file"
          accept="application/pdf"
          className="sr-only"
          tabIndex={-1}
          aria-hidden="true"
          onChange={handleFileChange}
        />
      </div>

      {(uploadedFileName || currentSourceUrl) && (
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          {uploadedFileName ? (
            <span>Uploaded: {uploadedFileName}</span>
          ) : (
            <span>Current source is set</span>
          )}
          {currentSourceUrl && (
            <a
              href={currentSourceUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 text-primary hover:underline"
            >
              View <ExternalLink className="h-3 w-3" />
            </a>
          )}
        </div>
      )}
    </div>
  );
}
