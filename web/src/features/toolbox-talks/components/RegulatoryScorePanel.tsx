'use client';

import { useState } from 'react';
import { Loader2, ChevronDown, ChevronRight, ArrowUp, ArrowDown, ArrowRight, CheckCircle2, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';
import {
  useRegulatoryScoreHistory,
  useTriggerRegulatoryScore,
} from '@/lib/api/toolbox-talks/use-content-creation';
import type {
  ValidationScoreType,
  RegulatoryScoreResultDto,
  CategoryScoreDto,
} from '@/types/content-creation';

// ============================================
// Types
// ============================================

interface RegulatoryScorePanelProps {
  talkId?: string;
  courseId?: string;
  runId: string;
  sectorKey: string | null;
  targetLanguage: string;
  hasCompletedSections: boolean;
}

interface ScoreColumnConfig {
  scoreType: ValidationScoreType;
  header: string;
  description: string;
  buttonLabel: string;
  rerunLabel?: (count: number) => string;
  accent: {
    text: string;
    border: string;
    bg: string;
    callout: string;
  };
}

// ============================================
// Column Configuration
// ============================================

const COLUMNS: ScoreColumnConfig[] = [
  {
    scoreType: 'SourceDocument',
    header: 'SOURCE DOCUMENT',
    description: 'How well does the source document meet regulatory standards? Establishes the baseline.',
    buttonLabel: 'Score Source Document',
    accent: {
      text: 'text-indigo-600',
      border: 'border-indigo-200',
      bg: 'bg-indigo-50',
      callout: 'border-l-indigo-400',
    },
  },
  {
    scoreType: 'PureTranslation',
    header: 'PURE TRANSLATION',
    description: 'Linguistic quality only — accuracy, fluency, completeness. No regulatory overlay.',
    buttonLabel: 'Score Translation Quality',
    accent: {
      text: 'text-teal-600',
      border: 'border-teal-200',
      bg: 'bg-teal-50',
      callout: 'border-l-teal-400',
    },
  },
  {
    scoreType: 'RegulatoryTranslation',
    header: 'REGULATORY TRANSLATION',
    description: 'How well does the translation render the source to regulatory standard?',
    buttonLabel: 'Score Translation',
    rerunLabel: (count: number) => `Re-run Score (${count} runs)`,
    accent: {
      text: 'text-blue-600',
      border: 'border-blue-200',
      bg: 'bg-blue-50',
      callout: 'border-l-blue-400',
    },
  },
];

// ============================================
// Helpers
// ============================================

function scoreColor(score: number): string {
  if (score >= 90) return 'text-green-600';
  if (score >= 75) return 'text-amber-600';
  return 'text-red-600';
}

function scoreBgColor(score: number): string {
  if (score >= 90) return 'bg-green-500';
  if (score >= 75) return 'bg-amber-500';
  return 'bg-red-500';
}

function verdictPillClass(verdict: string): string {
  const v = verdict.toUpperCase();
  if (v.includes('APPROVED') || v.includes('PASS') || v.includes('GOOD') || v.includes('EXCELLENT'))
    return 'bg-green-100 text-green-800';
  if (v.includes('REVIEW') || v.includes('ADEQUATE') || v.includes('FAIR'))
    return 'bg-amber-100 text-amber-800';
  return 'bg-red-100 text-red-800';
}

function getLatestScore(
  history: { sourceScore: RegulatoryScoreResultDto | null; pureScore: RegulatoryScoreResultDto | null; regulatoryScores: RegulatoryScoreResultDto[] },
  scoreType: ValidationScoreType
): RegulatoryScoreResultDto | null {
  switch (scoreType) {
    case 'SourceDocument':
      return history.sourceScore;
    case 'PureTranslation':
      return history.pureScore;
    case 'RegulatoryTranslation':
      return history.regulatoryScores[history.regulatoryScores.length - 1] ?? null;
  }
}

// ============================================
// Component
// ============================================

export function RegulatoryScorePanel({
  talkId,
  courseId,
  runId,
  sectorKey,
  targetLanguage,
  hasCompletedSections,
}: RegulatoryScorePanelProps) {
  // parentId is used only as a query key identifier — the actual API calls use runId
  const parentId = talkId ?? courseId ?? '';
  const { data: history, isLoading } = useRegulatoryScoreHistory(parentId, runId);
  const scoreMutation = useTriggerRegulatoryScore();
  const [scoringType, setScoringType] = useState<ValidationScoreType | null>(null);

  const handleScore = (scoreType: ValidationScoreType) => {
    setScoringType(scoreType);
    scoreMutation.mutate(
      { talkId: parentId, runId, scoreType },
      {
        onSuccess: () => {
          toast.success('Score assessment completed');
          setScoringType(null);
        },
        onError: (error) => {
          toast.error(
            error instanceof Error ? error.message : 'Failed to run score assessment'
          );
          setScoringType(null);
        },
      }
    );
  };

  const isScoring = scoreMutation.isPending;

  // Determine if "APPROVED FOR DISTRIBUTION" verdict
  const latestRegScore = history?.regulatoryScores[history.regulatoryScores.length - 1] ?? null;
  const isApproved = latestRegScore?.verdict === 'APPROVED FOR DISTRIBUTION';

  // Available scores for comparison bar
  const sourceScore = history?.sourceScore ?? null;
  const pureScore = history?.pureScore ?? null;
  const regScore = latestRegScore;
  const availableScores = [sourceScore, pureScore, regScore].filter(Boolean) as RegulatoryScoreResultDto[];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Regulatory Score</CardTitle>
        <p className="text-sm text-muted-foreground">
          Score this translation against regulatory standards. Run before and after remediation to track improvement.
        </p>
        {!sectorKey && (
          <div className="mt-2 flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
            <span className="text-amber-800">
              No sector configured for this run. Select a sector to enable regulatory scoring.
            </span>
          </div>
        )}
      </CardHeader>
      <CardContent className="space-y-6">
        {/* Three score columns */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {COLUMNS.map((col) => {
            const score = history ? getLatestScore(history, col.scoreType) : null;
            const isThisScoring = isScoring && scoringType === col.scoreType;
            const regRunCount = history?.regulatoryScores.length ?? 0;
            const isDisabled =
              isScoring ||
              !hasCompletedSections ||
              (col.scoreType === 'RegulatoryTranslation' && !sectorKey) ||
              (col.scoreType === 'RegulatoryTranslation' && isApproved);

            // Use scoreLabel from history for regulatory column header
            const displayHeader =
              col.scoreType === 'RegulatoryTranslation' && sectorKey && score?.scoreLabel
                ? score.scoreLabel
                : col.header;

            // Button label
            const buttonLabel =
              col.scoreType === 'RegulatoryTranslation' && regRunCount > 0 && col.rerunLabel
                ? col.rerunLabel(regRunCount)
                : col.buttonLabel;

            return (
              <ScoreColumn
                key={col.scoreType}
                config={col}
                displayHeader={displayHeader}
                score={score}
                isScoring={isThisScoring}
                isDisabled={isDisabled}
                buttonLabel={buttonLabel}
                sectorKey={sectorKey}
                onScore={() => handleScore(col.scoreType)}
              />
            );
          })}
        </div>

        {/* Approved banner */}
        {isApproved && latestRegScore && (
          <div className="flex items-center gap-2 rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-800">
            <CheckCircle2 className="h-4 w-4 shrink-0 text-green-600" />
            <span>
              {latestRegScore.regulatoryBody} Approved for Distribution — use the inspection export for submission.
            </span>
          </div>
        )}

        {/* Comparison bar */}
        {availableScores.length >= 2 && (
          <ComparisonBar
            sourceScore={sourceScore}
            pureScore={pureScore}
            regScore={regScore}
          />
        )}
      </CardContent>
    </Card>
  );
}

// ============================================
// ScoreColumn sub-component
// ============================================

function ScoreColumn({
  config,
  displayHeader,
  score,
  isScoring,
  isDisabled,
  buttonLabel,
  sectorKey,
  onScore,
}: {
  config: ScoreColumnConfig;
  displayHeader: string;
  score: RegulatoryScoreResultDto | null;
  isScoring: boolean;
  isDisabled: boolean;
  buttonLabel: string;
  sectorKey: string | null;
  onScore: () => void;
}) {
  const [showFindings, setShowFindings] = useState(false);
  const needsTooltip = config.scoreType === 'RegulatoryTranslation' && !sectorKey;

  const triggerButton = (
    <Button
      size="sm"
      variant="outline"
      className={cn('w-full', config.accent.border)}
      disabled={isDisabled}
      onClick={onScore}
    >
      {isScoring ? (
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
      ) : null}
      {isScoring ? 'Scoring...' : buttonLabel}
    </Button>
  );

  return (
    <div className={cn('rounded-lg border p-4 space-y-4', config.accent.border)}>
      {/* Header */}
      <div>
        <h4 className={cn('text-xs font-medium uppercase tracking-wider font-mono', config.accent.text)}>
          {displayHeader}
        </h4>
        <p className="mt-1 text-xs text-muted-foreground">{config.description}</p>
      </div>

      {/* Score display */}
      {score && (
        <div className="space-y-3">
          {/* Large score + verdict */}
          <div className="flex items-center gap-3">
            <span className={cn('text-3xl font-bold tabular-nums', scoreColor(score.overallScore))}>
              {score.overallScore}
            </span>
            <div className="space-y-1">
              <Badge className={cn('text-xs', verdictPillClass(score.verdict))}>
                {score.verdict}
              </Badge>
              {score.comparisonDelta != null && (
                <DeltaIndicator delta={score.comparisonDelta} />
              )}
            </div>
          </div>

          {/* Category breakdown */}
          <div className="space-y-2">
            {score.categoryScores.map((cat) => (
              <CategoryRow key={cat.key} category={cat} accent={config.accent} />
            ))}
          </div>

          {/* Summary callout */}
          {score.summary && (
            <div className={cn('border-l-4 pl-3 py-2 text-xs text-muted-foreground leading-relaxed', config.accent.callout)}>
              {score.summary}
            </div>
          )}

          {/* Expandable findings */}
          <button
            type="button"
            onClick={() => setShowFindings(!showFindings)}
            className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            {showFindings ? (
              <ChevronDown className="h-3 w-3" />
            ) : (
              <ChevronRight className="h-3 w-3" />
            )}
            Full findings
          </button>
          {showFindings && score.fullResponse && (
            <div className="rounded border bg-muted/20 p-2 text-xs font-mono whitespace-pre-wrap max-h-60 overflow-y-auto">
              {score.fullResponse}
            </div>
          )}

          {/* Meta */}
          <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
            <span>{score.scoredSectionCount} sections scored</span>
            {score.regulatoryBody && (
              <>
                <span>·</span>
                <span>{score.regulatoryBody}</span>
              </>
            )}
            <span>·</span>
            <span>{score.runLabel}</span>
          </div>
        </div>
      )}

      {/* Trigger button */}
      {needsTooltip ? (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="block">{triggerButton}</span>
            </TooltipTrigger>
            <TooltipContent>
              <p>No sector configured — cannot run regulatory scoring</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      ) : (
        triggerButton
      )}
    </div>
  );
}

// ============================================
// CategoryRow sub-component
// ============================================

function CategoryRow({
  category,
  accent,
}: {
  category: CategoryScoreDto;
  accent: ScoreColumnConfig['accent'];
}) {
  return (
    <div className="space-y-0.5">
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground">
          {category.label}
          {category.weight > 1 && (
            <span className={cn('ml-1 font-medium', accent.text)}>
              ×{category.weight}
            </span>
          )}
        </span>
        <span className={cn('tabular-nums font-medium', scoreColor(category.score))}>
          {category.score}%
        </span>
      </div>
      <Progress
        value={category.score}
        className={cn('h-1.5', category.score >= 90 ? '[&>[data-slot=progress-indicator]]:bg-green-500' : category.score >= 75 ? '[&>[data-slot=progress-indicator]]:bg-amber-500' : '[&>[data-slot=progress-indicator]]:bg-red-500')}
      />
    </div>
  );
}

// ============================================
// DeltaIndicator sub-component
// ============================================

function DeltaIndicator({ delta }: { delta: number }) {
  if (delta === 0) return null;
  const isPositive = delta > 0;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-0.5 text-xs font-medium',
        isPositive ? 'text-green-600' : 'text-red-600'
      )}
    >
      {isPositive ? (
        <ArrowUp className="h-3 w-3" />
      ) : (
        <ArrowDown className="h-3 w-3" />
      )}
      {Math.abs(delta)}
    </span>
  );
}

