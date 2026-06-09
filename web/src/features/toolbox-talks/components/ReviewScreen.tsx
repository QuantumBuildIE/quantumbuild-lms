'use client';

import { useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import { toast } from 'sonner';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ValidationSectionCard } from './create-wizard/steps/validate/ValidationSectionCard';
import {
  acceptSection,
  editSection,
  retrySection,
  useAcceptTranslation,
} from '@/lib/api/toolbox-talks';
import type { ValidationRunDetail } from '@/types/content-creation';

interface ReviewScreenProps {
  toolboxTalkId: string;
  languageCode: string;
  runId: string;
  runDetail: ValidationRunDetail;
  canAccept: boolean;
  onSectionsChanged: () => void;
}

export function ReviewScreen({
  toolboxTalkId,
  languageCode,
  runId,
  runDetail,
  canAccept,
  onSectionsChanged,
}: ReviewScreenProps) {
  const router = useRouter();
  const acceptTranslation = useAcceptTranslation();

  const [pendingBySectionIndex, setPendingBySectionIndex] = useState<
    Record<number, 'accepting' | 'editing' | 'retrying' | null>
  >({});

  const handleSectionDecision = useCallback(
    async (
      sectionIndex: number,
      pendingLabel: 'accepting' | 'editing' | 'retrying',
      apiCall: () => Promise<void>
    ) => {
      setPendingBySectionIndex((prev) => ({ ...prev, [sectionIndex]: pendingLabel }));
      try {
        await apiCall();
        const label =
          pendingLabel === 'accepting'
            ? 'accepted'
            : pendingLabel === 'editing'
              ? 'edited & re-validating'
              : 'retrying';
        toast.success(`Section ${sectionIndex + 1} ${label}`);
        onSectionsChanged();
      } catch (e) {
        const isConflict = e instanceof Error && e.message.includes('409');
        toast.error(isConflict ? 'Revalidation already in progress' : 'Action failed', {
          description: isConflict
            ? 'A revalidation is already running for this section.'
            : e instanceof Error
              ? e.message
              : 'Unknown error',
        });
      } finally {
        setPendingBySectionIndex((prev) => ({ ...prev, [sectionIndex]: null }));
      }
    },
    [onSectionsChanged]
  );

  const hasEditedSource = runDetail.results.some((r) => r.editedSource != null);

  const completedDate = runDetail.completedAt
    ? format(new Date(runDetail.completedAt), 'dd MMM yyyy HH:mm')
    : runDetail.startedAt
      ? format(new Date(runDetail.startedAt), 'dd MMM yyyy HH:mm')
      : format(new Date(runDetail.createdAt), 'dd MMM yyyy HH:mm');

  return (
    <div className="space-y-6">
      {/* Metadata strip */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Run Details</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <label className="text-xs font-medium uppercase text-muted-foreground">
                Language
              </label>
              <p className="mt-1">
                <Badge variant="outline" className="font-mono">
                  {runDetail.languageCode.toUpperCase()}
                </Badge>
              </p>
            </div>
            <div>
              <label className="text-xs font-medium uppercase text-muted-foreground">
                Source Language
              </label>
              <p className="mt-1 text-sm">{runDetail.sourceLanguage}</p>
            </div>
            {runDetail.sourceDialect && (
              <div>
                <label className="text-xs font-medium uppercase text-muted-foreground">
                  Source Dialect
                </label>
                <p className="mt-1 text-sm">{runDetail.sourceDialect}</p>
              </div>
            )}
            <div>
              <label className="text-xs font-medium uppercase text-muted-foreground">
                Pass Threshold
              </label>
              <p className="mt-1 text-sm">{runDetail.passThreshold}%</p>
            </div>
            {runDetail.sectorKey && (
              <div>
                <label className="text-xs font-medium uppercase text-muted-foreground">
                  Sector
                </label>
                <p className="mt-1 text-sm">{runDetail.sectorKey}</p>
              </div>
            )}
            {runDetail.reviewerName && (
              <div>
                <label className="text-xs font-medium uppercase text-muted-foreground">
                  Reviewer
                </label>
                <p className="mt-1 text-sm">{runDetail.reviewerName}</p>
              </div>
            )}
            {runDetail.reviewerOrg && (
              <div>
                <label className="text-xs font-medium uppercase text-muted-foreground">
                  Organisation
                </label>
                <p className="mt-1 text-sm">{runDetail.reviewerOrg}</p>
              </div>
            )}
            {runDetail.documentRef && (
              <div>
                <label className="text-xs font-medium uppercase text-muted-foreground">
                  Document Ref
                </label>
                <p className="mt-1 text-sm">{runDetail.documentRef}</p>
              </div>
            )}
            {runDetail.clientName && (
              <div>
                <label className="text-xs font-medium uppercase text-muted-foreground">
                  Client
                </label>
                <p className="mt-1 text-sm">{runDetail.clientName}</p>
              </div>
            )}
            {runDetail.auditPurpose && (
              <div className="sm:col-span-2">
                <label className="text-xs font-medium uppercase text-muted-foreground">
                  Audit Purpose
                </label>
                <p className="mt-1 text-sm">{runDetail.auditPurpose}</p>
              </div>
            )}
            <div>
              <label className="text-xs font-medium uppercase text-muted-foreground">
                Completed
              </label>
              <p className="mt-1 text-sm">{completedDate}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Section results */}
      <div className="space-y-3">
        {runDetail.results.map((result) => (
          <ValidationSectionCard
            key={result.id}
            sectionIndex={result.sectionIndex}
            sectionTitle={result.sectionTitle}
            result={result}
            isRunning={false}
            languageCode={runDetail.languageCode}
            passThreshold={runDetail.passThreshold}
            onAccept={() =>
              handleSectionDecision(result.sectionIndex, 'accepting', () =>
                acceptSection(toolboxTalkId, runId, result.sectionIndex)
              )
            }
            onEdit={(editedTranslation, editedOriginalText) =>
              handleSectionDecision(result.sectionIndex, 'editing', () =>
                editSection(
                  toolboxTalkId,
                  runId,
                  result.sectionIndex,
                  editedTranslation,
                  editedOriginalText,
                  true
                )
              )
            }
            onRetry={() =>
              handleSectionDecision(result.sectionIndex, 'retrying', () =>
                retrySection(toolboxTalkId, runId, result.sectionIndex)
              )
            }
            isDecisionPending={!!pendingBySectionIndex[result.sectionIndex]}
            readOnly={false}
            defaultExpanded={false}
          />
        ))}
      </div>

      {/* Edited source notice */}
      {hasEditedSource && (
        <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
          <p className="text-amber-700">
            One or more sections have edits to the English source. Accepting will update the source
            content and mark other-language translations as needing re-validation.
          </p>
        </div>
      )}

      {/* Bottom action bar */}
      <div className="flex justify-end rounded-lg border bg-muted/30 p-4">
        <Button
          disabled={!canAccept || acceptTranslation.isPending}
          onClick={() => {
            acceptTranslation.mutate(
              { toolboxTalkId, languageCode },
              {
                onSuccess: () => {
                  toast.success(
                    `${languageCode.toUpperCase()} translation accepted as final`
                  );
                  router.push(`/admin/toolbox-talks/talks/${toolboxTalkId}`);
                },
                onError: () => {
                  toast.error('Failed to accept translation');
                },
              }
            );
          }}
        >
          {acceptTranslation.isPending && (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          )}
          Accept this language as final
        </Button>
      </div>
    </div>
  );
}
