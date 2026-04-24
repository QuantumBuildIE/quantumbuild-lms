"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
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
import { EmployeeCertificatesSection } from "@/components/admin/employee-certificates-section";
import { AssignedOperatorsSection } from "@/components/admin/assigned-operators-section";
import { useEmployee, useResetEmployeePin } from "@/lib/api/admin/use-employees";
import { useUser } from "@/lib/api/admin/use-users";
import { useLookupValues } from "@/hooks/use-lookups";
import { useTenantSettings } from "@/lib/api/admin/use-tenant-settings";
import { useEmployeeTrainingHistory } from "@/lib/api/toolbox-talks/use-qr-locations";
import { ChevronLeft, Pencil, KeyRound, ScanLine } from "lucide-react";
import { toast } from "sonner";
import { format } from "date-fns";
import { cn } from "@/lib/utils";

const CONTENT_MODE_LABELS: Record<string, string> = {
  ViewOnly: "View Only",
  Training: "Training",
  Induction: "Induction",
};

const CONTENT_MODE_COLOURS: Record<string, string> = {
  ViewOnly: "bg-slate-100 text-slate-700",
  Training: "bg-blue-100 text-blue-700",
  Induction: "bg-green-100 text-green-700",
};

function QrTrainingHistorySection({ employeeId }: { employeeId: string }) {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useEmployeeTrainingHistory(employeeId, { page, pageSize: 10 });
  const items = data?.items.filter((i) => i.type === "QrSession") ?? [];
  const total = items.length;

  if (isLoading) {
    return (
      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><ScanLine className="h-4 w-4" />QR Training History</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
        </CardContent>
      </Card>
    );
  }

  if (total === 0) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ScanLine className="h-4 w-4" />
          QR Training History
        </CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                {["Location", "Talk", "Mode", "Score", "Language", "Date"].map((h) => (
                  <th key={h} className="px-4 py-2 text-left text-xs font-medium text-muted-foreground whitespace-nowrap">
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.itemId} className="border-t hover:bg-muted/30 transition-colors">
                  <td className="px-4 py-2 text-muted-foreground whitespace-nowrap text-xs">
                    {item.locationName ?? "—"}
                  </td>
                  <td className="px-4 py-2 max-w-[200px] truncate" title={item.talkTitle}>
                    {item.talkTitle}
                  </td>
                  <td className="px-4 py-2">
                    {item.contentMode ? (
                      <Badge className={cn("text-xs", CONTENT_MODE_COLOURS[item.contentMode] ?? "bg-slate-100 text-slate-700")}>
                        {CONTENT_MODE_LABELS[item.contentMode] ?? item.contentMode}
                      </Badge>
                    ) : <span className="text-muted-foreground">—</span>}
                  </td>
                  <td className="px-4 py-2 text-center">
                    {item.score != null ? `${item.score}%` : <span className="text-muted-foreground">—</span>}
                  </td>
                  <td className="px-4 py-2 text-muted-foreground text-xs">
                    {item.language ? (item.language.toUpperCase()) : "—"}
                  </td>
                  <td className="px-4 py-2 text-muted-foreground whitespace-nowrap text-xs">
                    {format(new Date(item.completedAt), "dd MMM yyyy")}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {(data?.totalPages ?? 1) > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t text-xs text-muted-foreground">
            <span>{data?.totalCount} sessions</span>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="outline" className="h-6 px-2 text-xs" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</Button>
              <span>{page} / {data?.totalPages}</span>
              <Button size="sm" variant="outline" className="h-6 px-2 text-xs" disabled={page >= (data?.totalPages ?? 1)} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

export default function EmployeeDetailPage() {
  const params = useParams();
  const employeeId = params.id as string;

  const { data: employee, isLoading, error } = useEmployee(employeeId);
  const linkedUserId = employee?.linkedUserId ?? "";
  const { data: linkedUser } = useUser(linkedUserId);
  const isSupervisor = linkedUser?.roles?.some((r) => r.name === "Supervisor") ?? false;
  const { data: languages = [] } = useLookupValues("Language");
  const { data: settings } = useTenantSettings();
  const qrEnabled = settings?.["QrLocationTrainingEnabled"] === "true";
  const resetPinMutation = useResetEmployeePin();
  const [pinResetConfirmOpen, setPinResetConfirmOpen] = useState(false);

  const handleResetPin = async () => {
    setPinResetConfirmOpen(false);
    try {
      await resetPinMutation.mutateAsync(employeeId);
      toast.success("New PIN sent to employee's email");
    } catch {
      toast.error("Failed to reset PIN. Please try again.");
    }
  };

  const getLanguageName = (code: string) => {
    const lang = languages.find((l) => l.code === code);
    if (!lang) return code;
    const meta = lang.metadata ? JSON.parse(lang.metadata) : null;
    return meta?.nativeName ? `${lang.name} (${meta.nativeName})` : lang.name;
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/employees">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Employee</h1>
            <p className="text-muted-foreground">Loading employee details...</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="flex items-center justify-center">
              <div className="animate-pulse text-muted-foreground">Loading...</div>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !employee) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/employees">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Employee</h1>
            <p className="text-muted-foreground">Employee not found</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="text-center">
              <p className="text-destructive">
                Failed to load employee. The employee may have been deleted.
              </p>
              <Button className="mt-4" asChild>
                <Link href="/admin/employees">Back to Employees</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/employees">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {employee.firstName} {employee.lastName}
            </h1>
            <p className="text-muted-foreground">{employee.employeeCode}</p>
          </div>
          <Badge variant={employee.isActive ? "default" : "secondary"}>
            {employee.isActive ? "Active" : "Inactive"}
          </Badge>
        </div>
        <div className="flex items-center gap-2">
          {qrEnabled && (
            <Button
              variant="outline"
              onClick={() => setPinResetConfirmOpen(true)}
              disabled={resetPinMutation.isPending}
            >
              <KeyRound className="h-4 w-4 mr-2" />
              Reset Workstation PIN
            </Button>
          )}
          <Button asChild>
            <Link href={`/admin/employees/${employeeId}/edit`}>
              <Pencil className="h-4 w-4 mr-2" />
              Edit
            </Link>
          </Button>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Employee Details</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-4">
            <DetailItem label="First Name" value={employee.firstName} />
            <DetailItem label="Last Name" value={employee.lastName} />
            <DetailItem label="Employee Code" value={employee.employeeCode} />
            <DetailItem label="Email" value={employee.email} />
            <DetailItem label="Phone" value={employee.phone} />
            <DetailItem label="Mobile" value={employee.mobile} />
            <DetailItem label="Job Title" value={employee.jobTitle} />
            <DetailItem label="Department" value={employee.department} />
            <DetailItem label="Default Site" value={employee.primarySiteName} />
            <DetailItem
              label="Preferred Language"
              value={employee.preferredLanguage ? getLanguageName(employee.preferredLanguage) : undefined}
            />
            <DetailItem
              label="Start Date"
              value={employee.startDate ? format(new Date(employee.startDate), "MMM d, yyyy") : undefined}
            />
            <DetailItem
              label="End Date"
              value={employee.endDate ? format(new Date(employee.endDate), "MMM d, yyyy") : undefined}
            />
            {employee.notes && (
              <div className="sm:col-span-2">
                <DetailItem label="Notes" value={employee.notes} />
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      <div className="max-w-2xl">
        <EmployeeCertificatesSection employeeId={employeeId} />
      </div>

      {isSupervisor && (
        <div className="max-w-2xl">
          <AssignedOperatorsSection employeeId={employeeId} />
        </div>
      )}

      {qrEnabled && (
        <div className="max-w-2xl">
          <QrTrainingHistorySection employeeId={employeeId} />
        </div>
      )}

      <AlertDialog open={pinResetConfirmOpen} onOpenChange={setPinResetConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Reset Workstation PIN?</AlertDialogTitle>
            <AlertDialogDescription>
              This will generate a new PIN and email it to{" "}
              <strong>
                {employee.firstName} {employee.lastName}
              </strong>{" "}
              at <strong>{employee.email}</strong>. Their current PIN will stop working immediately.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleResetPin}>
              Reset PIN &amp; Send Email
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function DetailItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <dt className="text-sm font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-sm">
        {value || <span className="text-muted-foreground">-</span>}
      </dd>
    </div>
  );
}
