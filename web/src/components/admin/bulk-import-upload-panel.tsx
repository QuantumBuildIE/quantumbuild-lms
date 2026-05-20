"use client";

import * as React from "react";
import { Download, Upload } from "lucide-react";
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
    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
      {/* Left column: form */}
      <Card>
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
                  Download the template with all column headers and example rows
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

      {/* Right column: instructions (always visible) */}
      <Card>
        <CardHeader>
          <CardTitle>Import Instructions</CardTitle>
          <CardDescription>
            What to include in your CSV and how each column works
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-5 text-sm">
          {/* Required columns */}
          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground mb-2">
              Required columns
            </p>
            <ul className="space-y-2 text-muted-foreground">
              <li>
                <span className="font-medium text-foreground">FirstName</span>,{" "}
                <span className="font-medium text-foreground">LastName</span>{" "}
                — employee name
              </li>
              <li>
                <span className="font-medium text-foreground">Email</span>{" "}
                — required on every row regardless of account type. An email already
                registered as a user anywhere in the system cannot be reused — that
                row will fail.
              </li>
              <li>
                <span className="font-medium text-foreground">CreateUserAccount</span>{" "}
                — <span className="font-medium text-foreground">Yes</span> or{" "}
                <span className="font-medium text-foreground">No</span> (default{" "}
                <span className="font-medium text-foreground">Yes</span>).{" "}
                <span className="font-medium text-foreground">Yes</span> creates a
                login and sends an invitation email so the employee can set their
                password.{" "}
                <span className="font-medium text-foreground">No</span> creates an
                employee record only — the employee still receives training
                notifications at their email address.
              </li>
            </ul>
          </div>

          {/* Optional columns */}
          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground mb-2">
              Optional columns
            </p>
            <ul className="space-y-2 text-muted-foreground">
              <li>
                <span className="font-medium text-foreground">
                  Phone, Mobile, JobTitle, Department, Notes
                </span>{" "}
                — contact and profile details
              </li>
              <li>
                <span className="font-medium text-foreground">
                  StartDate, EndDate
                </span>{" "}
                — employment dates in{" "}
                <span className="font-medium text-foreground">YYYY-MM-DD</span>{" "}
                format; end date must be after start date
              </li>
              <li>
                <span className="font-medium text-foreground">PreferredLanguage</span>{" "}
                — 2-letter ISO code (e.g.{" "}
                <span className="font-medium text-foreground">en</span>,{" "}
                <span className="font-medium text-foreground">fr</span>,{" "}
                <span className="font-medium text-foreground">pl</span>); blank or
                unrecognised defaults to{" "}
                <span className="font-medium text-foreground">en</span>
              </li>
              <li>
                <span className="font-medium text-foreground">UserRole</span>{" "}
                — accepts{" "}
                <span className="font-medium text-foreground">Operator</span> or{" "}
                <span className="font-medium text-foreground">Supervisor</span>;
                blank or any other value defaults to{" "}
                <span className="font-medium text-foreground">Operator</span>
              </li>
            </ul>
          </div>

          {/* Processing notes */}
          <div className="space-y-2 text-muted-foreground border-t pt-4">
            <p>
              Employee codes are generated automatically — do not include them as a
              column in your CSV.
            </p>
            <p>
              Imported employees are active immediately and will appear in compliance
              and training reports straight away, including before their first login.
            </p>
            <p>
              Rows with errors are skipped without blocking the rest of the import.
              After the job completes you can download a CSV of just the failed rows,
              correct them, and re-upload.
            </p>
            <p>
              Site assignment is not part of the import — assign sites afterward in
              the employee record.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
