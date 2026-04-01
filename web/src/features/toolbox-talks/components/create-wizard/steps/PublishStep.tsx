'use client';

import { useState, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQueries } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  Loader2,
  CheckCircle2,
  AlertTriangle,
  Image as ImageIcon,
  FileText,
  Languages,
  Award,
  Repeat,
  Eye,
  Shuffle,
  RotateCcw,
  Plus,
  ArrowLeft,
  Rocket,
  BookOpen,
  GraduationCap,
  Presentation,
  Captions,
  Film,
} from 'lucide-react';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';
import {
  useCreationSession,
  useSessionSettings,
  useSessionQuizData,
  usePublish,
  contentCreationKeys,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { getSessionValidationRun } from '@/lib/api/toolbox-talks/content-creation';
import { useLookupValues } from '@/hooks/use-lookups';
import { useAvailableSectors } from '@/lib/api/admin/use-tenant-sectors';
import type { WizardState } from '../CreateWizard';
import type {
  ContentCreationSettings,
  ParsedSection,
  QuizData,
  ValidationRunDetail,
  ValidationOutcome,
  ReviewerDecision,
  PublishResult,
} from '@/types/content-creation';

// ============================================
// Props
// ============================================

interface PublishStepProps {
  state: WizardState;
  updateState: (updates: Partial<WizardState>) => void;
  onBack: () => void;
}

// ============================================
// Helpers
// ============================================

function outcomeVariant(outcome: ValidationOutcome): 'default' | 'secondary' | 'destructive' {
  switch (outcome) {
    case 'Pass': return 'default';
    case 'Review': return 'secondary';
    case 'Fail': return 'destructive';
  }
}

function outcomeColor(outcome: ValidationOutcome): string {
  switch (outcome) {
    case 'Pass': return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
    case 'Review': return 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400';
    case 'Fail': return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';
  }
}

// ============================================
// Component
// ============================================

export function PublishStep({ state, onBack }: PublishStepProps) {
  const router = useRouter();
  const sessionId = state.sessionId;

  // Data fetching
  const { data: session, isLoading: sessionLoading } = useCreationSession(sessionId);
  const { data: settings, isLoading: settingsLoading } = useSessionSettings(sessionId);
  const { data: quizData, isLoading: quizLoading } = useSessionQuizData(sessionId);
  const { data: languages = [] } = useLookupValues('Language');
  const publish = usePublish();

  // Publish result state
  const [publishResult, setPublishResult] = useState<PublishResult | null>(null);

  // Derive data from session
  const parsedSections: ParsedSection[] = useMemo(() => {
    if (!session?.parsedSectionsJson) return [];
    try { return JSON.parse(session.parsedSectionsJson); } catch { return []; }
  }, [session?.parsedSectionsJson]);

  const targetLanguageCodes: string[] = useMemo(() => {
    if (!session?.targetLanguageCodes) return [];
    try { return JSON.parse(session.targetLanguageCodes); } catch { return []; }
  }, [session?.targetLanguageCodes]);

  const validationRunIds: string[] = useMemo(() => {
    if (!session?.validationRunIds) return [];
    try { return JSON.parse(session.validationRunIds); } catch { return []; }
  }, [session?.validationRunIds]);

  const languageLookup = useMemo(() => {
    const map = new Map<string, string>();
    languages.forEach((l) => map.set(l.code, l.name));
    return map;
  }, [languages]);

  // Fetch all validation runs in parallel (stable hook count via useQueries)
  // Validation runs are stored against the draft talk (session.outputTalkId), not the session ID
  const talkId = session?.outputTalkId ?? null;
  const validationRunQueries = useQueries({
    queries: validationRunIds.map((runId) => ({
      queryKey: contentCreationKeys.validationRun(talkId ?? '', runId),
      queryFn: () => getSessionValidationRun(talkId!, runId),
      enabled: !!talkId,
      staleTime: 10 * 1000,
    })),
  });

  // Build a map of runId → ValidationRunDetail for child components
  const validationRunMap = useMemo(() => {
    const map = new Map<string, ValidationRunDetail>();
    validationRunIds.forEach((runId, i) => {
      const data = validationRunQueries[i]?.data;
      if (data) map.set(runId, data);
    });
    return map;
  }, [validationRunIds, validationRunQueries]);

  const isLoading = sessionLoading || settingsLoading || quizLoading;

  // ============================================
  // Publish handler
  // ============================================

  const handlePublish = () => {
    if (!sessionId || !settings) return;

    publish.mutate(
      {
        sessionId,
        request: {
          title: settings.title,
          description: settings.description || undefined,
          category: settings.category || undefined,
          sourceLanguageCode: 'en',
        },
      },
      {
        onSuccess: (result) => {
          if (result.success) {
            setPublishResult(result);
          } else {
            toast.error(result.errorMessage || 'Failed to publish');
          }
        },
        onError: (error) => {
          if (error && typeof error === 'object' && 'response' in error) {
            const axiosError = error as import('axios').AxiosError<{ errors?: string[]; message?: string }>;
            const data = axiosError.response?.data;
            const description = data?.errors?.[0] ?? data?.message ?? axiosError.message ?? 'An error occurred';
            toast.error('Failed to publish', { description });
          } else {
            const message = error instanceof Error ? error.message : 'An error occurred';
            toast.error('Failed to publish', { description: message });
          }
        },
      }
    );
  };

  // ============================================
  // Success state
  // ============================================

  if (publishResult?.success) {
    const outputLabel = state.selectedOutputType === 'Course' ? 'Course' : 'Toolbox Talk';
    const viewPath = state.selectedOutputType === 'Course'
      ? `/admin/toolbox-talks/courses`
      : `/admin/toolbox-talks/talks/${publishResult.outputId}`;

    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <div className="rounded-full bg-green-100 p-4 dark:bg-green-900/30 mb-6">
          <CheckCircle2 className="h-12 w-12 text-green-600 dark:text-green-400" />
        </div>
        <h2 className="text-2xl font-bold mb-2">{outputLabel} Published</h2>
        <p className="text-muted-foreground mb-1 text-lg font-medium">{settings?.title}</p>
        {session?.documentRef && (
          <p className="text-sm text-muted-foreground mb-8">
            Document Reference: {session.documentRef}
          </p>
        )}
        <div className="flex gap-3">
          <Button onClick={() => router.push(viewPath)}>
            <Eye className="h-4 w-4 mr-2" />
            {state.selectedOutputType === 'Course' ? 'View Course List' : `View ${outputLabel}`}
          </Button>
          <Button
            variant="outline"
            onClick={() => router.push('/admin/toolbox-talks/create')}
          >
            <Plus className="h-4 w-4 mr-2" />
            Create Another
          </Button>
        </div>
      </div>
    );
  }

  // ============================================
  // Loading state
  // ============================================

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mr-2" />
        <span className="text-muted-foreground">Loading summary...</span>
      </div>
    );
  }

  // ============================================
  // Summary render
  // ============================================

  return (
    <div className="space-y-6">
      {/* Panel A — Content Summary */}
      <ContentSummaryPanel
        session={session!}
        settings={settings!}
        parsedSections={parsedSections}
        outputType={state.selectedOutputType}
      />

      {/* Three-column summary row */}
      <ThreeColumnSummary
        session={session!}
        settings={settings!}
        parsedSections={parsedSections}
        quizData={quizData ?? null}
        targetLanguageCodes={targetLanguageCodes}
        validationRunIds={validationRunIds}
        validationRunMap={validationRunMap}
        languageLookup={languageLookup}
      />

      {/* Panel B — Audit & Validation Summary */}
      <AuditSummaryPanel session={session!} />

      {/* Warning banner for manual reviews */}
      <ManualReviewWarningBanner
        targetLanguageCodes={targetLanguageCodes}
        validationRunIds={validationRunIds}
        validationRunMap={validationRunMap}
        languageLookup={languageLookup}
      />

      {/* Publish action */}
      <div className="flex justify-between pt-4 border-t">
        <Button variant="outline" onClick={onBack} disabled={publish.isPending}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back
        </Button>
        <Button
          size="lg"
          className="bg-green-600 hover:bg-green-700 text-white gap-2 px-8"
          onClick={handlePublish}
          disabled={publish.isPending}
        >
          {publish.isPending ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin" />
              Publishing...
            </>
          ) : (
            <>
              <Rocket className="h-4 w-4" />
              Publish
            </>
          )}
        </Button>
      </div>
    </div>
  );
}

