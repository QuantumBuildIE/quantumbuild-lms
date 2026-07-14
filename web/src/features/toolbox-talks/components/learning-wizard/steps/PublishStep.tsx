'use client';

import { useMemo } from 'react';
import {
  AlertTriangle,
  Award,
  BookOpen,
  Captions,
  CheckCircle2,
  Eye,
  FileText,
  Image as ImageIcon,
  Languages,
  Loader2,
  Presentation,
  Repeat,
  RotateCcw,
  Shuffle,
  XCircle,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { useValidationRuns } from '@/lib/api/toolbox-talks/use-content-creation';
import { useTalk } from '../hooks/useTalk';
import { useWorkflowSubscription } from '../hooks/useWorkflowSubscription';
import { LoadingState } from '../components/LoadingState';
import { parseLanguageCodes } from '@/features/toolbox-talks/utils/parseLanguageCodes';
import type { ValidationOutcome, ValidationRunSummary } from '@/types/content-creation';
import type { TranslationWorkflowStateDto } from '@/types/workflows';
import type { ToolboxTalk } from '@/types/toolbox-talks';

// ============================================
// Props
// ============================================

export interface PublishStepProps {
  talkId: string;
  isPublishing: boolean;
  publishError: Error | null;
}

// ============================================
// Helpers
// ============================================

const LANG_NAMES: Record<string, string> = {
  en: 'English', fr: 'French', pl: 'Polish', ro: 'Romanian',
  uk: 'Ukrainian', pt: 'Portuguese', es: 'Spanish', lt: 'Lithuanian',
  de: 'German', lv: 'Latvian', ga: 'Irish', nl: 'Dutch',
};

function langName(code: string): string {
  return LANG_NAMES[code] ?? code.toUpperCase();
}

function outcomeColor(outcome: ValidationOutcome): string {
  switch (outcome) {
    case 'Pass':   return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
    case 'Review': return 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400';
    case 'Fail':   return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';
  }
}

// ============================================
// Component
// ============================================

export function PublishStep({ talkId, isPublishing, publishError }: PublishStepProps) {
  const { talk, isLoading: talkLoading } = useTalk(talkId);
  const { data: validationRuns, isLoading: runsLoading } = useValidationRuns(talkId);
  const { data: workflowStates } = useWorkflowSubscription(talkId);

  const targetLanguageCodes = useMemo(
    () => parseLanguageCodes(talk?.targetLanguageCodes ?? null),
    [talk?.targetLanguageCodes]
  );

  // Latest completed run per language code (API returns newest-first)
  const latestRunByCode = useMemo(() => {
    const map: Record<string, ValidationRunSummary> = {};
    for (const run of validationRuns ?? []) {
      if (!map[run.languageCode]) map[run.languageCode] = run;
    }
    return map;
  }, [validationRuns]);

  if (talkLoading || runsLoading) {
    return <LoadingState label="Loading summary…" />;
  }

  if (!talk) return null;

  return (
    <div className="space-y-6">
      {/* Inline publish error */}
      {publishError && <PublishErrorAlert error={publishError} />}

      {/* Content summary */}
      <ContentSummaryPanel talk={talk} />

      {/* Three-column summary row */}
      <ThreeColumnSummary
        talk={talk}
        targetLanguageCodes={targetLanguageCodes}
        latestRunByCode={latestRunByCode}
      />

      {/* Audit metadata — only when at least one field is populated */}
      <AuditMetadataPanel talk={talk} />

      {/* External review warning */}
      <ExternalReviewWarningBanner
        workflowStates={workflowStates ?? []}
        targetLanguageCodes={targetLanguageCodes}
      />

      {/* Publishing in-progress indicator */}
      {isPublishing && (
        <div className="flex items-center justify-center gap-2 py-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Publishing…
        </div>
      )}
    </div>
  );
}

// ============================================
// Publish error alert
// ============================================

function PublishErrorAlert({ error }: { error: Error }) {
  const axiosError = error as import('axios').AxiosError<{ errors?: string[]; message?: string }>;
  const detail =
    axiosError.response?.data?.errors?.[0] ??
    axiosError.response?.data?.message ??
    error.message;

  return (
    <Alert variant="destructive">
      <XCircle className="h-4 w-4" />
      <AlertTitle>Publish failed</AlertTitle>
      <AlertDescription>{detail}</AlertDescription>
    </Alert>
  );
}

// ============================================
// Content Summary Panel
// ============================================

function ContentSummaryPanel({ talk }: { talk: ToolboxTalk }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Content Summary</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="flex gap-6">
          {/* Cover image or placeholder */}
          <div className="flex-shrink-0">
            {talk.coverImageUrl ? (
              <img
                src={talk.coverImageUrl}
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
            <h3 className="text-xl font-semibold truncate">{talk.title}</h3>
            {talk.description && (
              <p className="text-sm text-muted-foreground line-clamp-2">{talk.description}</p>
            )}

            {/* Document ref */}
            {talk.documentRef && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <FileText className="h-3.5 w-3.5" />
                <span>{talk.documentRef}</span>
              </div>
            )}

            {/* Badges */}
            <div className="flex flex-wrap gap-2 pt-1">
              <Badge variant="outline" className="gap-1">
                <BookOpen className="h-3 w-3" />
                Talk
              </Badge>

              {talk.category && (
                <Badge variant="secondary">{talk.category}</Badge>
              )}

              {talk.requiresRefresher && (
                <Badge variant="outline" className="gap-1">
                  <Repeat className="h-3 w-3" />
                  Refresher
                </Badge>
              )}

              {talk.generateCertificate && (
                <Badge variant="outline" className="gap-1">
                  <Award className="h-3 w-3" />
                  Certificate
                </Badge>
              )}

              {talk.autoAssignToNewEmployees && (
                <Badge variant="outline">Auto-assign</Badge>
              )}

              {talk.inputMode === 'Video' && talk.minimumVideoWatchPercent > 0 && (
                <Badge variant="outline" className="gap-1">
                  <Eye className="h-3 w-3" />
                  Watch {talk.minimumVideoWatchPercent}%
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
  talk,
  targetLanguageCodes,
  latestRunByCode,
}: {
  talk: ToolboxTalk;
  targetLanguageCodes: string[];
  latestRunByCode: Record<string, ValidationRunSummary>;
}) {
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      {/* Content column */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium text-muted-foreground">Content</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {talk.sourceFileName && (
            <Row label="Source file" value={talk.sourceFileName} />
          )}
          <Row label="Sections" value={String(talk.sections.length)} />
          {talk.inputMode !== 'Text' && (
            <Row label="Input mode" value={talk.inputMode} />
          )}
          <Row label="Source language" value={langName(talk.sourceLanguageCode)} />

          {talk.slidesGenerated && (
            <div className="flex items-center justify-between gap-2 pt-1">
              <span className="text-muted-foreground flex items-center gap-1.5">
                <Presentation className="h-3 w-3" />
                Slideshow
              </span>
              <Badge variant="outline" className="text-xs bg-green-50 text-green-700 border-green-200 dark:bg-green-900/30 dark:text-green-400 dark:border-green-800">
                Ready
              </Badge>
            </div>
          )}

          {talk.inputMode === 'Video' && talk.videoUrl && (
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
        </CardContent>
      </Card>

      {/* Translations column */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium text-muted-foreground">Back-translation scores</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {targetLanguageCodes.length === 0 ? (
            <p className="text-muted-foreground italic">No translations</p>
          ) : (
            targetLanguageCodes.map((code) => {
              const run = latestRunByCode[code];
              return (
                <div key={code} className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2 min-w-0">
                    <Languages className="h-3.5 w-3.5 flex-shrink-0 text-muted-foreground" />
                    <span className="truncate">{langName(code)}</span>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    {run && run.status === 'Completed' ? (
                      <>
                        <span className="text-xs text-muted-foreground">
                          {Math.round(run.overallScore)}%
                        </span>
                        <Badge
                          className={cn('text-xs', outcomeColor(run.overallOutcome))}
                          variant="outline"
                        >
                          {run.overallOutcome}
                        </Badge>
                      </>
                    ) : (
                      <Badge variant="outline" className="text-xs">
                        {run ? run.status : 'No run'}
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
          {!talk.requiresQuiz ? (
            <p className="text-muted-foreground italic">Quiz not required</p>
          ) : (
            <>
              <Row label="Questions" value={String(talk.questions.length)} />
              <Row label="Passing score" value={`${talk.passingScore ?? 80}%`} />
              <Row
                label="Shuffle"
                value={talk.shuffleQuestions ? 'On' : 'Off'}
                icon={<Shuffle className="h-3 w-3" />}
              />
              <Row
                label="Retry"
                value={talk.allowRetry ? 'On' : 'Off'}
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
// Audit Metadata Panel
// ============================================

function AuditMetadataPanel({ talk }: { talk: ToolboxTalk }) {
  const fields = [
    { label: 'Reviewer', value: talk.reviewerName },
    { label: 'Organisation', value: talk.reviewerOrg },
    { label: 'Role', value: talk.reviewerRole },
    { label: 'Document Ref', value: talk.documentRef },
    { label: 'Client', value: talk.clientName },
    { label: 'Audit Purpose', value: talk.auditPurpose },
  ];

  const hasAnyValue = fields.some((f) => f.value);
  if (!hasAnyValue) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Audit & Validation</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="grid gap-x-8 gap-y-3 sm:grid-cols-2 md:grid-cols-3 text-sm">
          {fields.map(({ label, value }) =>
            value ? (
              <div key={label}>
                <dt className="text-muted-foreground text-xs font-medium">{label}</dt>
                <dd className="font-medium mt-0.5">{value}</dd>
              </div>
            ) : null
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================
// External Review Warning Banner
// ============================================

function ExternalReviewWarningBanner({
  workflowStates,
  targetLanguageCodes,
}: {
  workflowStates: TranslationWorkflowStateDto[];
  targetLanguageCodes: string[];
}) {
  const awaitingLangs = workflowStates.filter(
    (s) => targetLanguageCodes.includes(s.languageCode) && s.state === 'AwaitingThirdParty'
  );

  if (awaitingLangs.length === 0) return null;

  const count = awaitingLangs.length;
  const langList = awaitingLangs.map((s) => langName(s.languageCode)).join(', ');

  return (
    <Alert className="border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30">
      <AlertTriangle className="h-4 w-4 text-amber-600" />
      <AlertTitle className="text-amber-800 dark:text-amber-300">
        {count === 1
          ? '1 language is awaiting external review'
          : `${count} languages are awaiting external review`}
      </AlertTitle>
      <AlertDescription className="text-amber-700 dark:text-amber-400">
        <p>
          {count === 1 ? (
            <>
              When the reviewer submits, their translation will be applied to the published talk
              automatically. To update <strong>{langList}</strong> yourself before then, cancel
              the external review first.
            </>
          ) : (
            <>
              When the reviewers submit, their translations will be applied to the published talk
              automatically. To update <strong>{langList}</strong> yourself before then, cancel
              the external review for each language first.
            </>
          )}
        </p>
      </AlertDescription>
    </Alert>
  );
}

// ============================================
// Row helper
// ============================================

function Row({ label, value, icon }: { label: string; value: string; icon?: React.ReactNode }) {
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

// ============================================
// Success state (exported for use by page wrapper)
// ============================================

export function PublishSuccessState({ talkId, title }: { talkId: string; title: string | null }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <div className="rounded-full bg-green-100 p-4 dark:bg-green-900/30 mb-6">
        <CheckCircle2 className="h-12 w-12 text-green-600 dark:text-green-400" />
      </div>
      <h2 className="text-2xl font-bold mb-2">Talk Published</h2>
      {title && <p className="text-muted-foreground mb-8 text-lg font-medium">{title}</p>}
    </div>
  );
}
