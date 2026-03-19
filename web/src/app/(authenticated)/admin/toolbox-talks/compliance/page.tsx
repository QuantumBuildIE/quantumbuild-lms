'use client';

import { useState } from 'react';
import Link from 'next/link';
import { format } from 'date-fns';
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
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs';
import {
  CheckCircle2,
  Clock,
  AlertTriangle,
  Info,
  FileText,
  BookOpen,
  Plus,
  FileBarChart,
  ShieldCheck,
} from 'lucide-react';
import { usePermission, useAuth } from '@/lib/auth/use-auth';
import { useTenantSectors } from '@/lib/api/admin/use-tenant-sectors';
import { useComplianceChecklist } from '@/lib/api/admin/use-requirement-mappings';
import { AddMappingDialog } from '@/features/toolbox-talks/components/AddMappingDialog';
import { GenerateReportDialog } from '@/features/toolbox-talks/components/GenerateReportDialog';
import type {
  ComplianceRequirementDto,
} from '@/types/requirement-mappings';

// ============================================
// Helpers
// ============================================

function CoverageStatusBadge({ status }: { status: string }) {
  if (status === 'Covered') {
    return (
      <Badge className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400">
        <CheckCircle2 className="mr-1 h-3 w-3" />
        Covered
      </Badge>
    );
  }
  if (status === 'Pending') {
    return (
      <Badge className="bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400">
        <Clock className="mr-1 h-3 w-3" />
        Pending Review
      </Badge>
    );
  }
  return (
    <Badge variant="secondary" className="text-muted-foreground">
      <AlertTriangle className="mr-1 h-3 w-3" />
      Gap
    </Badge>
  );
}

function PriorityBadge({ priority }: { priority: string }) {
  const variant =
    priority === 'high' ? 'destructive' : priority === 'low' ? 'secondary' : 'outline';
  return <Badge variant={variant}>{priority}</Badge>;
}

function ValidationScoreChip({ score, outcome }: { score: number; outcome: string }) {
  const color =
    outcome === 'Pass'
      ? 'text-green-700'
      : outcome === 'Review'
        ? 'text-amber-700'
        : 'text-red-700';
  return (
    <span className={`text-xs font-semibold tabular-nums ${color}`}>
      {Math.round(score)}%
    </span>
  );
}

// ============================================
// Requirement Row
// ============================================

