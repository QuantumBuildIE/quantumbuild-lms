"use client";

import * as React from "react";
import { Upload } from "lucide-react";
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
  useUploadBulkSopImport,
  type BulkSopImportUploadResponse,
} from "@/lib/api/toolbox-talks/use-bulk-sop-import";
import { useTenants } from "@/lib/api/admin/use-tenants";
import { getApiErrorMessage } from "@/lib/utils";

interface UploadPanelProps {
  onSuccess: (response: BulkSopImportUploadResponse) => void;
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

export function BulkSopImportUploadPanel({ onSuccess }: UploadPanelProps) {
  const isSuperUser = useIsSuperUser();
  const [file, setFile] = React.useState<File | null>(null);
  const [targetTenantId, setTargetTenantId] = React.useState("");
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  const uploadMutation = useUploadBulkSopImport();

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFile(e.target.files?.[0] ?? null);
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
        description: getApiErrorMessage(error, "Failed to upload the ZIP file."),
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
          <CardTitle>Import SOPs</CardTitle>
          <CardDescription>
            Upload a ZIP of SOP PDFs to create a Draft learning from each one.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-6">
            {/* SuperUser tenant picker — only rendered when user is SuperUser */}
            {isSuperUser && (
              <TenantSelect value={targetTenantId} onChange={setTargetTenantId} />
            )}

            {/* File picker */}
            <div className="space-y-2">
              <Label>ZIP File</Label>
              <div className="flex items-center gap-3">
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".zip"
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
              <p className="text-xs text-muted-foreground">
                Accepts a single .zip file, up to 200 MB, containing up to 50 PDFs
              </p>
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
          <CardDescription>What to include in your ZIP and how it's processed</CardDescription>
        </CardHeader>
        <CardContent className="space-y-5 text-sm">
          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground mb-2">
              What to include
            </p>
            <ul className="space-y-2 text-muted-foreground">
              <li>
                A flat or nested ZIP of <span className="font-medium text-foreground">.pdf</span>{" "}
                files — one PDF per SOP. Non-PDF entries (e.g.{" "}
                <span className="font-medium text-foreground">readme.txt</span>) are ignored, not
                treated as errors.
              </li>
              <li>
                Up to <span className="font-medium text-foreground">50 PDFs</span>, each up to{" "}
                <span className="font-medium text-foreground">50 MB</span>, with a{" "}
                <span className="font-medium text-foreground">500 MB</span> combined
                (uncompressed) limit and a <span className="font-medium text-foreground">200 MB</span>{" "}
                ZIP upload limit.
              </li>
            </ul>
          </div>

          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground mb-2">
              How each PDF is processed
            </p>
            <ul className="space-y-2 text-muted-foreground">
              <li>
                Each PDF is parsed and a quiz is generated automatically, the same steps a wizard
                user would run manually.
              </li>
              <li>
                Every PDF lands as a <span className="font-medium text-foreground">Draft</span>{" "}
                learning with no translations — review, translate, and publish afterward.
              </li>
              <li>
                If quiz generation fails for a PDF, the learning is still created with its parsed
                sections — you can add a quiz manually before publishing.
              </li>
            </ul>
          </div>

          <div className="space-y-2 text-muted-foreground border-t pt-4">
            <p>
              Validation happens before anything is generated — you'll see exactly what's in the
              ZIP and confirm before import starts.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