// ============================================
// Panel A — Content Summary
// ============================================

function ContentSummaryPanel({
  session,
  settings,
  parsedSections,
  outputType,
}: {
  session: NonNullable<ReturnType<typeof useCreationSession>['data']>;
  settings: ContentCreationSettings;
  parsedSections: ParsedSection[];
  outputType: string | null;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Content Summary</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="flex gap-6">
          {/* Cover image or placeholder */}
          <div className="flex-shrink-0">
            {settings.coverImageUrl ? (
              <img
                src={settings.coverImageUrl}
                alt="Cover"
                className="h-28 w-28 rounded-lg object-cover border"
              />
            ) : (
              <div className="h-28 w-28 rounded-lg border-2 border-dashed flex items-center justify-center bg-muted/30">
                <ImageIcon className="h-8 w-8 text-muted-foreground/50" />
              </div>
            )}
          </div>

          {/* Details */}
          <div className="flex-1 min-w-0 space-y-2">
            <h3 className="text-xl font-semibold truncate">{settings.title || 'Untitled'}</h3>
            {settings.description && (
              <p className="text-sm text-muted-foreground line-clamp-2">{settings.description}</p>
            )}

            {/* Document ref */}
            {session.documentRef && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <FileText className="h-3.5 w-3.5" />
                <span>{session.documentRef}</span>
              </div>
            )}

            {/* Badges row */}
            <div className="flex flex-wrap gap-2 pt-1">
              {/* Output type */}
              <Badge variant="outline" className="gap-1">
                {outputType === 'Course' ? (
                  <GraduationCap className="h-3 w-3" />
                ) : (
                  <BookOpen className="h-3 w-3" />
                )}
                {outputType ?? 'Lesson'}
              </Badge>

              {/* Category */}
              {settings.category && (
                <Badge variant="secondary">{settings.category}</Badge>
              )}

              {/* Frequency */}
              {settings.refresherFrequency && settings.refresherFrequency !== 'Once' && (
                <Badge variant="outline" className="gap-1">
                  <Repeat className="h-3 w-3" />
                  {settings.refresherFrequency}
                </Badge>
              )}

              {/* Behaviour tags */}
              {settings.generateCertificate && (
                <Badge variant="outline" className="gap-1">
                  <Award className="h-3 w-3" />
                  Certificate
                </Badge>
              )}
              {settings.autoAssign && (
                <Badge variant="outline" className="gap-1">
                  Auto-assign
                </Badge>
              )}
              {session.inputMode === 'Video' && settings.minimumWatchPercent > 0 && (
                <Badge variant="outline" className="gap-1">
                  <Eye className="h-3 w-3" />
                  Watch {settings.minimumWatchPercent}%
                </Badge>
              )}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================
// Three-Column Summary Row
// ============================================

function ThreeColumnSummary({
  session,
  settings,
  parsedSections,
  quizData,
  targetLanguageCodes,
  validationRunIds,
  validationRunMap,
  languageLookup,
}: {
  session: NonNullable<ReturnType<typeof useCreationSession>['data']>;
  settings: ContentCreationSettings;
  parsedSections: ParsedSection[];
  quizData: QuizData | null;
  targetLanguageCodes: string[];
  validationRunIds: string[];
  validationRunMap: Map<string, ValidationRunDetail>;
  languageLookup: Map<string, string>;
}) {
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      {/* Content column */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium text-muted-foreground">Content</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {session.sourceFileName && (
            <Row label="Source file" value={session.sourceFileName} />
          )}
          <Row
            label={session.outputType === 'Course' ? 'Lessons' : 'Sections'}
            value={String(parsedSections.length)}
          />
          <Row label="Input mode" value={session.inputMode} />
          <Row label="Output type" value={session.outputType ?? 'Lesson'} />
          <Row label="Source language" value="English" />

          {/* Slideshow — generating after publish */}
          {settings.generateSlideshow && (
            <div className="flex items-center justify-between gap-2 pt-1">
              <span className="text-muted-foreground flex items-center gap-1.5">
                <Presentation className="h-3 w-3" />
                Slideshow
              </span>
              <Badge variant="outline" className="text-xs bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-900/30 dark:text-blue-400 dark:border-blue-800">
                Generating in background
              </Badge>
            </div>
          )}

          {/* Subtitles — still processing when user reaches publish */}
          {session.inputMode === 'Video' && session.subtitleJobId && (
            <div className="flex items-center justify-between gap-2 pt-1">
              <span className="text-muted-foreground flex items-center gap-1.5">
                <Captions className="h-3 w-3" />
                Subtitles
              </span>
              <Badge variant="outline" className="text-xs bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-900/30 dark:text-blue-400 dark:border-blue-800">
                Processing in background
              </Badge>
            </div>
          )}

          {/* Course + Video note */}
          {session.outputType === 'Course' && session.inputMode === 'Video' && (
            <div className="flex items-start gap-1.5 pt-2 text-xs text-muted-foreground">
              <Film className="h-3 w-3 mt-0.5 flex-shrink-0" />
              <span>Full video added as first learning in course</span>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Translations column */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium text-muted-foreground">Translations</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {targetLanguageCodes.length === 0 ? (
            <p className="text-muted-foreground italic">No translations</p>
          ) : (
            targetLanguageCodes.map((code, i) => {
              const runId = validationRunIds[i] ?? null;
              const runDetail = runId ? validationRunMap.get(runId) : undefined;
              const langName = languageLookup.get(code) ?? code.toUpperCase();

              return (
                <div key={code} className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2 min-w-0">
                    <Languages className="h-3.5 w-3.5 flex-shrink-0 text-muted-foreground" />
                    <span className="truncate">{langName}</span>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    {runDetail ? (
                      <>
                        <span className="text-xs text-muted-foreground">
                          {Math.round(runDetail.overallScore)}%
                        </span>
                        <Badge
                          className={cn('text-xs', outcomeColor(runDetail.overallOutcome))}
                          variant="outline"
                        >
                          {runDetail.overallOutcome}
                        </Badge>
                      </>
                    ) : (
                      <Badge variant="outline" className="text-xs">
                        {runId ? 'Loading...' : 'N/A'}
                      </Badge>
                    )}
                  </div>
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      {/* Quiz column */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium text-muted-foreground">Quiz</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {quizData?.settings?.requireQuiz === false ? (
            <p className="text-muted-foreground italic">Quiz not required</p>
          ) : (
            <>
              <Row label="Questions" value={String(quizData?.questions?.length ?? 0)} />
              <Row label="Passing score" value={`${quizData?.settings?.passingScore ?? 80}%`} />
              <Row
                label="Shuffle"
                value={quizData?.settings?.shuffleQuestions ? 'On' : 'Off'}
                icon={<Shuffle className="h-3 w-3" />}
              />
              <Row
                label="Retry"
                value={quizData?.settings?.allowRetry ? 'On' : 'Off'}
                icon={<RotateCcw className="h-3 w-3" />}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

// ============================================
// Panel B — Audit & Validation Summary
// ============================================

function AuditSummaryPanel({
  session,
}: {
  session: NonNullable<ReturnType<typeof useCreationSession>['data']>;
}) {
  const { data: sectors = [] } = useAvailableSectors();

  // Resolve sectorKey to display name with icon
  const sectorDisplay = useMemo(() => {
    if (!session.sectorKey) return null;
    const match = sectors.find((s) => s.key === session.sectorKey);
    if (match) return match.icon ? `${match.icon} ${match.name}` : match.name;
    return session.sectorKey;
  }, [session.sectorKey, sectors]);

  const fields = [
    { label: 'Reviewer', value: session.reviewerName },
    { label: 'Organisation', value: session.reviewerOrg },
    { label: 'Role', value: session.reviewerRole },
    { label: 'Document Ref', value: session.documentRef },
    { label: 'Client', value: session.clientName },
    { label: 'Audit Purpose', value: session.auditPurpose },
    { label: 'Pass Threshold', value: session.passThreshold ? `${session.passThreshold}%` : null },
    { label: 'Safety Sector', value: sectorDisplay },
  ];

  // Only show if at least one field has a value
  const hasAnyValue = fields.some((f) => f.value);
  if (!hasAnyValue) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Audit & Validation</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="grid gap-x-8 gap-y-3 sm:grid-cols-2 md:grid-cols-4 text-sm">
          {fields.map(({ label, value }) => (
            <div key={label}>
              <dt className="text-muted-foreground text-xs font-medium">{label}</dt>
              <dd className="font-medium mt-0.5">{value || '—'}</dd>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================
// Manual Review Warning Banner
// ============================================

function ManualReviewWarningBanner({
  targetLanguageCodes,
  validationRunIds,
  validationRunMap,
  languageLookup,
}: {
  targetLanguageCodes: string[];
  validationRunIds: string[];
  validationRunMap: Map<string, ValidationRunDetail>;
  languageLookup: Map<string, string>;
}) {
  // Collect manually reviewed sections from already-fetched run data
  const reviewedItems: { language: string; sectionTitle: string; decision: ReviewerDecision }[] = [];

  validationRunIds.forEach((runId, i) => {
    const runDetail = validationRunMap.get(runId);
    if (!runDetail?.results) return;
    const langCode = targetLanguageCodes[i] ?? 'unknown';
    const langName = languageLookup.get(langCode) ?? langCode.toUpperCase();
    runDetail.results.forEach((section) => {
      if (section.reviewerDecision === 'Accepted' || section.reviewerDecision === 'Edited') {
        reviewedItems.push({
          language: langName,
          sectionTitle: section.sectionTitle,
          decision: section.reviewerDecision,
        });
      }
    });
  });

  if (reviewedItems.length === 0) return null;

  return (
    <Alert className="border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30">
      <AlertTriangle className="h-4 w-4 text-amber-600" />
      <AlertTitle className="text-amber-800 dark:text-amber-300">
        Manual Review Applied
      </AlertTitle>
      <AlertDescription className="text-amber-700 dark:text-amber-400">
        <p className="mb-2">
          The following translation sections were manually reviewed by the reviewer and
          may differ from the automated validation outcome:
        </p>
        <ul className="list-disc pl-5 space-y-0.5 text-sm">
          {reviewedItems.map((item, i) => (
            <li key={i}>
              <span className="font-medium">{item.language}</span> &mdash;{' '}
              &ldquo;{item.sectionTitle}&rdquo;{' '}
              <span className="text-xs">
                ({item.decision === 'Edited' ? 'Edited by reviewer' : 'Accepted by reviewer'})
              </span>
            </li>
          ))}
        </ul>
      </AlertDescription>
    </Alert>
  );
}

// ============================================
// Utility sub-components
// ============================================

function Row({
  label,
  value,
  icon,
}: {
  label: string;
  value: string;
  icon?: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-2">
      <span className="text-muted-foreground flex items-center gap-1.5">
        {icon}
        {label}
      </span>
      <span className="font-medium truncate text-right">{value}</span>
    </div>
  );
}