function RequirementRow({
  requirement,
  onAddMapping,
}: {
  requirement: ComplianceRequirementDto;
  onAddMapping: (req: ComplianceRequirementDto) => void;
}) {
  const [expanded, setExpanded] = useState(false);

  const coveredMapping = requirement.mappings.find(
    (m) => m.mappingStatus === 'Confirmed' && m.validationOutcome
  );
  const pendingMapping = requirement.mappings.find(
    (m) => m.mappingStatus === 'Suggested' || (m.mappingStatus === 'Confirmed' && !m.validationOutcome)
  );

  const contentHref = (contentId: string, contentType: string) =>
    contentType === 'Talk'
      ? `/admin/toolbox-talks/talks/${contentId}`
      : `/admin/toolbox-talks/courses/${contentId}/edit`;

  return (
    <Card>
      <CardContent className="pt-5 pb-4">
        <div className="flex items-start justify-between gap-4">
          {/* Left side */}
          <div className="flex-1 min-w-0 space-y-1">
            <div className="flex items-center gap-2 flex-wrap">
              <h4 className="font-medium text-sm">{requirement.title}</h4>
              <PriorityBadge priority={requirement.priority} />
              {requirement.principle && (
                <Badge variant="outline" className="text-xs">
                  {requirement.principle}
                  {requirement.principleLabel ? ` — ${requirement.principleLabel}` : ''}
                </Badge>
              )}
            </div>
            <p
              className={`text-sm text-muted-foreground ${!expanded ? 'line-clamp-2' : ''}`}
            >
              {requirement.description}
            </p>
            {requirement.description.length > 150 && (
              <button
                className="text-xs text-primary hover:underline"
                onClick={() => setExpanded(!expanded)}
              >
                {expanded ? 'Show less' : 'Show more'}
              </button>
            )}

            {/* Mapping details */}
            {requirement.coverageStatus === 'Covered' && coveredMapping && (
              <div className="flex items-center gap-2 pt-1 text-sm">
                {coveredMapping.contentType === 'Talk' ? (
                  <FileText className="h-3.5 w-3.5 text-muted-foreground" />
                ) : (
                  <BookOpen className="h-3.5 w-3.5 text-muted-foreground" />
                )}
                <Link
                  href={contentHref(coveredMapping.contentId, coveredMapping.contentType)}
                  className="hover:underline text-primary"
                >
                  {coveredMapping.contentTitle}
                </Link>
                {coveredMapping.validationScore != null && coveredMapping.validationOutcome && (
                  <ValidationScoreChip
                    score={coveredMapping.validationScore}
                    outcome={coveredMapping.validationOutcome}
                  />
                )}
                {coveredMapping.validationDate && (
                  <span className="text-xs text-muted-foreground">
                    {format(new Date(coveredMapping.validationDate), 'dd MMM yyyy')}
                  </span>
                )}
              </div>
            )}

            {requirement.coverageStatus === 'Pending' && pendingMapping && (
              <div className="flex items-center gap-2 pt-1 text-sm">
                {pendingMapping.contentType === 'Talk' ? (
                  <FileText className="h-3.5 w-3.5 text-muted-foreground" />
                ) : (
                  <BookOpen className="h-3.5 w-3.5 text-muted-foreground" />
                )}
                <span className="text-muted-foreground">
                  {pendingMapping.contentTitle}
                </span>
                <Link
                  href="/admin/toolbox-talks/pending-mappings"
                  className="text-xs text-primary hover:underline"
                >
                  Review Mapping
                </Link>
              </div>
            )}
          </div>

          {/* Right side */}
          <div className="flex items-center gap-2 shrink-0">
            <CoverageStatusBadge status={requirement.coverageStatus} />
            {requirement.coverageStatus === 'Gap' && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => onAddMapping(requirement)}
              >
                <Plus className="mr-1 h-3.5 w-3.5" />
                Add Mapping
              </Button>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================
// Sector Checklist View
// ============================================

function SectorChecklistView({ sectorKey }: { sectorKey: string }) {
  const { data: checklist, isLoading } = useComplianceChecklist(sectorKey);
  const [principleFilter, setPrincipleFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [mappingTarget, setMappingTarget] = useState<ComplianceRequirementDto | null>(null);
  const [reportDialogOpen, setReportDialogOpen] = useState(false);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="grid gap-4 md:grid-cols-4">
          {[1, 2, 3, 4].map((i) => (
            <Skeleton key={i} className="h-24" />
          ))}
        </div>
        <Skeleton className="h-4 w-full" />
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-32" />
        ))}
      </div>
    );
  }

  if (!checklist || checklist.totalRequirements === 0) {
    return (
      <Card className="p-8 text-center">
        <div className="flex flex-col items-center gap-2">
          <ShieldCheck className="h-8 w-8 text-muted-foreground" />
          <p className="text-muted-foreground">
            No regulatory requirements found for this sector. Requirements are added via the regulatory ingestion pipeline.
          </p>
        </div>
      </Card>
    );
  }

  // Get unique principles for filter
  const principles = checklist.principleGroups.map((g) => ({
    value: g.principle,
    label: g.principle
      ? `${g.principle}${g.principleLabel ? ` — ${g.principleLabel}` : ''}`
      : 'Uncategorised',
  }));

  // Filter groups
  const filteredGroups = checklist.principleGroups
    .filter((g) => principleFilter === 'all' || g.principle === principleFilter)
    .map((group) => ({
      ...group,
      requirements:
        statusFilter === 'all'
          ? group.requirements
          : group.requirements.filter((r) => r.coverageStatus === statusFilter),
    }))
    .filter((g) => g.requirements.length > 0);

  return (
    <div className="space-y-6">
      {/* Subtitle with regulatory body */}
      {checklist.regulatoryBody && (
        <p className="text-sm text-muted-foreground">
          {checklist.sectorName} — {checklist.regulatoryBody}
        </p>
      )}

      {/* Summary stat cards */}
      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Total Requirements</CardDescription>
            <CardTitle className="text-2xl">{checklist.totalRequirements}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Covered</CardDescription>
            <CardTitle className="text-2xl text-green-600">
              {checklist.coveredCount}
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Pending</CardDescription>
            <CardTitle className="text-2xl text-amber-600">
              {checklist.pendingCount}
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Gap</CardDescription>
            <CardTitle className="text-2xl text-red-600">
              {checklist.gapCount}
            </CardTitle>
          </CardHeader>
        </Card>
      </div>

      {/* Coverage progress bar */}
      <div className="space-y-1">
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">Coverage</span>
          <span className="font-medium">{checklist.coveragePercentage}%</span>
        </div>
        <div className="h-3 w-full rounded-full bg-muted overflow-hidden flex">
          {checklist.coveredCount > 0 && (
            <div
              className="h-full bg-green-500 transition-all"
              style={{
                width: `${(checklist.coveredCount / checklist.totalRequirements) * 100}%`,
              }}
            />
          )}
          {checklist.pendingCount > 0 && (
            <div
              className="h-full bg-amber-400 transition-all"
              style={{
                width: `${(checklist.pendingCount / checklist.totalRequirements) * 100}%`,
              }}
            />
          )}
        </div>
      </div>

      {/* Filters row */}
      <div className="flex items-center gap-3 flex-wrap">
        <Select value={principleFilter} onValueChange={setPrincipleFilter}>
          <SelectTrigger className="w-[220px]">
            <SelectValue placeholder="All Principles" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Principles</SelectItem>
            {principles.map((p) => (
              <SelectItem key={p.value} value={p.value}>
                {p.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder="All Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Status</SelectItem>
            <SelectItem value="Covered">Covered</SelectItem>
            <SelectItem value="Pending">Pending</SelectItem>
            <SelectItem value="Gap">Gap</SelectItem>
          </SelectContent>
        </Select>

        <div className="ml-auto">
          <Button variant="outline" onClick={() => setReportDialogOpen(true)}>
            <FileBarChart className="mr-2 h-4 w-4" />
            Generate Inspection Report
          </Button>
        </div>
      </div>

      {/* Requirements grouped by principle */}
      <Accordion type="multiple" defaultValue={filteredGroups.map((g) => g.principle)} className="space-y-2">
        {filteredGroups.map((group) => (
          <AccordionItem key={group.principle} value={group.principle} className="border rounded-lg px-1">
            <AccordionTrigger className="hover:no-underline px-3">
              <div className="flex items-center gap-3">
                <span className="font-medium">
                  {group.principle || 'Uncategorised'}
                  {group.principleLabel ? ` — ${group.principleLabel}` : ''}
                </span>
                <div className="flex items-center gap-1.5 text-xs">
                  <span className="text-green-600">{group.coveredCount} covered</span>
                  <span className="text-muted-foreground">/</span>
                  <span className="text-amber-600">{group.pendingCount} pending</span>
                  <span className="text-muted-foreground">/</span>
                  <span className="text-red-600">{group.gapCount} gap</span>
                </div>
              </div>
            </AccordionTrigger>
            <AccordionContent className="px-2 pb-3">
              <div className="space-y-2">
                {group.requirements.map((req) => (
                  <RequirementRow
                    key={req.id}
                    requirement={req}
                    onAddMapping={setMappingTarget}
                  />
                ))}
              </div>
            </AccordionContent>
          </AccordionItem>
        ))}
      </Accordion>

      {filteredGroups.length === 0 && (
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            No requirements match the selected filters.
          </p>
        </Card>
      )}

      <AddMappingDialog
        open={!!mappingTarget}
        onOpenChange={(open) => { if (!open) setMappingTarget(null); }}
        requirementId={mappingTarget?.id ?? ''}
        requirementTitle={mappingTarget?.title ?? ''}
      />

      <GenerateReportDialog
        open={reportDialogOpen}
        onOpenChange={setReportDialogOpen}
        sectorKey={sectorKey}
        sectorName={checklist.sectorName}
        regulatoryBody={checklist.regulatoryBody}
      />
    </div>
  );
}

// ============================================
// Main Page
// ============================================

export default function ComplianceChecklistPage() {
  const hasAdminPermission = usePermission('Learnings.Admin');
  const { user } = useAuth();
  const { data: tenantSectors, isLoading: sectorsLoading } = useTenantSectors(
    user?.tenantId ?? ''
  );

  if (!hasAdminPermission) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Compliance Checklist
          </h1>
        </div>
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            You do not have permission to view the compliance checklist.
          </p>
        </Card>
      </div>
    );
  }

  const sectors = tenantSectors ?? [];
  const isSingleSector = sectors.length === 1;
  const isMultiSector = sectors.length > 1;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Compliance Checklist
        </h1>
        <p className="text-muted-foreground">
          Regulatory requirement coverage across your training content.
        </p>
      </div>

      {/* Disclaimer banner */}
      <Alert className="border-blue-200 bg-blue-50 dark:border-blue-900 dark:bg-blue-950/30">
        <Info className="h-4 w-4 text-blue-600" />
        <AlertDescription className="text-blue-800 dark:text-blue-300 text-sm">
          This checklist is generated from your training content and AI-assisted mappings.
          It is your organisation&apos;s responsibility to ensure all regulatory requirements are met
          and that mappings accurately reflect your training programme.
          CertifiedIQ does not provide legal or regulatory advice.
        </AlertDescription>
      </Alert>

      {sectorsLoading ? (
        <div className="space-y-4">
          <Skeleton className="h-10 w-full" />
          <div className="grid gap-4 md:grid-cols-4">
            {[1, 2, 3, 4].map((i) => (
              <Skeleton key={i} className="h-24" />
            ))}
          </div>
        </div>
      ) : sectors.length === 0 ? (
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            No sectors configured for your organisation. Contact your administrator to set up sectors.
          </p>
        </Card>
      ) : isSingleSector ? (
        <SectorChecklistView sectorKey={sectors[0].sectorKey} />
      ) : isMultiSector ? (
        <Tabs defaultValue={sectors.find((s) => s.isDefault)?.sectorKey ?? sectors[0].sectorKey}>
          <TabsList>
            {sectors.map((sector) => (
              <TabsTrigger key={sector.sectorKey} value={sector.sectorKey}>
                {sector.sectorIcon && <span className="mr-1">{sector.sectorIcon}</span>}
                {sector.sectorName}
              </TabsTrigger>
            ))}
          </TabsList>
          {sectors.map((sector) => (
            <TabsContent key={sector.sectorKey} value={sector.sectorKey}>
              <SectorChecklistView sectorKey={sector.sectorKey} />
            </TabsContent>
          ))}
        </Tabs>
      ) : null}
    </div>
  );
}
