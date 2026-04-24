'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { format, formatDistanceToNow } from 'date-fns';
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
  Database,
  Play,
  RefreshCw,
  TrendingDown,
  ChevronDown,
  ChevronUp,
  Loader2,
  FileText,
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
import { Progress } from '@/components/ui/progress';
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
  useCorpora,
  useCorpus,
  useCorpusRuns,
  useCorpusRunDetail,
  useCorpusRunDiff,
  useFreezeCorpus,
  useLockCorpus,
  useAddCorpusEntry,
  useRemoveCorpusEntry,
  useTriggerCorpusRun,
  useConfirmCorpusRun,
  useUpdateChangeStatus,
} from '@/lib/api/toolbox-talks/use-pipeline-audit';
import { useToolboxTalks } from '@/lib/api/toolbox-talks/use-toolbox-talks';
import { useCorpusRunHub } from '@/features/toolbox-talks/hooks/use-corpus-run-hub';
import type {
  TranslationDeviationDto,
  CreateDeviationRequest,
  CreatePipelineChangeRecordRequest,
  PipelineChangeRecordDto,
  ModuleOutcomeDto,
  TermGateCheckResult,
  TermGateFailure,
  AuditCorpusDto,
  AuditCorpusEntryDto,
  CorpusRunSummaryDto,
  CorpusRunDetailDto,
  CorpusVerdict,
  PipelineChangeStatus,
  TriggerCorpusRunResponse,
  FreezeCorpusRequest,
  AddCorpusEntryRequest,
  UpdateChangeStatusRequest,
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

function ChangeStatusDialog({
  changeId,
  currentStatus,
  open,
  onOpenChange,
}: {
  changeId: string;
  currentStatus: PipelineChangeStatus;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const updateMutation = useUpdateChangeStatus();
  const [justification, setJustification] = useState('');
  const [targetStatus, setTargetStatus] = useState<PipelineChangeStatus | null>(null);

  const transitions: { from: PipelineChangeStatus; to: PipelineChangeStatus; label: string; requiresJustification?: boolean }[] = [
    { from: 'Draft', to: 'ReadyForReview', label: 'Submit for Review' },
    { from: 'PendingApproval', to: 'Approved', label: 'Approve' },
    { from: 'BlockedRegression', to: 'Approved', label: 'Override (Approve Despite Regression)', requiresJustification: true },
  ];

  const available = transitions.filter((t) => t.from === currentStatus);

  const handle = (to: PipelineChangeStatus) => {
    const t = transitions.find((tr) => tr.to === to);
    if (t?.requiresJustification) {
      setTargetStatus(to);
    } else {
      updateMutation.mutate(
        { id: changeId, request: { status: to } },
        {
          onSuccess: () => { toast.success('Status updated'); onOpenChange(false); },
          onError: () => toast.error('Failed to update status'),
        }
      );
    }
  };

  const handleConfirmJustification = () => {
    if (!justification.trim() || !targetStatus) return;
    updateMutation.mutate(
      { id: changeId, request: { status: targetStatus, justification } },
      {
        onSuccess: () => { toast.success('Status updated'); onOpenChange(false); setTargetStatus(null); setJustification(''); },
        onError: () => toast.error('Failed to update status'),
      }
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Update Change Status</DialogTitle>
        </DialogHeader>
        {!targetStatus ? (
          <div className="space-y-3 py-2">
            {available.length === 0 ? (
              <p className="text-sm text-muted-foreground">No transitions available from {currentStatus}.</p>
            ) : (
              available.map((t) => (
                <Button
                  key={t.to}
                  variant="outline"
                  className="w-full justify-start"
                  onClick={() => handle(t.to)}
                  disabled={updateMutation.isPending}
                >
                  {t.label}
                </Button>
              ))
            )}
          </div>
        ) : (
          <div className="space-y-3 py-2">
            <Alert className="border-amber-200 bg-amber-50 dark:bg-amber-950/30">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                You are overriding a regression block. A justification is required.
              </AlertDescription>
            </Alert>
            <div className="space-y-1.5">
              <Label>Justification <span className="text-destructive">*</span></Label>
              <Textarea
                value={justification}
                onChange={(e) => setJustification(e.target.value)}
                placeholder="Explain why this regression is acceptable"
                rows={3}
              />
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => { setTargetStatus(null); setJustification(''); }}>Back</Button>
              <Button onClick={handleConfirmJustification} disabled={!justification.trim() || updateMutation.isPending}>
                Confirm Override
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

function ChangesTab() {
  const [newOpen, setNewOpen] = useState(false);
  const [statusDialogId, setStatusDialogId] = useState<string | null>(null);
  const [statusDialogCurrent, setStatusDialogCurrent] = useState<PipelineChangeStatus>('Draft');
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [page] = useState(1);
  const { data, isLoading } = useChangeRecords({ page, pageSize: 25 });

  const openStatusDialog = (cr: PipelineChangeRecordDto) => {
    setStatusDialogId(cr.id);
    setStatusDialogCurrent((cr.status as PipelineChangeStatus) ?? 'Draft');
    setStatusDialogOpen(true);
  };

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
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Status</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Justification</th>
                <th className="px-4 py-3" />
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
                  <td className="px-4 py-3">
                    <ChangeStatusBadge status={(cr.status as PipelineChangeStatus) ?? 'Draft'} />
                  </td>
                  <td className="px-4 py-3 max-w-[240px]">
                    <p className="truncate text-muted-foreground">{cr.justification}</p>
                  </td>
                  <td className="px-4 py-3">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-7 text-xs"
                      onClick={() => openStatusDialog(cr)}
                    >
                      Status →
                    </Button>
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

      {statusDialogId && (
        <ChangeStatusDialog
          changeId={statusDialogId}
          currentStatus={statusDialogCurrent}
          open={statusDialogOpen}
          onOpenChange={setStatusDialogOpen}
        />
      )}
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

// ─── Change Status Badge ──────────────────────────────────────────────────────

function ChangeStatusBadge({ status }: { status: PipelineChangeStatus }) {
  const map: Record<PipelineChangeStatus, { label: string; className: string }> = {
    Draft: { label: 'Draft', className: 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300' },
    ReadyForReview: { label: 'Ready for Review', className: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400' },
    PendingApproval: { label: 'Pending Approval', className: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400' },
    Approved: { label: 'Approved', className: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400' },
    BlockedRegression: { label: 'Blocked — Regression', className: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400' },
  };
  const s = map[status] ?? map.Draft;
  return <Badge className={s.className}>{s.label}</Badge>;
}

// ─── Corpus Verdict Badge ─────────────────────────────────────────────────────

function VerdictBadge({ verdict }: { verdict: CorpusVerdict | undefined | null }) {
  if (!verdict) return null;
  if (verdict === 'Pass') {
    return <Badge className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400">Pass</Badge>;
  }
  if (verdict === 'Fail') {
    return <Badge className="bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400">Fail</Badge>;
  }
  return <Badge className="bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400">Inconclusive</Badge>;
}

// ─── Run Status Badge ─────────────────────────────────────────────────────────

function RunStatusBadge({ status }: { status: string }) {
  if (status === 'Running') {
    return <Badge className="bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400"><Loader2 className="mr-1 h-3 w-3 animate-spin" />Running</Badge>;
  }
  if (status === 'Completed') {
    return <Badge className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400"><CheckCircle2 className="mr-1 h-3 w-3" />Completed</Badge>;
  }
  if (status === 'Failed') {
    return <Badge className="bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400"><AlertTriangle className="mr-1 h-3 w-3" />Failed</Badge>;
  }
  return <Badge variant="outline"><Clock className="mr-1 h-3 w-3" />Pending</Badge>;
}

// ─── Corpus Run Detail Dialog ─────────────────────────────────────────────────

function CorpusRunDetailDialog({
  runId,
  open,
  onOpenChange,
}: {
  runId: string | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const { data: run, isLoading } = useCorpusRunDetail(open ? runId : null);
  const { data: diff } = useCorpusRunDiff(open && run?.status === 'Completed' ? runId : null);
  const [showDiff, setShowDiff] = useState(false);
  const { isConnected, progress, isComplete, verdict, error: hubError, reset } = useCorpusRunHub(
    run?.status === 'Running' ? runId : null
  );

  useEffect(() => {
    if (!open) reset();
  }, [open, reset]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            Corpus Run Detail
            {run && <RunStatusBadge status={verdict ? 'Completed' : run.status} />}
            {(verdict ?? run?.verdict) && <VerdictBadge verdict={(verdict ?? run?.verdict) as CorpusVerdict} />}
          </DialogTitle>
          {run && (
            <DialogDescription>
              {run.isSmokeTest ? 'Smoke test · ' : ''}{run.triggerType} · {run.totalEntries} entries
            </DialogDescription>
          )}
        </DialogHeader>

        {isLoading ? (
          <div className="space-y-3 py-4">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-3/4" />
          </div>
        ) : run ? (
          <div className="space-y-4">
            {/* Live progress */}
            {run.status === 'Running' && (
              <div className="space-y-2">
                <div className="flex items-center gap-2 text-sm">
                  {isConnected ? (
                    <span className="text-blue-600 dark:text-blue-400 flex items-center gap-1.5">
                      <span className="h-2 w-2 rounded-full bg-blue-500 animate-pulse" />
                      Live
                    </span>
                  ) : (
                    <span className="text-muted-foreground">Connecting…</span>
                  )}
                  {progress && <span className="text-muted-foreground">{progress.message}</span>}
                </div>
                <Progress value={progress?.percentComplete ?? 0} className="h-2" />
                {hubError && (
                  <Alert variant="destructive">
                    <AlertDescription>{hubError}</AlertDescription>
                  </Alert>
                )}
              </div>
            )}

            {/* Aggregate stats */}
            {(run.status === 'Completed' || isComplete) && (
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm">
                <div className="rounded-md border p-3">
                  <p className="text-xs text-muted-foreground">Pass</p>
                  <p className="text-lg font-semibold text-green-600">{run.passedEntries}</p>
                </div>
                <div className="rounded-md border p-3">
                  <p className="text-xs text-muted-foreground">Review</p>
                  <p className="text-lg font-semibold text-amber-600">{run.reviewEntries}</p>
                </div>
                <div className="rounded-md border p-3">
                  <p className="text-xs text-muted-foreground">Fail</p>
                  <p className="text-lg font-semibold text-red-600">{run.failedEntries}</p>
                </div>
                <div className="rounded-md border p-3">
                  <p className="text-xs text-muted-foreground">Regressions</p>
                  <p className={`text-lg font-semibold ${run.regressionEntries > 0 ? 'text-red-600' : 'text-green-600'}`}>
                    {run.regressionEntries}
                  </p>
                </div>
              </div>
            )}

            {/* Per-entry results */}
            {run.results && run.results.length > 0 && (
              <div className="space-y-2">
                <p className="text-xs font-medium text-muted-foreground uppercase">Entry Results</p>
                <div className="rounded-md border overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-muted/50">
                      <tr>
                        <th className="px-3 py-2 text-left font-medium text-muted-foreground">Ref</th>
                        <th className="px-3 py-2 text-left font-medium text-muted-foreground">Section</th>
                        <th className="px-3 py-2 text-left font-medium text-muted-foreground">Score</th>
                        <th className="px-3 py-2 text-left font-medium text-muted-foreground">Outcome</th>
                        <th className="px-3 py-2 text-left font-medium text-muted-foreground">Expected</th>
                        <th className="px-3 py-2 text-left font-medium text-muted-foreground">Δ</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {run.results.map((r) => (
                        <tr
                          key={r.id}
                          className={r.isRegression ? 'bg-red-50/50 dark:bg-red-950/20' : ''}
                        >
                          <td className="px-3 py-2 font-mono text-xs">{r.entryRef}</td>
                          <td className="px-3 py-2 max-w-[160px] truncate">{r.sectionTitle}</td>
                          <td className="px-3 py-2 tabular-nums font-medium">{Math.round(r.finalScore)}%</td>
                          <td className="px-3 py-2"><OutcomeBadge outcome={r.outcome} /></td>
                          <td className="px-3 py-2 text-muted-foreground">{r.expectedOutcome}</td>
                          <td className="px-3 py-2">
                            {r.isRegression ? (
                              <span className="text-red-600 flex items-center gap-0.5 text-xs font-medium">
                                <TrendingDown className="h-3 w-3" />{Math.round(r.scoreDelta)}
                              </span>
                            ) : (
                              <span className="text-muted-foreground text-xs">
                                {r.scoreDelta >= 0 ? '+' : ''}{Math.round(r.scoreDelta)}
                              </span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Diff vs previous run */}
            {diff && (
              <div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowDiff((v) => !v)}
                  className="text-xs"
                >
                  {showDiff ? <ChevronUp className="mr-1 h-3 w-3" /> : <ChevronDown className="mr-1 h-3 w-3" />}
                  Diff vs previous run ({diff.regressionCount} regressions, {diff.improvementCount} improvements)
                </Button>
                {showDiff && (
                  <div className="rounded-md border overflow-hidden mt-2">
                    <table className="w-full text-xs">
                      <thead className="bg-muted/50">
                        <tr>
                          <th className="px-3 py-2 text-left font-medium text-muted-foreground">Ref</th>
                          <th className="px-3 py-2 text-left font-medium text-muted-foreground">Current</th>
                          <th className="px-3 py-2 text-left font-medium text-muted-foreground">Previous</th>
                          <th className="px-3 py-2 text-left font-medium text-muted-foreground">Score Δ</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y">
                        {diff.entries.map((e) => (
                          <tr
                            key={e.corpusEntryId}
                            className={
                              e.isRegression
                                ? 'bg-red-50/50 dark:bg-red-950/20'
                                : e.isImprovement
                                ? 'bg-green-50/50 dark:bg-green-950/20'
                                : ''
                            }
                          >
                            <td className="px-3 py-2 font-mono">{e.entryRef}</td>
                            <td className="px-3 py-2">
                              <OutcomeBadge outcome={e.currentOutcome} />
                            </td>
                            <td className="px-3 py-2 text-muted-foreground">
                              {e.previousOutcome ?? '—'}
                            </td>
                            <td className="px-3 py-2 tabular-nums">
                              {e.previousScore != null ? (
                                <span className={e.isRegression ? 'text-red-600 font-medium' : e.isImprovement ? 'text-green-600 font-medium' : 'text-muted-foreground'}>
                                  {e.scoreDelta >= 0 ? '+' : ''}{Math.round(e.scoreDelta)}
                                </span>
                              ) : '—'}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

// ─── Freeze Corpus Dialog ─────────────────────────────────────────────────────

function FreezeCorpusDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const [talkId, setTalkId] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const { data: talks } = useToolboxTalks({ pageNumber: 1, pageSize: 100 });
  const freezeMutation = useFreezeCorpus();

  const handleSubmit = () => {
    if (!talkId || !name.trim()) {
      toast.error('Please select a talk and enter a name');
      return;
    }
    const request: FreezeCorpusRequest = {
      talkId,
      name: name.trim(),
      description: description.trim() || undefined,
      sectionIndexes: [],
    };
    freezeMutation.mutate(request, {
      onSuccess: () => {
        toast.success('Corpus created from talk');
        onOpenChange(false);
        setTalkId('');
        setName('');
        setDescription('');
      },
      onError: () => toast.error('Failed to create corpus'),
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Create Audit Corpus</DialogTitle>
          <DialogDescription>
            Freeze accepted validation results from a talk into a reusable test corpus.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          <div className="space-y-1.5">
            <Label>Source Talk <span className="text-destructive">*</span></Label>
            <Select value={talkId} onValueChange={setTalkId}>
              <SelectTrigger>
                <SelectValue placeholder="Select a published talk" />
              </SelectTrigger>
              <SelectContent>
                {talks?.items?.filter((t) => t.status === 'Published').map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.code} — {t.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label>Corpus Name <span className="text-destructive">*</span></Label>
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Manual Handling Safety EN→PL Baseline"
            />
          </div>

          <div className="space-y-1.5">
            <Label>Description</Label>
            <Textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional description of this corpus"
              rows={2}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={freezeMutation.isPending}>
            {freezeMutation.isPending ? 'Creating…' : 'Create Corpus'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Trigger Run Dialog ───────────────────────────────────────────────────────

function TriggerRunDialog({
  corpusId,
  open,
  onOpenChange,
  onRunStarted,
}: {
  corpusId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onRunStarted: (runId: string) => void;
}) {
  const [isSmokeTest, setIsSmokeTest] = useState(false);
  const [pendingRun, setPendingRun] = useState<TriggerCorpusRunResponse | null>(null);
  const triggerMutation = useTriggerCorpusRun();
  const confirmMutation = useConfirmCorpusRun();

  const handleTrigger = () => {
    triggerMutation.mutate(
      { corpusId, request: { isSmokeTest } },
      {
        onSuccess: (resp) => {
          if (!resp.requiresConfirmation && !resp.requiresSuperUserApproval) {
            toast.success('Corpus run queued');
            onRunStarted(resp.runId);
            onOpenChange(false);
          } else {
            setPendingRun(resp);
          }
        },
        onError: () => toast.error('Failed to trigger run'),
      }
    );
  };

  const handleConfirm = () => {
    if (!pendingRun) return;
    confirmMutation.mutate(
      { corpusId, runId: pendingRun.runId },
      {
        onSuccess: () => {
          toast.success('Corpus run confirmed and queued');
          onRunStarted(pendingRun.runId);
          onOpenChange(false);
          setPendingRun(null);
        },
        onError: () => toast.error('Failed to confirm run'),
      }
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Trigger Corpus Run</DialogTitle>
          <DialogDescription>
            Execute the full validation pipeline against the corpus entries.
          </DialogDescription>
        </DialogHeader>

        {!pendingRun ? (
          <div className="space-y-4 py-2">
            <div className="flex items-center gap-3 rounded-md border p-3">
              <input
                type="checkbox"
                id="smoke-test"
                checked={isSmokeTest}
                onChange={(e) => setIsSmokeTest(e.target.checked)}
                className="rounded"
              />
              <div>
                <label htmlFor="smoke-test" className="text-sm font-medium cursor-pointer">
                  Smoke test (first 5 entries only)
                </label>
                <p className="text-xs text-muted-foreground">Faster and cheaper — good for sanity checks</p>
              </div>
            </div>
          </div>
        ) : (
          <div className="space-y-3 py-2">
            <Alert className={pendingRun.requiresSuperUserApproval
              ? 'border-red-200 bg-red-50 dark:bg-red-950/30'
              : 'border-amber-200 bg-amber-50 dark:bg-amber-950/30'
            }>
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                {pendingRun.requiresSuperUserApproval
                  ? 'This run requires SuperUser approval due to high estimated cost.'
                  : 'Please confirm this run.'}
                <br />
                Estimated cost:{' '}
                <span className="font-semibold">
                  €{pendingRun.estimatedCostEur.toFixed(2)}
                </span>{' '}
                for {pendingRun.estimatedEntries} entries.
              </AlertDescription>
            </Alert>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => { onOpenChange(false); setPendingRun(null); }}>
            Cancel
          </Button>
          {!pendingRun ? (
            <Button onClick={handleTrigger} disabled={triggerMutation.isPending}>
              {triggerMutation.isPending ? 'Preparing…' : 'Trigger Run'}
            </Button>
          ) : (
            <Button onClick={handleConfirm} disabled={confirmMutation.isPending}>
              {confirmMutation.isPending ? 'Confirming…' : 'Confirm & Queue'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Corpus Detail Panel ──────────────────────────────────────────────────────

function CorpusDetailPanel({ corpusId, onBack }: { corpusId: string; onBack: () => void }) {
  const { data: corpus, isLoading } = useCorpus(corpusId);
  const { data: runs, isLoading: runsLoading } = useCorpusRuns(corpusId);
  const lockMutation = useLockCorpus();
  const removeEntryMutation = useRemoveCorpusEntry();
  const [triggerOpen, setTriggerOpen] = useState(false);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [runDetailOpen, setRunDetailOpen] = useState(false);

  const handleLock = () => {
    lockMutation.mutate(
      { id: corpusId, request: {} },
      {
        onSuccess: () => toast.success('Corpus locked'),
        onError: () => toast.error('Failed to lock corpus'),
      }
    );
  };

  const handleRemoveEntry = (entryId: string, entryRef: string) => {
    if (!confirm(`Remove entry ${entryRef}?`)) return;
    removeEntryMutation.mutate(
      { corpusId, entryId },
      {
        onSuccess: () => toast.success('Entry removed'),
        onError: () => toast.error('Failed to remove entry'),
      }
    );
  };

  const openRunDetail = (runId: string) => {
    setSelectedRunId(runId);
    setRunDetailOpen(true);
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (!corpus) return null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <button
            onClick={onBack}
            className="text-sm text-muted-foreground hover:text-foreground flex items-center gap-1 mb-2"
          >
            ← Back to corpora
          </button>
          <div className="flex items-center gap-2 flex-wrap">
            <h2 className="text-lg font-semibold">{corpus.name}</h2>
            <Badge variant="outline" className="font-mono text-xs">{corpus.corpusId}</Badge>
            {corpus.isLocked && (
              <Badge className="bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400">
                <Lock className="mr-1 h-3 w-3" />Locked v{corpus.version}
              </Badge>
            )}
          </div>
          {corpus.description && (
            <p className="text-sm text-muted-foreground mt-1">{corpus.description}</p>
          )}
          <div className="flex flex-wrap gap-4 mt-2 text-xs text-muted-foreground">
            <span>Sector: <span className="font-medium">{corpus.sectorKey}</span></span>
            <span>Language pair: <span className="font-medium">{corpus.languagePair}</span></span>
            <span>Entries: <span className="font-medium">{corpus.activeEntryCount}/{corpus.entryCount}</span></span>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {!corpus.isLocked && (
            <Button
              variant="outline"
              size="sm"
              onClick={handleLock}
              disabled={lockMutation.isPending || corpus.activeEntryCount === 0}
            >
              <Lock className="mr-2 h-4 w-4" />
              Lock Corpus
            </Button>
          )}
          <Button
            size="sm"
            onClick={() => setTriggerOpen(true)}
            disabled={corpus.activeEntryCount === 0}
          >
            <Play className="mr-2 h-4 w-4" />
            Run
          </Button>
        </div>
      </div>

      {/* Entries table */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center gap-2">
            <FileText className="h-4 w-4 text-muted-foreground" />
            Entries ({corpus.activeEntryCount})
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {corpus.entries.length === 0 ? (
            <p className="px-6 py-8 text-center text-sm text-muted-foreground">
              No entries in this corpus.
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-muted/50">
                  <tr>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Ref</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Section</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Expected</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Threshold</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Safety</th>
                    <th className="px-4 py-2.5" />
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {corpus.entries.filter((e) => e.isActive).map((entry) => (
                    <tr key={entry.id} className="hover:bg-muted/30 transition-colors">
                      <td className="px-4 py-2.5 font-mono text-xs">{entry.entryRef}</td>
                      <td className="px-4 py-2.5 max-w-[200px] truncate">{entry.sectionTitle}</td>
                      <td className="px-4 py-2.5"><OutcomeBadge outcome={entry.expectedOutcome} /></td>
                      <td className="px-4 py-2.5 tabular-nums">{entry.passThreshold}%</td>
                      <td className="px-4 py-2.5">
                        {entry.isSafetyCritical && (
                          <Badge className="bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400 text-xs">
                            Safety
                          </Badge>
                        )}
                      </td>
                      <td className="px-4 py-2.5">
                        {!corpus.isLocked && (
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-7 px-2 text-muted-foreground hover:text-destructive"
                            onClick={() => handleRemoveEntry(entry.id, entry.entryRef)}
                            disabled={removeEntryMutation.isPending}
                          >
                            ×
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Run history */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center gap-2">
            <RefreshCw className="h-4 w-4 text-muted-foreground" />
            Run History
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {runsLoading ? (
            <div className="p-4 space-y-2">
              {[1, 2].map((i) => <Skeleton key={i} className="h-12 w-full" />)}
            </div>
          ) : !runs || runs.length === 0 ? (
            <p className="px-6 py-8 text-center text-sm text-muted-foreground">
              No runs yet. Click Run to execute the corpus.
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-muted/50">
                  <tr>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Status</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Verdict</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Entries</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Regressions</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Mean Score</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Cost</th>
                    <th className="px-4 py-2.5 text-left font-medium text-muted-foreground">Triggered</th>
                    <th className="px-4 py-2.5" />
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {runs.map((run) => (
                    <tr
                      key={run.id}
                      className="hover:bg-muted/30 transition-colors cursor-pointer"
                      onClick={() => openRunDetail(run.id)}
                    >
                      <td className="px-4 py-2.5"><RunStatusBadge status={run.status} /></td>
                      <td className="px-4 py-2.5">
                        {run.verdict ? <VerdictBadge verdict={run.verdict} /> : <span className="text-muted-foreground">—</span>}
                      </td>
                      <td className="px-4 py-2.5 tabular-nums">{run.totalEntries}</td>
                      <td className="px-4 py-2.5">
                        {run.regressionEntries > 0 ? (
                          <span className="text-red-600 font-medium flex items-center gap-1">
                            <TrendingDown className="h-3.5 w-3.5" />{run.regressionEntries}
                          </span>
                        ) : (
                          <span className="text-green-600">0</span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 tabular-nums">
                        {run.meanScore != null ? `${Math.round(run.meanScore)}%` : '—'}
                      </td>
                      <td className="px-4 py-2.5 tabular-nums text-muted-foreground">
                        {run.actualCostEur != null ? `€${run.actualCostEur.toFixed(3)}` : run.estimatedCostEur != null ? `~€${run.estimatedCostEur.toFixed(3)}` : '—'}
                      </td>
                      <td className="px-4 py-2.5 text-muted-foreground whitespace-nowrap text-xs">
                        {formatDistanceToNow(new Date(run.createdAt), { addSuffix: true })}
                      </td>
                      <td className="px-4 py-2.5">
                        <ChevronRight className="h-4 w-4 text-muted-foreground" />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <TriggerRunDialog
        corpusId={corpusId}
        open={triggerOpen}
        onOpenChange={setTriggerOpen}
        onRunStarted={(runId) => openRunDetail(runId)}
      />

      <CorpusRunDetailDialog
        runId={selectedRunId}
        open={runDetailOpen}
        onOpenChange={setRunDetailOpen}
      />
    </div>
  );
}

// ─── Corpus Tab ───────────────────────────────────────────────────────────────

function CorpusTab() {
  const [freezeOpen, setFreezeOpen] = useState(false);
  const [page] = useState(1);
  const [selectedCorpusId, setSelectedCorpusId] = useState<string | null>(null);
  const { data, isLoading } = useCorpora({ page, pageSize: 20 });

  if (selectedCorpusId) {
    return (
      <CorpusDetailPanel
        corpusId={selectedCorpusId}
        onBack={() => setSelectedCorpusId(null)}
      />
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-4">
        <p className="text-sm text-muted-foreground">
          Audit corpora are frozen sets of validated sections used to regression-test the pipeline.
        </p>
        <Button onClick={() => setFreezeOpen(true)}>
          <Plus className="mr-2 h-4 w-4" />
          New Corpus
        </Button>
      </div>

      {isLoading ? (
        <div className="space-y-2">
          {[1, 2, 3].map((i) => <Skeleton key={i} className="h-16 w-full" />)}
        </div>
      ) : !data || data.items.length === 0 ? (
        <Card className="p-8 text-center">
          <div className="flex flex-col items-center gap-2">
            <Database className="h-8 w-8 text-muted-foreground" />
            <p className="font-medium">No corpora yet</p>
            <p className="text-sm text-muted-foreground">
              Create a corpus by freezing accepted validation results from a published talk.
            </p>
            <Button onClick={() => setFreezeOpen(true)} className="mt-2">
              <Plus className="mr-2 h-4 w-4" />
              New Corpus
            </Button>
          </div>
        </Card>
      ) : (
        <div className="rounded-md border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">ID</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Name</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Sector</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Lang</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Entries</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Status</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Created</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {data.items.map((corpus) => (
                <tr
                  key={corpus.id}
                  className="hover:bg-muted/30 transition-colors cursor-pointer"
                  onClick={() => setSelectedCorpusId(corpus.id)}
                >
                  <td className="px-4 py-3 font-mono text-xs">{corpus.corpusId}</td>
                  <td className="px-4 py-3 font-medium max-w-[200px] truncate">{corpus.name}</td>
                  <td className="px-4 py-3 text-muted-foreground">{corpus.sectorKey}</td>
                  <td className="px-4 py-3">
                    <Badge variant="outline" className="font-mono text-xs">{corpus.languagePair}</Badge>
                  </td>
                  <td className="px-4 py-3 tabular-nums">{corpus.activeEntryCount}</td>
                  <td className="px-4 py-3">
                    {corpus.isLocked ? (
                      <Badge className="bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400 text-xs">
                        <Lock className="mr-1 h-3 w-3" />Locked
                      </Badge>
                    ) : (
                      <Badge variant="secondary" className="text-xs">Draft</Badge>
                    )}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground text-xs whitespace-nowrap">
                    {format(new Date(corpus.createdAt), 'dd MMM yyyy')}
                  </td>
                  <td className="px-4 py-3">
                    <ChevronRight className="h-4 w-4 text-muted-foreground ml-auto" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {data.totalPages > 1 && (
            <div className="border-t px-4 py-2 text-xs text-muted-foreground">
              Showing {data.items.length} of {data.totalCount} corpora
            </div>
          )}
        </div>
      )}

      <FreezeCorpusDialog open={freezeOpen} onOpenChange={setFreezeOpen} />
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
          <TabsTrigger value="corpus">Corpus</TabsTrigger>
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

        <TabsContent value="corpus" className="mt-6">
          <CorpusTab />
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
