'use client';

import { useState, useEffect, useCallback } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { format } from 'date-fns';
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  GitCommitHorizontal,
  BookOpen,
  ShieldCheck,
  Hash,
  Plus,
  ChevronRight,
  Info,
  Lock,
} from 'lucide-react';
import { useAuth } from '@/lib/auth/use-auth';
import { toast } from 'sonner';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  usePipelineAuditDashboard,
  useDeviations,
  useDeviation,
  useModuleOutcomes,
  useChangeRecords,
  useCreateDeviation,
  useUpdateDeviationStatus,
  useCreateChangeRecord,
  useTermGateSummary,
  useTermGateCheck,
} from '@/lib/api/toolbox-talks/use-pipeline-audit';
import type {
  TranslationDeviationDto,
  CreateDeviationRequest,
  CreatePipelineChangeRecordRequest,
  PipelineChangeRecordDto,
  ModuleOutcomeDto,
  TermGateCheckResult,
  TermGateFailure,
} from '@/lib/api/toolbox-talks/pipeline-audit';

// ─── Shared helpers ────────────────────────────────────────────────────────────

function OutcomeBadge({ outcome }: { outcome: string }) {
  if (outcome === 'Pass') {
    return (
      <Badge className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400">
        Pass
      </Badge>
    );
  }
  if (outcome === 'Review') {
    return (
      <Badge className="bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400">
        Review
      </Badge>
    );
  }
  return (
    <Badge className="bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400">
      Fail
    </Badge>
  );
}

function DeviationStatusBadge({ status }: { status: string }) {
  if (status === 'Closed') {
    return (
      <Badge className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400">
        <CheckCircle2 className="mr-1 h-3 w-3" />
        Closed
      </Badge>
    );
  }
  if (status === 'InProgress') {
    return (
      <Badge className="bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400">
        <Clock className="mr-1 h-3 w-3" />
        In Progress
      </Badge>
    );
  }
  return (
    <Badge className="bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400">
      <AlertTriangle className="mr-1 h-3 w-3" />
      Open
    </Badge>
  );
}

// ─── Deviation prefill type (mirrors ValidationSectionCard + term gate) ───────

interface DeviationPrefill {
  validationRunId?: string;
  validationResultId?: string;
  moduleRef?: string;
  lessonRef?: string;
  languagePair?: string;
  sourceExcerpt?: string;
  targetExcerpt?: string;
  // Pre-fill nature/rootCauseCategory from term gate failures
  nature?: string;
  rootCauseCategory?: string;
}

// ─── New Deviation Dialog ─────────────────────────────────────────────────────

interface NewDeviationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  prefill?: DeviationPrefill;
}

