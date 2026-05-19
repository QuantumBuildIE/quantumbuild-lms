"use client";

import * as React from "react";
import { ChevronDown, ChevronUp, Download, Upload } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import {
  useUploadBulkImport,
  useDownloadBulkImportTemplate,
  type BulkImportUploadResponse,
} from "@/lib/api/admin/use-bulk-import";
import { useTenants } from "@/lib/api/admin/use-tenants";
import { getApiErrorMessage } from "@/lib/utils";

interface UploadPanelProps {
  onSuccess: (response: BulkImportUploadResponse) => void;
}

// Isolated so useTenants only runs when this component is mounted (SuperUser only).
function TenantSelect({
  value,
  onChange,
}: {
  value: string;
  onChange: (value: string) => void;
}) {
  const { data } = useTenants({ pageNumber: 1, pageSize: 200 });

  return (
    <div className="space-y-2">
      <Label htmlFor="target-tenant">Target Tenant</Label>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id="target-tenant">
          <SelectValue placeholder="Select a tenant..." />
        </SelectTrigger>
        <SelectContent>
          {data?.items.map((t) => (
            <SelectItem key={t.id} value={t.id}>
              {t.name}
              {t.companyName ? ` — ${t.companyName}` : ""}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">
        Select the tenant this import should be applied to.
      </p>
    </div>
  );
}

export function BulkImportUploadPanel({ onSuccess }: UploadPanelProps) {
  const isSuperUser = useIsSuperUser();
  const [file, setFile] = React.useState<File | null>(null);
  const [targetTenantId, setTargetTenantId] = React.useState("");
  const [showInstructions, setShowInstructions] = React.useState(false);
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  const uploadMutation = useUploadBulkImport();
  const downloadTemplate = useDownloadBulkImportTemplate();

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFile(e.target.files?.[0] ?? null);
  };

  const handleDownloadTemplate = async () => {
    try {
      await downloadTemplate.mutateAsync();
    } catch {
      toast.error("Failed to download template");
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;
    try {
      const response = await uploadMutation.mutateAsync({
        file,
        targetTenantId: isSuperUser && targetTenantId ? targetTenantId : undefined,
      });
      onSuccess(response);
    } catch (error) {
      toast.error("Upload failed", {
        description: getApiErrorMessage(error, "Failed to upload the CSV file."),
      });
    }
  };

  const canSubmit =
    file !== null &&
    !uploadMutation.isPending &&
    (!isSuperUser || targetTenantId !== "");

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>Import Employees</CardTitle>
        <CardDescription>
          Upload a CSV file to create multiple employee records at once.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Template download */}
          <div className="rounded-md border bg-muted/50 p-4 flex items-start justify-between gap-4">
            <div>
              <p className="text-sm font-medium">CSV Template</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                Download the template with required column headers and example rows
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={handleDownloadTemplate}
              disabled={downloadTemplate.isPending}
              className="shrink-0"
            >
              <Download className="mr-2 h-4 w-4" />
              {downloadTemplate.isPending ? "Downloading..." : "Download Template"}
            </Button>
          </div>

          {/* SuperUser tenant picker — only rendered when user is SuperUser */}
          {isSuperUser && (
            <TenantSelect value={targetTenantId} onChange={setTargetTenantId} />
          )}

          {/* File picker */}
          <div className="space-y-2">
            <Label>CSV File</Label>
            <div className="flex items-center gap-3">
              <input
                ref={fileInputRef}
                type="file"
                accept=".csv"
                onChange={handleFileChange}
                className="hidden"
              />
              <Button
                type="button"
                variant="outline"
                onClick={() => fileInputRef.current?.click()}
              >
                {file ? "Change file" : "Choose file"}
              </Button>
              <span className="text-sm text-muted-foreground truncate max-w-xs">
                {file ? file.name : "No file chosen"}
              </span>
            </div>
            <p className="text-xs text-muted-foreground">Accepts .csv files only</p>
          </div>

          {/* Collapsible instructions */}
          <div>
            <button
              type="button"
              onClick={() => setShowInstructions((v) => !v)}
              className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors"
            >
              {showInstructions ? (
                <ChevronUp className="h-3.5 w-3.5" />
              ) : (
                <ChevronDown className="h-3.5 w-3.5" />
              )}
              Import instructions
            </button>
            {showInstructions && (
              <div className="mt-3 rounded-md border bg-muted/30 p-4 text-sm space-y-3">
                <div>
                  <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground mb-1.5">
                    Required columns
                  </p>
                  <ul className="space-y-1 text-muted-foreground">
                    <li>
                      <span className="font-medium text-foreground">FirstName</span>,{" "}
                      <span className="font-medium text-foreground">LastName</span> — employee name
                    </li>
                    <li>
                      <span className="font-medium text-foreground">Email</span> — used for login;
                      must be unique
                    </li>
                  </ul>
                </div>
                <div>
                  <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground mb-1.5">
                    Optional columns
                  </p>
                  <ul className="space-y-1 text-muted-foreground">
                    <li>
                      <span className="font-medium text-foreground">JobTitle</span>,{" "}
                      <span className="font-medium text-foreground">Phone</span>,{" "}
                      <span className="font-medium text-foreground">Mobile</span>
                    </li>
                    <li>
                      <span className="font-medium text-foreground">SiteCode</span> — assigns the
                      employee to a site (must match an existing site code)
                    </li>
                    <li>
                      <span className="font-medium text-foreground">Role</span> — defaults to{" "}
                      <span className="font-medium text-foreground">Operator</span> if omitted
                    </li>
                    <li>
                      <span className="font-medium text-foreground">SendInvite</span> — true/false;
                      defaults to true; sends a set-password invitation email
                    </li>
                  </ul>
                </div>
                <p className="text-xs text-muted-foreground border-t pt-2">
                  Rows with validation errors are skipped. Valid and warning rows are still created.
                  You can correct failed rows and re-import.
                </p>
              </div>
            )}
          </div>

          {/* Submit */}
          <div className="flex justify-end pt-2">
            <Button type="submit" disabled={!canSubmit}>
              <Upload className="mr-2 h-4 w-4" />
              {uploadMutation.isPending ? "Uploading..." : "Upload & Validate"}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