// ============================================
// ComparisonBar sub-component
// ============================================

function ComparisonBar({
  sourceScore,
  pureScore,
  regScore,
}: {
  sourceScore: RegulatoryScoreResultDto | null;
  pureScore: RegulatoryScoreResultDto | null;
  regScore: RegulatoryScoreResultDto | null;
}) {
  const items: { label: string; score: number }[] = [];
  if (sourceScore) items.push({ label: 'Source', score: sourceScore.overallScore });
  if (pureScore) items.push({ label: 'Pure', score: pureScore.overallScore });
  if (regScore) items.push({ label: 'Regulatory', score: regScore.overallScore });

  if (items.length < 2) return null;

  // Interpretive text
  let interpretation = '';
  if (sourceScore && regScore) {
    if (regScore.overallScore > sourceScore.overallScore) {
      interpretation = 'The translation improves on the source document\'s regulatory quality.';
    } else if (regScore.overallScore < sourceScore.overallScore) {
      interpretation = 'The translation introduces issues not present in the source document.';
    } else {
      interpretation = 'The translation faithfully reflects the source document\'s regulatory quality.';
    }
  } else if (sourceScore && pureScore && !regScore) {
    interpretation = 'Linguistic quality assessed — run regulatory score to complete the comparison.';
  }

  return (
    <div className="space-y-2 rounded-lg border bg-muted/20 p-4">
      <div className="flex items-center justify-center gap-3 flex-wrap">
        {items.map((item, i) => (
          <span key={item.label} className="flex items-center gap-2">
            {i > 0 && (
              <span className="flex items-center gap-1 text-xs text-muted-foreground">
                <ArrowRight className="h-3 w-3" />
                {items[i - 1] && (
                  <span
                    className={cn(
                      'tabular-nums font-medium',
                      item.score - items[i - 1].score > 0
                        ? 'text-green-600'
                        : item.score - items[i - 1].score < 0
                          ? 'text-red-600'
                          : 'text-muted-foreground'
                    )}
                  >
                    {item.score - items[i - 1].score > 0 ? '+' : ''}
                    {item.score - items[i - 1].score}
                  </span>
                )}
              </span>
            )}
            <span className="flex items-center gap-1.5">
              <span className={cn('text-lg font-bold tabular-nums', scoreColor(item.score))}>
                {item.score}
              </span>
              <span className="text-xs text-muted-foreground">{item.label}</span>
            </span>
          </span>
        ))}
      </div>
      {interpretation && (
        <p className="text-center text-xs text-muted-foreground">{interpretation}</p>
      )}
    </div>
  );
}