function NewDeviationDialog({ open, onOpenChange, prefill }: NewDeviationDialogProps) {
  const createMutation = useCreateDeviation();
  const [form, setForm] = useState<Partial<CreateDeviationRequest>>({});

  useEffect(() => {
    if (open) {
      setForm({
        validationRunId: prefill?.validationRunId,
        validationResultId: prefill?.validationResultId,
        moduleRef: prefill?.moduleRef ?? '',
        lessonRef: prefill?.lessonRef ?? '',
        languagePair: prefill?.languagePair ?? '',
        sourceExcerpt: prefill?.sourceExcerpt ?? '',
        targetExcerpt: prefill?.targetExcerpt ?? '',
        detectedBy: '',
        nature: prefill?.nature ?? '',
        rootCauseCategory: prefill?.rootCauseCategory ?? '',
        rootCauseDetail: '',
        correctiveAction: '',
        preventiveAction: '',
        approver: '',
      });
    }
  }, [open, prefill]);

  const set = (key: keyof CreateDeviationRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  const handleSubmit = () => {
    if (!form.detectedBy || !form.nature || !form.rootCauseCategory) {
      toast.error('Please fill in all required fields');
      return;
    }
    createMutation.mutate(form as CreateDeviationRequest, {
      onSuccess: () => {
        toast.success('Deviation logged');
        onOpenChange(false);
      },
      onError: () => toast.error('Failed to log deviation'),
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Log Translation Deviation</DialogTitle>
          <DialogDescription>
            Record a quality deviation for audit purposes.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          {/* Context fields (pre-filled, read-only when from flag) */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label>Module Ref</Label>
              <Input
                value={form.moduleRef ?? ''}
                onChange={(e) => set('moduleRef', e.target.value)}
                placeholder="e.g. Manual Handling Safety"
              />
            </div>
            <div className="space-y-1.5">
              <Label>Lesson / Section Ref</Label>
              <Input
                value={form.lessonRef ?? ''}
                onChange={(e) => set('lessonRef', e.target.value)}
                placeholder="e.g. Section 2"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label>Language Pair</Label>
              <Input
                value={form.languagePair ?? ''}
                onChange={(e) => set('languagePair', e.target.value)}
                placeholder="e.g. EN→PL"
              />
            </div>
            <div className="space-y-1.5">
              <Label>
                Detected By <span className="text-destructive">*</span>
              </Label>
              <Input
                value={form.detectedBy ?? ''}
                onChange={(e) => set('detectedBy', e.target.value)}
                placeholder="Your name"
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>Source Excerpt</Label>
            <Textarea
              value={form.sourceExcerpt ?? ''}
              onChange={(e) => set('sourceExcerpt', e.target.value)}
              placeholder="Paste the relevant source text"
              rows={2}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Target Excerpt</Label>
            <Textarea
              value={form.targetExcerpt ?? ''}
              onChange={(e) => set('targetExcerpt', e.target.value)}
              placeholder="Paste the problematic translation"
              rows={2}
            />
          </div>

          <div className="space-y-1.5">
            <Label>
              Nature of Deviation <span className="text-destructive">*</span>
            </Label>
            <Input
              value={form.nature ?? ''}
              onChange={(e) => set('nature', e.target.value)}
              placeholder="e.g. Mistranslation, Omission, Terminology error"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label>
                Root Cause Category <span className="text-destructive">*</span>
              </Label>
              <Input
                value={form.rootCauseCategory ?? ''}
                onChange={(e) => set('rootCauseCategory', e.target.value)}
                placeholder="e.g. Provider error, Glossary gap"
              />
            </div>
            <div className="space-y-1.5">
              <Label>Root Cause Detail</Label>
              <Input
                value={form.rootCauseDetail ?? ''}
                onChange={(e) => set('rootCauseDetail', e.target.value)}
                placeholder="Optional additional detail"
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>Corrective Action</Label>
            <Textarea
              value={form.correctiveAction ?? ''}
              onChange={(e) => set('correctiveAction', e.target.value)}
              placeholder="What was done to fix this specific issue?"
              rows={2}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Preventive Action</Label>
            <Textarea
              value={form.preventiveAction ?? ''}
              onChange={(e) => set('preventiveAction', e.target.value)}
              placeholder="What process change prevents recurrence?"
              rows={2}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Approver</Label>
            <Input
              value={form.approver ?? ''}
              onChange={(e) => set('approver', e.target.value)}
              placeholder="Name of approving person (optional)"
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createMutation.isPending}>
            {createMutation.isPending ? 'Logging…' : 'Log Deviation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Deviation Detail Dialog ──────────────────────────────────────────────────

function DeviationDetailDialog({
  deviationId,
  open,
  onOpenChange,
}: {
  deviationId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { data: deviation, isLoading } = useDeviation(open ? deviationId : null);
  const updateMutation = useUpdateDeviationStatus();

  const handleStatusChange = (status: string) => {
    if (!deviationId) return;
    updateMutation.mutate(
      { id: deviationId, request: { status } },
      {
        onSuccess: () => toast.success('Status updated'),
        onError: () => toast.error('Failed to update status'),
      }
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {deviation ? `Deviation ${deviation.deviationId}` : 'Deviation Detail'}
          </DialogTitle>
          {deviation && <DeviationStatusBadge status={deviation.status} />}
        </DialogHeader>

        {isLoading ? (
          <div className="space-y-3 py-4">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-full" />
          </div>
        ) : deviation ? (
          <div className="space-y-4 py-2 text-sm">
            <div className="grid grid-cols-2 gap-x-6 gap-y-3">
              <Field label="Detected By" value={deviation.detectedBy} />
              <Field label="Detected At" value={format(new Date(deviation.detectedAt), 'dd MMM yyyy HH:mm')} />
              <Field label="Module" value={deviation.moduleRef} />
              <Field label="Section / Lesson" value={deviation.lessonRef} />
              <Field label="Language Pair" value={deviation.languagePair} />
              <Field label="Pipeline Version" value={deviation.pipelineVersionAtTime} />
            </div>

            {(deviation.sourceExcerpt || deviation.targetExcerpt) && (
              <div className="space-y-2">
                {deviation.sourceExcerpt && (
                  <div>
                    <p className="text-xs font-medium text-muted-foreground uppercase mb-1">Source</p>
                    <p className="rounded-md bg-muted/50 px-3 py-2 text-sm">{deviation.sourceExcerpt}</p>
                  </div>
                )}
                {deviation.targetExcerpt && (
                  <div>
                    <p className="text-xs font-medium text-muted-foreground uppercase mb-1">Target</p>
                    <p className="rounded-md bg-muted/50 px-3 py-2 text-sm">{deviation.targetExcerpt}</p>
                  </div>
                )}
              </div>
            )}

            <div className="grid grid-cols-2 gap-x-6 gap-y-3">
              <Field label="Nature" value={deviation.nature} />
              <Field label="Root Cause" value={deviation.rootCauseCategory} />
              {deviation.rootCauseDetail && (
                <Field label="Root Cause Detail" value={deviation.rootCauseDetail} className="col-span-2" />
              )}
            </div>

            {deviation.correctiveAction && (
              <Field label="Corrective Action" value={deviation.correctiveAction} multiline />
            )}
            {deviation.preventiveAction && (
              <Field label="Preventive Action" value={deviation.preventiveAction} multiline />
            )}

            {deviation.approver && <Field label="Approver" value={deviation.approver} />}

            {deviation.closedBy && (
              <div className="grid grid-cols-2 gap-x-6">
                <Field label="Closed By" value={deviation.closedBy} />
                {deviation.closedAt && (
                  <Field label="Closed At" value={format(new Date(deviation.closedAt), 'dd MMM yyyy HH:mm')} />
                )}
              </div>
            )}

            {/* Status update */}
            {deviation.status !== 'Closed' && (
              <div className="border-t pt-4">
                <p className="text-xs font-medium text-muted-foreground uppercase mb-2">Update Status</p>
                <div className="flex items-center gap-2">
                  {deviation.status === 'Open' && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleStatusChange('InProgress')}
                      disabled={updateMutation.isPending}
                    >
                      Mark In Progress
                    </Button>
                  )}
                  <Button
                    size="sm"
                    onClick={() => handleStatusChange('Closed')}
                    disabled={updateMutation.isPending}
                  >
                    Close Deviation
                  </Button>
                </div>
              </div>
            )}
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

function Field({
  label,
  value,
  multiline,
  className,
}: {
  label: string;
  value?: string | null;
  multiline?: boolean;
  className?: string;
}) {
  if (!value) return null;
  return (
    <div className={className}>
      <p className="text-xs font-medium text-muted-foreground uppercase mb-0.5">{label}</p>
      {multiline ? (
        <p className="text-sm rounded-md bg-muted/50 px-3 py-2">{value}</p>
      ) : (
        <p className="text-sm">{value}</p>
      )}
    </div>
  );
}

// ─── Dashboard Tab ────────────────────────────────────────────────────────────

function DashboardTab() {
  const { data: dashboard, isLoading } = usePipelineAuditDashboard();

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {[1, 2, 3, 4, 5, 6, 7].map((i) => <Skeleton key={i} className="h-24" />)}
        </div>
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (!dashboard) return null;

  return (
    <div className="space-y-6">
      {/* Stat cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {/* Deviations */}
        <StatCard
          label="Open Deviations"
          value={dashboard.openDeviations}
          valueClass="text-red-600"
          icon={<AlertTriangle className="h-5 w-5 text-red-400" />}
        />
        <StatCard
          label="In Progress"
          value={dashboard.inProgressDeviations}
          valueClass="text-amber-600"
          icon={<Clock className="h-5 w-5 text-amber-400" />}
        />
        <StatCard
          label="Closed Deviations"
          value={dashboard.closedDeviations}
          valueClass="text-green-600"
          icon={<CheckCircle2 className="h-5 w-5 text-green-400" />}
        />
        <StatCard
          label="Change Records"
          value={dashboard.changeRecords}
          valueClass="text-blue-600"
          icon={<GitCommitHorizontal className="h-5 w-5 text-blue-400" />}
        />
        <StatCard
          label="Locked Terms"
          value={dashboard.lockedTerms}
          valueClass="text-blue-600"
          icon={<Lock className="h-5 w-5 text-blue-400" />}
        />
        <StatCard
          label="Module Outcomes"
          value={dashboard.moduleOutcomes}
          valueClass="text-green-600"
          icon={<BookOpen className="h-5 w-5 text-green-400" />}
        />
        {/* Active Pipeline Version */}
        <Card className="sm:col-span-2 lg:col-span-2">
          <CardHeader className="pb-2">
            <CardDescription className="flex items-center gap-1.5">
              <ShieldCheck className="h-3.5 w-3.5" />
              Active Pipeline Version
            </CardDescription>
            <CardTitle className="text-lg font-semibold">
              {dashboard.activePipelineVersion}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <code className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Hash className="h-3 w-3" />
              {dashboard.activePipelineHash}
            </code>
          </CardContent>
        </Card>
      </div>

      {/* Most recent change record */}
      {dashboard.mostRecentChangeRecord && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Most Recent Change Record</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 text-sm">
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase">ID</p>
                <p className="font-mono">{dashboard.mostRecentChangeRecord.changeId}</p>
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase">Component</p>
                <p>{dashboard.mostRecentChangeRecord.component}</p>
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase">Change</p>
                <p className="truncate">
                  {dashboard.mostRecentChangeRecord.changeFrom} → {dashboard.mostRecentChangeRecord.changeTo}
                </p>
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase">Deployed</p>
                <p>{format(new Date(dashboard.mostRecentChangeRecord.deployedAt), 'dd MMM yyyy')}</p>
              </div>
              {dashboard.mostRecentChangeRecord.justification && (
                <div className="sm:col-span-2 lg:col-span-4">
                  <p className="text-xs font-medium text-muted-foreground uppercase">Justification</p>
                  <p>{dashboard.mostRecentChangeRecord.justification}</p>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Top open deviations */}
      {dashboard.topOpenDeviations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Top Open Deviations</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="divide-y">
              {dashboard.topOpenDeviations.map((d) => (
                <div key={d.id} className="flex items-center justify-between gap-4 py-3">
                  <div className="flex items-center gap-3 min-w-0">
                    <span className="font-mono text-xs text-muted-foreground shrink-0">{d.deviationId}</span>
                    <div className="min-w-0">
                      <p className="text-sm font-medium truncate">{d.nature}</p>
                      <p className="text-xs text-muted-foreground">
                        {d.moduleRef || '—'} · {d.languagePair || '—'}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <span className="text-xs text-muted-foreground">
                      {format(new Date(d.detectedAt), 'dd MMM yyyy')}
                    </span>
                    <DeviationStatusBadge status={d.status} />
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function StatCard({
  label,
  value,
  valueClass,
  icon,
}: {
  label: string;
  value: number;
  valueClass: string;
  icon: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardDescription className="flex items-center justify-between">
          {label}
          {icon}
        </CardDescription>
        <CardTitle className={`text-2xl ${valueClass}`}>{value}</CardTitle>
      </CardHeader>
    </Card>
  );
}

// ─── Deviations Tab ───────────────────────────────────────────────────────────

function DeviationsTab({
  initialOpenNew,
  prefill,
}: {
  initialOpenNew: boolean;
  prefill?: DeviationPrefill;
}) {
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page] = useState(1);
  const [newOpen, setNewOpen] = useState(initialOpenNew);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);

  useEffect(() => {
    if (initialOpenNew) setNewOpen(true);
  }, [initialOpenNew]);

  const params = {
    status: statusFilter === 'all' ? undefined : statusFilter,
    page,
    pageSize: 20,
  };

  const { data, isLoading } = useDeviations(params);

  const openDetail = (id: string) => {
    setSelectedId(id);
    setDetailOpen(true);
  };

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-2">
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All</SelectItem>
              <SelectItem value="Open">Open</SelectItem>
              <SelectItem value="InProgress">In Progress</SelectItem>
              <SelectItem value="Closed">Closed</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <Button onClick={() => setNewOpen(true)}>
          <Plus className="mr-2 h-4 w-4" />
          New Deviation
        </Button>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="space-y-2">
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-14 w-full" />)}
        </div>
      ) : !data || data.items.length === 0 ? (
        <Card className="p-8 text-center">
          <div className="flex flex-col items-center gap-2">
            <CheckCircle2 className="h-8 w-8 text-muted-foreground" />
            <p className="text-muted-foreground">No deviations found.</p>
          </div>
        </Card>
      ) : (
        <div className="rounded-md border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">ID</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Detected</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Module</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Nature</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Root Cause</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {data.items.map((d: TranslationDeviationDto) => (
                <tr
                  key={d.id}
                  className="hover:bg-muted/30 cursor-pointer transition-colors"
                  onClick={() => openDetail(d.id)}
                >
                  <td className="px-4 py-3 font-mono text-xs">{d.deviationId}</td>
                  <td className="px-4 py-3 text-muted-foreground whitespace-nowrap">
                    {format(new Date(d.detectedAt), 'dd MMM yyyy')}
                  </td>
                  <td className="px-4 py-3 max-w-[180px] truncate">{d.moduleRef || '—'}</td>
                  <td className="px-4 py-3 max-w-[180px] truncate">{d.nature}</td>
                  <td className="px-4 py-3 max-w-[160px] truncate">{d.rootCauseCategory}</td>
                  <td className="px-4 py-3">
                    <DeviationStatusBadge status={d.status} />
                  </td>
                  <td className="px-4 py-3 text-right">
                    <ChevronRight className="h-4 w-4 text-muted-foreground ml-auto" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {data.totalPages > 1 && (
            <div className="border-t px-4 py-2 text-xs text-muted-foreground">
              Showing {data.items.length} of {data.totalCount} deviations
            </div>
          )}
        </div>
      )}

      <NewDeviationDialog
        open={newOpen}
        onOpenChange={setNewOpen}
        prefill={prefill}
      />

      <DeviationDetailDialog
        deviationId={selectedId}
        open={detailOpen}
        onOpenChange={setDetailOpen}
      />
    </div>
  );
}

// ─── Modules Tab ──────────────────────────────────────────────────────────────

function ModulesTab() {
  const [outcomeFilter, setOutcomeFilter] = useState<string>('all');
  const [page] = useState(1);

  const params = {
    outcome: outcomeFilter === 'all' ? undefined : outcomeFilter,
    page,
    pageSize: 25,
  };

  const { data, isLoading } = useModuleOutcomes(params);

  const runHref = (run: ModuleOutcomeDto) => {
    if (run.toolboxTalkId) {
      return `/admin/toolbox-talks/talks/${run.toolboxTalkId}/validation/${run.runId}`;
    }
    if (run.courseId) {
      return `/admin/toolbox-talks/courses/${run.courseId}/validation/${run.runId}`;
    }
    return '#';
  };

  return (
    <div className="space-y-4">
      {/* Filter */}
      <div className="flex items-center gap-2">
        <Select value={outcomeFilter} onValueChange={setOutcomeFilter}>
          <SelectTrigger className="w-[160px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Outcomes</SelectItem>
            <SelectItem value="Pass">Pass</SelectItem>
            <SelectItem value="Review">Review</SelectItem>
            <SelectItem value="Fail">Fail</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="space-y-2">
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-14 w-full" />)}
        </div>
      ) : !data || data.items.length === 0 ? (
        <Card className="p-8 text-center">
          <div className="flex flex-col items-center gap-2">
            <BookOpen className="h-8 w-8 text-muted-foreground" />
            <p className="text-muted-foreground">No completed validation runs found.</p>
          </div>
        </Card>
      ) : (
        <div className="rounded-md border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Talk / Course</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Language</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Score</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Outcome</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Sections</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Completed</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Pipeline v</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {data.items.map((run: ModuleOutcomeDto) => (
                <tr key={run.runId} className="hover:bg-muted/30 transition-colors">
                  <td className="px-4 py-3 max-w-[200px]">
                    <p className="truncate font-medium">
                      {run.talkTitle ?? run.courseTitle ?? '—'}
                    </p>
                    {run.courseTitle && run.talkTitle && (
                      <p className="text-xs text-muted-foreground truncate">{run.courseTitle}</p>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant="outline" className="font-mono text-xs">
                      {run.languageCode.toUpperCase()}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 tabular-nums font-medium">
                    {Math.round(run.overallScore)}%
                  </td>
                  <td className="px-4 py-3">
                    <OutcomeBadge outcome={run.overallOutcome} />
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {run.passedSections}/{run.totalSections}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground whitespace-nowrap">
                    {run.completedAt
                      ? format(new Date(run.completedAt), 'dd MMM yyyy')
                      : '—'}
                  </td>
                  <td className="px-4 py-3">
                    {run.pipelineVersionHash ? (
                      <code className="text-xs text-muted-foreground">
                        {run.pipelineVersionHash.slice(0, 8)}
                      </code>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <Link
                      href={runHref(run)}
                      className="flex items-center justify-end text-primary hover:underline"
                    >
                      <ChevronRight className="h-4 w-4" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {data.totalPages > 1 && (
            <div className="border-t px-4 py-2 text-xs text-muted-foreground">
              Showing {data.items.length} of {data.totalCount} runs
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Changes Tab ──────────────────────────────────────────────────────────────

function NewChangeRecordDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const createMutation = useCreateChangeRecord();
  const [form, setForm] = useState<Partial<CreatePipelineChangeRecordRequest>>({});

  const set = (key: keyof CreatePipelineChangeRecordRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  const handleSubmit = () => {
    if (!form.component || !form.changeFrom || !form.changeTo || !form.justification || !form.newVersionLabel) {
      toast.error('Please fill in all required fields');
      return;
    }
    createMutation.mutate(form as CreatePipelineChangeRecordRequest, {
      onSuccess: () => {
        toast.success('Change record appended');
        onOpenChange(false);
        setForm({});
      },
      onError: () => toast.error('Failed to append change record'),
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Append Change Record</DialogTitle>
          <DialogDescription>
            This record is permanent and cannot be edited or deleted.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          <div className="space-y-1.5">
            <Label>Component <span className="text-destructive">*</span></Label>
            <Input
              value={form.component ?? ''}
              onChange={(e) => set('component', e.target.value)}
              placeholder="e.g. Round 1 Model, Pass Threshold, Prompt Version"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label>Change From <span className="text-destructive">*</span></Label>
              <Input
                value={form.changeFrom ?? ''}
                onChange={(e) => set('changeFrom', e.target.value)}
                placeholder="Previous value"
              />
            </div>
            <div className="space-y-1.5">
              <Label>Change To <span className="text-destructive">*</span></Label>
              <Input
                value={form.changeTo ?? ''}
                onChange={(e) => set('changeTo', e.target.value)}
                placeholder="New value"
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>Justification <span className="text-destructive">*</span></Label>
            <Textarea
              value={form.justification ?? ''}
              onChange={(e) => set('justification', e.target.value)}
              placeholder="Explain why this change was made"
              rows={3}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Impact Assessment</Label>
            <Textarea
              value={form.impactAssessment ?? ''}
              onChange={(e) => set('impactAssessment', e.target.value)}
              placeholder="How does this affect existing validated modules?"
              rows={2}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Action on Prior Modules</Label>
            <Input
              value={form.priorModulesAction ?? ''}
              onChange={(e) => set('priorModulesAction', e.target.value)}
              placeholder="e.g. Re-validate all, No action required"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label>New Version Label <span className="text-destructive">*</span></Label>
              <Input
                value={form.newVersionLabel ?? ''}
                onChange={(e) => set('newVersionLabel', e.target.value)}
                placeholder="e.g. v6.5"
              />
            </div>
            <div className="space-y-1.5">
              <Label>Approver</Label>
              <Input
                value={form.approver ?? ''}
                onChange={(e) => set('approver', e.target.value)}
                placeholder="Name (optional)"
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createMutation.isPending}>
            {createMutation.isPending ? 'Appending…' : 'Append Record'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ChangesTab() {
  const [newOpen, setNewOpen] = useState(false);
  const [page] = useState(1);
  const { data, isLoading } = useChangeRecords({ page, pageSize: 25 });

  return (
    <div className="space-y-4">
      {/* Append-only notice + New button */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <Alert className="border-blue-200 bg-blue-50 dark:border-blue-900 dark:bg-blue-950/30 flex-1">
          <Info className="h-4 w-4 text-blue-600" />
          <AlertDescription className="text-blue-800 dark:text-blue-300 text-sm">
            Change records are append-only. Once submitted they cannot be edited or deleted.
          </AlertDescription>
        </Alert>
        <Button onClick={() => setNewOpen(true)} className="shrink-0">
          <Plus className="mr-2 h-4 w-4" />
          Append Change Record
        </Button>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="space-y-2">
          {[1, 2, 3].map((i) => <Skeleton key={i} className="h-14 w-full" />)}
        </div>
      ) : !data || data.items.length === 0 ? (
        <Card className="p-8 text-center">
          <div className="flex flex-col items-center gap-2">
            <GitCommitHorizontal className="h-8 w-8 text-muted-foreground" />
            <p className="text-muted-foreground">No change records yet.</p>
          </div>
        </Card>
      ) : (
        <div className="rounded-md border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">ID</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Date</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Component</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">From → To</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Pipeline v</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Justification</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {data.items.map((cr: PipelineChangeRecordDto) => (
                <tr key={cr.id} className="hover:bg-muted/30 transition-colors">
                  <td className="px-4 py-3 font-mono text-xs">{cr.changeId}</td>
                  <td className="px-4 py-3 text-muted-foreground whitespace-nowrap">
                    {format(new Date(cr.deployedAt), 'dd MMM yyyy')}
                  </td>
                  <td className="px-4 py-3">{cr.component}</td>
                  <td className="px-4 py-3 max-w-[200px]">
                    <span className="text-muted-foreground truncate block">
                      {cr.changeFrom} → {cr.changeTo}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    {cr.pipelineVersionLabel && (
                      <Badge variant="outline" className="font-mono text-xs">
                        {cr.pipelineVersionLabel}
                      </Badge>
                    )}
                  </td>
                  <td className="px-4 py-3 max-w-[240px]">
                    <p className="truncate text-muted-foreground">{cr.justification}</p>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {data.totalPages > 1 && (
            <div className="border-t px-4 py-2 text-xs text-muted-foreground">
              Showing {data.items.length} of {data.totalCount} records
            </div>
          )}
        </div>
      )}

      <NewChangeRecordDialog open={newOpen} onOpenChange={setNewOpen} />
    </div>
  );
}

// ─── Term Gate Tab ────────────────────────────────────────────────────────────

const GATE_LANGUAGES = [
  { code: 'fr', name: 'French' },
  { code: 'pl', name: 'Polish' },
  { code: 'ro', name: 'Romanian' },
  { code: 'uk', name: 'Ukrainian' },
  { code: 'pt', name: 'Portuguese' },
  { code: 'es', name: 'Spanish' },
  { code: 'lt', name: 'Lithuanian' },
  { code: 'de', name: 'German' },
  { code: 'lv', name: 'Latvian' },
];

function TermGateTab() {
  const router = useRouter();
  const [sourceText, setSourceText] = useState('');
  const [targetText, setTargetText] = useState('');
  const [languageCode, setLanguageCode] = useState('');
  const [sectorKey, setSectorKey] = useState('');
  const [lastResult, setLastResult] = useState<TermGateCheckResult | null>(null);
  const [checkError, setCheckError] = useState<string | null>(null);

  const { data: summary, isLoading: summaryLoading } = useTermGateSummary();
  const checkMutation = useTermGateCheck();

  const handleRunCheck = () => {
    if (!sectorKey || !languageCode) {
      setCheckError('Please select a sector and language before running the check.');
      return;
    }
    setCheckError(null);
    setLastResult(null);
    checkMutation.mutate(
      { sourceText, targetText, languageCode, sectorKey },
      {
        onSuccess: (result) => setLastResult(result),
        onError: () => setCheckError('Failed to run gate check. Please try again.'),
      }
    );
  };

  const handleClear = () => {
    setSourceText('');
    setTargetText('');
    setLanguageCode('');
    setSectorKey('');
    setLastResult(null);
    setCheckError(null);
  };

  const handleLogDeviation = (failures: TermGateFailure[]) => {
    const first = failures[0];
    const prefill: DeviationPrefill = {
      nature: first ? `Term gate failure: ${first.englishTerm}` : 'Term gate failure',
      rootCauseCategory: 'terminology',
      sourceExcerpt: sourceText,
      targetExcerpt: targetText,
      languagePair: `en-${languageCode}`,
    };
    router.push(
      `/admin/toolbox-talks/pipeline?action=new_deviation&prefill=${btoa(JSON.stringify(prefill))}`
    );
  };

  const sectorName =
    summary?.termsBySector.find((s) => s.sectorKey === sectorKey)?.sectorName ?? sectorKey;

  const resultState =
    lastResult === null
      ? 'none'
      : lastResult.checkedCount === 0
      ? 'no-terms'
      : lastResult.passed
      ? 'pass'
      : 'fail';

  return (
    <div className="space-y-6">
      {/* Section 1 — Term Database Summary */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center justify-between">
            <span className="flex items-center gap-2">
              <Lock className="h-4 w-4 text-muted-foreground" />
              Term Database
            </span>
            <Link
              href="/admin/toolbox-talks/settings"
              className="text-sm font-normal text-primary hover:underline"
            >
              Manage Glossary →
            </Link>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {summaryLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-4 w-56" />
              <Skeleton className="h-4 w-72" />
            </div>
          ) : summary && summary.totalTerms > 0 ? (
            <div className="space-y-3">
              {/* Top stats */}
              <div className="flex flex-wrap items-center gap-6 text-sm">
                <div>
                  <span className="text-muted-foreground">Total locked terms: </span>
                  <span className="font-semibold">{summary.totalTerms}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Critical: </span>
                  <span className="font-semibold text-amber-600">{summary.criticalTerms}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Languages covered: </span>
                  <span className="font-semibold">{summary.languagesWithCoverage.length}</span>
                </div>
              </div>
              {/* Sector chips */}
              {summary.termsBySector.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {summary.termsBySector.map((s) => (
                    <Badge key={s.sectorKey} variant="secondary" className="text-xs">
                      {s.sectorName}: {s.termCount}
                    </Badge>
                  ))}
                </div>
              )}
              {/* Language coverage badges */}
              {summary.languagesWithCoverage.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {summary.languagesWithCoverage.map((code) => (
                    <Badge key={code} variant="outline" className="font-mono text-xs">
                      {code.toUpperCase()}
                    </Badge>
                  ))}
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              No glossary terms found. Add terms in Settings to enable the gate.
            </p>
          )}
        </CardContent>
      </Card>

      {/* Section 2 — Gate Tester */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center gap-2">
            <ShieldCheck className="h-4 w-4 text-muted-foreground" />
            Gate Tester
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="grid sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label>Language</Label>
                <Select value={languageCode} onValueChange={setLanguageCode}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select language" />
                  </SelectTrigger>
                  <SelectContent>
                    {GATE_LANGUAGES.map((l) => (
                      <SelectItem key={l.code} value={l.code}>
                        {l.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Sector</Label>
                <Select value={sectorKey} onValueChange={setSectorKey}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select sector" />
                  </SelectTrigger>
                  <SelectContent>
                    {summary?.termsBySector.map((s) => (
                      <SelectItem key={s.sectorKey} value={s.sectorKey}>
                        {s.sectorName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-1.5">
              <Label>Source text (English)</Label>
              <Textarea
                value={sourceText}
                onChange={(e) => setSourceText(e.target.value)}
                placeholder="Paste the English source sentence to check"
                rows={3}
              />
            </div>

            <div className="space-y-1.5">
              <Label>Target translation</Label>
              <Textarea
                value={targetText}
                onChange={(e) => setTargetText(e.target.value)}
                placeholder="Paste the translated text to check against"
                rows={3}
              />
            </div>

            {checkError && (
              <Alert variant="destructive">
                <AlertDescription>{checkError}</AlertDescription>
              </Alert>
            )}

            <div className="flex items-center gap-2">
              <Button
                onClick={handleRunCheck}
                disabled={checkMutation.isPending || !sourceText.trim() || !targetText.trim()}
              >
                {checkMutation.isPending ? 'Checking…' : 'Run Gate Check'}
              </Button>
              <Button variant="outline" onClick={handleClear} disabled={checkMutation.isPending}>
                Clear
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Section 3 — Gate Result */}
      {lastResult !== null && (
        <Card>
          <CardContent className="pt-6">
            {resultState === 'no-terms' && (
              <div className="border-l-4 border-muted pl-4 py-2">
                <p className="text-sm text-muted-foreground">
                  No terms from the{' '}
                  <span className="font-medium">{sectorName}</span> glossary were found in the
                  source text. Nothing to check.
                </p>
              </div>
            )}

            {resultState === 'pass' && (
              <div className="border-l-4 border-green-500 pl-4 py-2 space-y-3">
                <p className="text-sm font-medium text-green-700 dark:text-green-400 flex items-center gap-2">
                  <CheckCircle2 className="h-4 w-4" />
                  Gate passed — {lastResult.checkedCount} term
                  {lastResult.checkedCount !== 1 ? 's' : ''} checked, all approved translations
                  present
                </p>
                {lastResult.passingTerms.length > 0 && (
                  <div className="space-y-1.5">
                    {lastResult.passingTerms.map((t) => (
                      <div key={t.termId} className="flex items-center gap-2 text-sm">
                        <CheckCircle2 className="h-3.5 w-3.5 text-green-500 shrink-0" />
                        <span className="font-medium">{t.englishTerm}</span>
                        <span className="text-muted-foreground">→</span>
                        <span className="italic">{t.approvedTranslation}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            {resultState === 'fail' && (
              <div className="border-l-4 border-red-500 pl-4 py-2 space-y-4">
                <p className="text-sm font-medium text-red-700 dark:text-red-400 flex items-center gap-2">
                  <AlertTriangle className="h-4 w-4" />
                  Gate failed — {lastResult.failures.length} failure
                  {lastResult.failures.length !== 1 ? 's' : ''} detected
                </p>
                <div className="space-y-3">
                  {lastResult.failures.map((f, i) => (
                    <div key={i} className="rounded-md bg-muted/50 px-4 py-3 text-sm space-y-1">
                      <div>
                        <span className="text-muted-foreground">Term: </span>
                        <span className="font-medium">{f.englishTerm}</span>
                      </div>
                      <div>
                        <span className="text-muted-foreground">Expected: </span>
                        <span className="italic">{f.expectedTranslation}</span>
                      </div>
                      <div>
                        <span className="text-muted-foreground">Reason: </span>
                        {f.failureReason === 'missing_approved' ? (
                          'Approved translation not found in target'
                        ) : (
                          <>
                            Forbidden variant{' '}
                            <span className="font-medium text-red-600 dark:text-red-400">
                              &lsquo;{f.forbiddenTermFound}&rsquo;
                            </span>{' '}
                            present in target
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => handleLogDeviation(lastResult.failures)}
                >
                  Log as Deviation →
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function PipelineAuditPage() {
  const { user } = useAuth();
  const searchParams = useSearchParams();
  const router = useRouter();

  const action = searchParams.get('action');
  const prefillParam = searchParams.get('prefill');

  const prefill: DeviationPrefill | undefined = useCallback(() => {
    if (!prefillParam) return undefined;
    try {
      return JSON.parse(atob(prefillParam)) as DeviationPrefill;
    } catch {
      return undefined;
    }
  }, [prefillParam])();

  const initialTab = action === 'new_deviation' ? 'deviations' : 'dashboard';
  const [activeTab, setActiveTab] = useState(initialTab);

  // Clear the query params once consumed so back-nav doesn't re-open the dialog
  useEffect(() => {
    if (action) {
      router.replace('/admin/toolbox-talks/pipeline', { scroll: false });
    }
  }, [action, router]);

  const isSuperUser = user?.isSuperUser ?? false;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Pipeline Audit</h1>
        <p className="text-muted-foreground">
          Translation pipeline quality controls, deviation tracking, and change history.
        </p>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="dashboard">Dashboard</TabsTrigger>
          <TabsTrigger value="deviations">Deviations</TabsTrigger>
          <TabsTrigger value="modules">Modules</TabsTrigger>
          <TabsTrigger value="term-gate">Term Gate</TabsTrigger>
          {isSuperUser && <TabsTrigger value="changes">Changes</TabsTrigger>}
        </TabsList>

        <TabsContent value="dashboard" className="mt-6">
          <DashboardTab />
        </TabsContent>

        <TabsContent value="deviations" className="mt-6">
          <DeviationsTab
            initialOpenNew={action === 'new_deviation'}
            prefill={prefill}
          />
        </TabsContent>

        <TabsContent value="modules" className="mt-6">
          <ModulesTab />
        </TabsContent>

        <TabsContent value="term-gate" className="mt-6">
          <TermGateTab />
        </TabsContent>

        {isSuperUser && (
          <TabsContent value="changes" className="mt-6">
            <ChangesTab />
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
}
