'use client';

import { useState, useMemo, useEffect, useRef } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import {
  ChevronDown,
  ChevronRight,
  Check,
  X,
  Pencil,
  RefreshCw,
  ShieldAlert,
  AlertTriangle,
  Loader2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type {
  SectionValidationResult,
  ValidationOutcome,
  ReviewerDecision,
} from '@/types/content-creation';

// ============================================
// Types
// ============================================

interface GlossaryMismatch {
  term: string;
  expected: string;
  actual: string;
}

interface ValidationSectionCardProps {
  sectionIndex: number;
  sectionTitle: string;
  result: SectionValidationResult | null;
  isRunning: boolean;
  languageCode: string;
  passThreshold: number;
  onAccept: () => void;
  onEdit: (editedTranslation: string) => void;
  onRetry: () => void;
  isDecisionPending: boolean;
  defaultExpanded?: boolean;
  /** When true, hides Accept/Reject/Edit/Retry action buttons */
  readOnly?: boolean;
}

// ============================================
// Helpers
// ============================================

const outcomeColor: Record<ValidationOutcome, string> = {
  Pass: 'border-green-200 bg-green-50/50',
  Review: 'border-amber-200 bg-amber-50/50',
  Fail: 'border-red-200 bg-red-50/50',
};

const outcomePillColor: Record<ValidationOutcome, string> = {
  Pass: 'bg-green-100 text-green-800',
  Review: 'bg-amber-100 text-amber-800',
  Fail: 'bg-red-100 text-red-800',
};

const decisionBadge: Record<
  Exclude<ReviewerDecision, 'Pending'>,
  { label: string; className: string; icon: typeof Check }
> = {
  Accepted: {
    label: 'Accepted',
    className: 'bg-green-100 text-green-800',
    icon: Check,
  },
  Rejected: {
    label: 'Rejected',
    className: 'bg-red-100 text-red-800',
    icon: X,
  },
  Edited: {
    label: 'Edited',
    className: 'bg-blue-100 text-blue-800',
    icon: Pencil,
  },
};

function parseJsonSafe<T>(json: string | null, fallback: T): T {
  if (!json) return fallback;
  try {
    return JSON.parse(json);
  } catch {
    return fallback;
  }
}

// ============================================
// Component
// ============================================

export function ValidationSectionCard({
  sectionIndex,
  sectionTitle,
  result,
  isRunning,
  languageCode,
  passThreshold,
  onAccept,
  onEdit,
  onRetry,
  isDecisionPending,
  defaultExpanded = false,
  readOnly = false,
}: ValidationSectionCardProps) {
  const [isExpanded, setIsExpanded] = useState(defaultExpanded);
  const [isEditing, setIsEditing] = useState(false);
  const [editText, setEditText] = useState('');

  // Auto-collapse when a section is accepted
  const prevDecision = useRef(result?.reviewerDecision);
  useEffect(() => {
    if (
      result?.reviewerDecision === 'Accepted' &&
      prevDecision.current !== 'Accepted'
    ) {
      setIsExpanded(false);
    }
    prevDecision.current = result?.reviewerDecision;
  }, [result?.reviewerDecision]);

  const sectionLabel = `L${String(sectionIndex + 1).padStart(2, '0')}`;
  const isPending = !result && !isRunning;
  const hasDecision =
    result?.reviewerDecision && result.reviewerDecision !== 'Pending';

  const glossaryMismatches = useMemo(
    () =>
      parseJsonSafe<GlossaryMismatch[]>(result?.glossaryMismatches ?? null, []),
    [result?.glossaryMismatches]
  );

  const criticalTerms = useMemo(
    () => parseJsonSafe<string[]>(result?.criticalTerms ?? null, []),
    [result?.criticalTerms]
  );

  const agreement = result
    ? 100 - Math.abs(result.scoreA - result.scoreB)
    : 0;

  // ============================================
  // Edit handlers
  // ============================================

  function startEdit() {
    setEditText(result?.editedTranslation ?? result?.translatedText ?? '');
    setIsEditing(true);
  }

  function cancelEdit() {
    setIsEditing(false);
    setEditText('');
  }

  function submitEdit() {
    if (editText.trim()) {
      onEdit(editText.trim());
      setIsEditing(false);
    }
  }

  // ============================================
  // Render: Header (always visible)
  // ============================================

  const renderHeader = () => (
    <button
      type="button"
      onClick={() => result && setIsExpanded(!isExpanded)}
      className={cn(
        'flex w-full items-center gap-3 px-4 py-3 text-left',
        result && 'cursor-pointer hover:bg-muted/30'
      )}
      disabled={!result}
    >
      {/* Expand/Collapse icon */}
      {result ? (
        isExpanded ? (
          <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
        ) : (
          <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
        )
      ) : (
        <span className="h-4 w-4 shrink-0" />
      )}

      {/* Section label badge */}
      <Badge variant="outline" className="shrink-0 font-mono text-xs">
        {sectionLabel}
      </Badge>

      {/* Title */}
      <span className="flex-1 truncate text-sm font-medium">
        {sectionTitle}
      </span>

      {/* Safety-critical badge */}
      {result?.isSafetyCritical && (
        <Badge
          variant="outline"
          className="shrink-0 gap-1 border-orange-300 bg-orange-50 text-orange-700"
        >
          <ShieldAlert className="h-3 w-3" />
          Safety
        </Badge>
      )}

      {/* Score */}
      {result && (
        <span
          className={cn(
            'shrink-0 tabular-nums text-sm font-semibold',
            result.outcome === 'Pass' && 'text-green-700',
            result.outcome === 'Review' && 'text-amber-700',
            result.outcome === 'Fail' && 'text-red-700'
          )}
        >
          {Math.round(result.finalScore)}
        </span>
      )}

      {/* Outcome pill / status */}
      {result ? (
        <Badge className={cn('shrink-0', outcomePillColor[result.outcome])}>
          {result.outcome}
        </Badge>
      ) : isRunning ? (
        <Badge className="shrink-0 animate-pulse bg-blue-100 text-blue-800">
          <Loader2 className="mr-1 h-3 w-3 animate-spin" />
          Running
        </Badge>
      ) : (
        <Badge variant="secondary" className="shrink-0">
          Pending
        </Badge>
      )}

      {/* Decision badge */}
      {hasDecision && result?.reviewerDecision !== 'Pending' && (
        <Badge
          className={cn(
            'shrink-0 gap-1',
            decisionBadge[result!.reviewerDecision as Exclude<ReviewerDecision, 'Pending'>]
              .className
          )}
        >
          {(() => {
            const db =
              decisionBadge[
                result!.reviewerDecision as Exclude<ReviewerDecision, 'Pending'>
              ];
            const Icon = db.icon;
            return <Icon className="h-3 w-3" />;
          })()}
          {
            decisionBadge[
              result!.reviewerDecision as Exclude<ReviewerDecision, 'Pending'>
            ].label
          }
        </Badge>
      )}
    </button>
  );

  // ============================================
  // Render: Body (expanded)
  // ============================================

  const renderBody = () => {
    if (!result || !isExpanded) return null;

    return (
      <div className="space-y-4 border-t px-4 py-4">
        {/* Original / Translation side-by-side */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <div className="space-y-1">
            <div className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
              Original (English)
            </div>
            <div className="rounded-md border bg-muted/20 p-3 text-sm leading-relaxed">
              {result.originalText}
            </div>
          </div>
          <div className="space-y-1">
            <div className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
              Translation ({languageCode.toUpperCase()})
            </div>
            {isEditing ? (
              <div className="space-y-2">
                <Textarea
                  value={editText}
                  onChange={(e) => setEditText(e.target.value)}
                  className="min-h-[120px] text-sm"
                  autoFocus
                />
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    onClick={submitEdit}
                    disabled={!editText.trim() || isDecisionPending}
                  >
                    {isDecisionPending ? (
                      <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                    ) : (
                      <RefreshCw className="mr-1 h-3 w-3" />
                    )}
                    Re-validate
                  </Button>
                  <Button size="sm" variant="outline" onClick={cancelEdit}>
                    Cancel
                  </Button>
                </div>
              </div>
            ) : (
              <div className="rounded-md border bg-muted/20 p-3 text-sm leading-relaxed">
                {result.editedTranslation ?? result.translatedText}
              </div>
            )}
          </div>
        </div>

        {/* Glossary mismatch warning */}
        {glossaryMismatches.length > 0 && (
          <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
            <div className="space-y-1">
              <span className="font-medium text-amber-800">
                Glossary Mismatch
              </span>
              {glossaryMismatches.map((m, i) => (
                <div key={i} className="text-amber-700">
                  &quot;{m.term}&quot; — expected &quot;{m.expected}&quot;, got
                  &quot;{m.actual}&quot;
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Back-translations side-by-side */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wider text-muted-foreground">
              Back-translation A
              <Badge
                variant="outline"
                className="text-[10px] font-normal normal-case"
              >
                Claude Haiku
              </Badge>
            </div>
            <div className="rounded-md border bg-muted/20 p-3 text-sm leading-relaxed">
              {result.backTranslationA ?? (
                <span className="italic text-muted-foreground">
                  Not available
                </span>
              )}
            </div>
          </div>
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wider text-muted-foreground">
              Back-translation B
              <Badge
                variant="outline"
                className="text-[10px] font-normal normal-case"
              >
                DeepL
              </Badge>
            </div>
            <div className="rounded-md border bg-muted/20 p-3 text-sm leading-relaxed">
              {result.backTranslationB ?? (
                <span className="italic text-muted-foreground">
                  Not available
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Scores row */}
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2 rounded-md border bg-muted/20 px-4 py-2.5 text-sm">
          <ScoreItem label="A vs Original" value={result.scoreA} />
          <ScoreItem label="B vs Original" value={result.scoreB} />
          <ScoreItem label="A+B Agreement" value={agreement} />
          <ScoreItem
            label="Consensus"
            value={result.finalScore}
            highlight={result.outcome}
          />
          <span
            className={cn(
              'font-medium',
              result.outcome === 'Pass' && 'text-green-700',
              result.outcome === 'Review' && 'text-amber-700',
              result.outcome === 'Fail' && 'text-red-700'
            )}
          >
            {result.outcome === 'Pass'
              ? 'Verified'
              : result.outcome === 'Review'
                ? 'Marginal'
                : 'Insufficient'}
          </span>
        </div>

        {/* Round indicator + Safety note */}
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground">Rounds:</span>
            <div className="flex gap-1">
              {[1, 2, 3].map((r) => (
                <span
                  key={r}
                  className={cn(
                    'inline-block h-2 w-6 rounded-full',
                    r <= result.roundsUsed ? 'bg-primary' : 'bg-muted'
                  )}
                />
              ))}
            </div>
            <span className="text-xs text-muted-foreground">
              ({result.roundsUsed}/3)
            </span>
          </div>

          {result.isSafetyCritical && (
            <span className="text-xs text-orange-600">
              Safety threshold: {result.effectiveThreshold} (base{' '}
              {passThreshold} + {result.effectiveThreshold - passThreshold}{' '}
              bump)
            </span>
          )}
        </div>

        {/* Critical terms */}
        {criticalTerms.length > 0 && (
          <div className="flex flex-wrap gap-1">
            <span className="text-xs text-muted-foreground">
              Critical terms:
            </span>
            {criticalTerms.map((term, i) => (
              <Badge
                key={i}
                variant="outline"
                className="border-orange-200 text-xs text-orange-700"
              >
                {term}
              </Badge>
            ))}
          </div>
        )}

        {/* Action buttons */}
        {!readOnly && !isEditing && (
          <div className="flex gap-2 border-t pt-3">
            <Button
              size="sm"
              variant={
                result.reviewerDecision === 'Accepted' ? 'default' : 'outline'
              }
              className={
                result.reviewerDecision === 'Accepted'
                  ? 'bg-green-600 hover:bg-green-700'
                  : ''
              }
              onClick={onAccept}
              disabled={isDecisionPending}
            >
              <Check className="mr-1 h-3 w-3" />
              Accept
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={startEdit}
              disabled={isDecisionPending}
            >
              <Pencil className="mr-1 h-3 w-3" />
              Edit
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={onRetry}
              disabled={isDecisionPending}
            >
              {isDecisionPending ? (
                <Loader2 className="mr-1 h-3 w-3 animate-spin" />
              ) : (
                <RefreshCw className="mr-1 h-3 w-3" />
              )}
              Retry
            </Button>
          </div>
        )}
      </div>
    );
  };

  // ============================================
  // Render
  // ============================================

  return (
    <div
      className={cn(
        'overflow-hidden rounded-lg border transition-colors',
        result
          ? outcomeColor[result.outcome]
          : isRunning
            ? 'border-blue-200 bg-blue-50/30'
            : 'border-muted bg-muted/10'
      )}
    >
      {renderHeader()}
      {renderBody()}
    </div>
  );
}

// ============================================
// Score display sub-component
// ============================================

function ScoreItem({
  label,
  value,
  highlight,
}: {
  label: string;
  value: number;
  highlight?: ValidationOutcome;
}) {
  return (
    <span className="flex items-center gap-1.5">
      <span className="text-muted-foreground">{label}:</span>
      <span
        className={cn(
          'tabular-nums font-medium',
          highlight === 'Pass' && 'text-green-700',
          highlight === 'Review' && 'text-amber-700',
          highlight === 'Fail' && 'text-red-700'
        )}
      >
        {Math.round(value)}
      </span>
    </span>
  );
}
