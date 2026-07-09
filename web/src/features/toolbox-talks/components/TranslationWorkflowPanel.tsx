'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import { Languages, History, ClipboardCheck, Send, X, CheckCircle2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { WorkflowStateBadge } from './WorkflowStateBadge';
import { WorkflowHistoryModal } from './WorkflowHistoryModal';
import {
  useAvailableLanguages,
  useGenerateContentTranslations,
  useWorkflowStates,
  useValidateTranslation,
  useInitiateExternalReview,
  useCancelExternalReview,
  useAcceptTranslation,
} from '@/lib/api/toolbox-talks';
import { SendExternalReviewDialog } from './SendExternalReviewDialog';
import { CancelExternalReviewDialog } from './CancelExternalReviewDialog';
import type { ToolboxTalkSection, ToolboxTalkTranslation } from '@/types/toolbox-talks';
import type { TranslationWorkflowState, ValidationOutcome } from '@/types/workflows';

interface TranslationWorkflowPanelProps {
  toolboxTalkId: string;
  existingTranslations: ToolboxTalkTranslation[];
  sections: ToolboxTalkSection[];
}

const outcomePillClass: Record<ValidationOutcome, string> = {
  Pass: 'bg-green-100 text-green-800',
  Review: 'bg-amber-100 text-amber-800',
  Fail: 'bg-red-100 text-red-800',
};

function isTranslateButtonEnabled(state: TranslationWorkflowState): boolean {
  return state === 'Initial' || state === 'Stale' || state === 'Accepted';
}

function canValidate(state: TranslationWorkflowState): boolean {
  return state === 'AIGenerated';
}

function canReview(state: TranslationWorkflowState): boolean {
  return state === 'Validated' || state === 'ReviewerAccepted';
}

function canAcceptExternalReview(state: TranslationWorkflowState): boolean {
  return state === 'ThirdPartyReviewed';
}

function canSendForExternalReview(state: TranslationWorkflowState): boolean {
  return state === 'Validated' || state === 'ReviewerAccepted';
}

function canCancelExternalReview(state: TranslationWorkflowState): boolean {
  return state === 'AwaitingThirdParty';
}

export function TranslationWorkflowPanel({
  toolboxTalkId,
  existingTranslations,
  sections,
}: TranslationWorkflowPanelProps) {
  const router = useRouter();
  const { data: languagesData } = useAvailableLanguages();
  const { data: workflowStates } = useWorkflowStates(toolboxTalkId);
  const generateMutation = useGenerateContentTranslations();
  const validateMutation = useValidateTranslation();
  const initiateExternalReviewMutation = useInitiateExternalReview();
  const cancelExternalReviewMutation = useCancelExternalReview();
  const acceptTranslationMutation = useAcceptTranslation();

  const [pendingByLanguage, setPendingByLanguage] = useState<
    Record<string, 'translating' | 'validating' | 'accepting' | null>
  >({});
  const [overwriteLanguageCode, setOverwriteLanguageCode] = useState<string | null>(null);
  const [overwriteLanguageName, setOverwriteLanguageName] = useState<string | null>(null);
  const [overwriteWasExternallyReviewed, setOverwriteWasExternallyReviewed] = useState(false);
  const [historyLanguageCode, setHistoryLanguageCode] = useState<string | null>(null);
  const [historyLanguageName, setHistoryLanguageName] = useState<string | null>(null);
  const [sendReviewLanguageCode, setSendReviewLanguageCode] = useState<string | null>(null);
  const [sendReviewLanguageName, setSendReviewLanguageName] = useState<string | null>(null);
  const [sendReviewFlaggedCount, setSendReviewFlaggedCount] = useState(0);
  const [cancelReviewLanguageCode, setCancelReviewLanguageCode] = useState<string | null>(null);

  const existingCodes = new Set(existingTranslations.map((t) => t.languageCode));

  // Sorted by sectionNumber — array index is the SectionIndex the backend validates against
  // (translated sections are generated from these in the same order).
  const sortedSections = [...sections].sort((a, b) => a.sectionNumber - b.sectionNumber);

  // Language code → state dto lookup
  const stateByCode = new Map(
    (workflowStates ?? []).map((s) => [s.languageCode, s])
  );

  // Build unified language rows
  interface LanguageRow {
    languageCode: string;
    languageName: string;
    state: TranslationWorkflowState;
  }

  const rows: LanguageRow[] = [];
  const seen = new Set<string>();

  // Existing translations first
  for (const t of existingTranslations) {
    seen.add(t.languageCode);
    rows.push({
      languageCode: t.languageCode,
      languageName: t.language,
      state: stateByCode.get(t.languageCode)?.state ?? 'Initial',
    });
  }

  // New employee languages not yet translated
  for (const lang of languagesData?.employeeLanguages ?? []) {
    if (!seen.has(lang.languageCode)) {
      seen.add(lang.languageCode);
      rows.push({
        languageCode: lang.languageCode,
        languageName: lang.language,
        state: 'Initial',
      });
    }
  }

  const fireTranslateMutation = async (languageCode: string, languageName: string) => {
    setPendingByLanguage((prev) => ({ ...prev, [languageCode]: 'translating' }));
    try {
      const result = await generateMutation.mutateAsync({
        toolboxTalkId,
        request: { languages: [languageName] },
      });
      const succeeded = result.languageResults.filter((r) => r.success).length;
      if (succeeded > 0) {
        toast.success(`Translation generated for ${languageName}`);
      } else {
        const err = result.languageResults[0]?.errorMessage ?? 'Translation failed';
        toast.error(err);
      }
    } catch {
      toast.error(`Failed to generate translation for ${languageName}`);
    } finally {
      setPendingByLanguage((prev) => ({ ...prev, [languageCode]: null }));
    }
  };

  const handleTranslateClick = (languageCode: string, languageName: string, state: TranslationWorkflowState) => {
    if (state === 'Accepted') {
      setOverwriteLanguageCode(languageCode);
      setOverwriteLanguageName(languageName);
      setOverwriteWasExternallyReviewed(!!stateByCode.get(languageCode)?.lastExternalReviewedAt);
      return;
    }
    fireTranslateMutation(languageCode, languageName);
  };

  const handleValidateClick = async (languageCode: string, languageName: string) => {
    setPendingByLanguage((prev) => ({ ...prev, [languageCode]: 'validating' }));
    try {
      await validateMutation.mutateAsync({ toolboxTalkId, languageCode });
      toast.success(`Validation started for ${languageName}`);
    } catch {
      toast.error(`Failed to start validation for ${languageName}`);
    } finally {
      setPendingByLanguage((prev) => ({ ...prev, [languageCode]: null }));
    }
  };

  const handleSendForExternalReview = async (email: string, editableSectionIndices: number[]) => {
    if (!sendReviewLanguageCode || !sendReviewLanguageName) return;
    try {
      await initiateExternalReviewMutation.mutateAsync({
        toolboxTalkId,
        languageCode: sendReviewLanguageCode,
        reviewerEmail: email,
        editableSectionIndices,
      });
      toast.success(`Invitation sent to ${email}`);
      setSendReviewLanguageCode(null);
    } catch {
      toast.error(`Failed to send invitation for ${sendReviewLanguageName}`);
    }
  };

  const handleAcceptExternalReview = async (languageCode: string) => {
    setPendingByLanguage((prev) => ({ ...prev, [languageCode]: 'accepting' }));
    try {
      await acceptTranslationMutation.mutateAsync({ toolboxTalkId, languageCode });
      toast.success(`${languageCode.toUpperCase()} translation accepted as final`);
    } catch {
      toast.error('Failed to accept translation');
    } finally {
      setPendingByLanguage((prev) => ({ ...prev, [languageCode]: null }));
    }
  };

  const handleCancelExternalReview = async () => {
    if (!cancelReviewLanguageCode) return;
    try {
      await cancelExternalReviewMutation.mutateAsync({
        toolboxTalkId,
        languageCode: cancelReviewLanguageCode,
      });
      toast.success('Invitation cancelled');
      setCancelReviewLanguageCode(null);
    } catch {
      toast.error('Failed to cancel invitation');
    }
  };

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Languages className="h-5 w-5" />
            Content Translations
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {rows.length === 0 && (
            <p className="text-sm text-muted-foreground">
              No employee languages configured. Add languages via employee profiles.
            </p>
          )}
          {rows.map((row) => {
            const dto = stateByCode.get(row.languageCode);
            const pending = pendingByLanguage[row.languageCode];
            const isTranslating = pending === 'translating';
            const isValidating = pending === 'validating';
            const isAccepting = pending === 'accepting';
            const isBusy = isTranslating || isValidating || isAccepting;

            return (
              <div
                key={row.languageCode}
                className="flex flex-wrap items-center gap-3 rounded-md border p-3"
              >
                {/* Language name */}
                <div className="min-w-[120px]">
                  <div>
                    <span className="font-medium text-sm">{row.languageName}</span>
                    <span className="ml-1.5 text-xs text-muted-foreground">
                      {row.languageCode}
                    </span>
                  </div>
                  {dto?.lastExternalReviewedAt && (
                    <p className="text-xs text-muted-foreground">
                      Externally reviewed by {dto.lastExternalReviewedBy} on{' '}
                      {format(new Date(dto.lastExternalReviewedAt), 'd MMM yyyy')}
                    </p>
                  )}
                </div>

                {/* Workflow state badge */}
                <WorkflowStateBadge state={row.state} />

                {/* Last validation outcome */}
                {dto?.lastValidationOutcome && (
                  <Badge
                    variant="secondary"
                    className={`text-xs ${outcomePillClass[dto.lastValidationOutcome]}`}
                  >
                    {dto.lastValidationOutcome}
                  </Badge>
                )}

                {/* Last event timestamp */}
                {dto?.lastEventAt && (
                  <span className="text-xs text-muted-foreground">
                    {format(new Date(dto.lastEventAt), 'dd MMM yyyy HH:mm')}
                  </span>
                )}

                {/* Spacer */}
                <div className="flex-1" />

                {/* Action buttons */}
                <div className="flex items-center gap-2">
                  {/* Translate */}
                  <TooltipProvider>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span>
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            disabled={isBusy || !isTranslateButtonEnabled(row.state)}
                            onClick={() =>
                              handleTranslateClick(row.languageCode, row.languageName, row.state)
                            }
                          >
                            {isTranslating ? (
                              <span className="flex items-center gap-1">
                                <Languages className="h-3 w-3 animate-pulse" />
                                Translating…
                              </span>
                            ) : (
                              'Translate'
                            )}
                          </Button>
                        </span>
                      </TooltipTrigger>
                      {!isTranslateButtonEnabled(row.state) && (
                        <TooltipContent>
                          <p>Cannot translate in current state</p>
                        </TooltipContent>
                      )}
                    </Tooltip>
                  </TooltipProvider>

                  {/* Validate */}
                  <TooltipProvider>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span>
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            disabled={isBusy || !canValidate(row.state)}
                            onClick={() =>
                              handleValidateClick(row.languageCode, row.languageName)
                            }
                          >
                            {isValidating ? (
                              <span className="flex items-center gap-1">
                                <ClipboardCheck className="h-3 w-3 animate-pulse" />
                                Starting…
                              </span>
                            ) : (
                              'Validate'
                            )}
                          </Button>
                        </span>
                      </TooltipTrigger>
                      {!canValidate(row.state) && (
                        <TooltipContent>
                          <p>Validation only available after translation</p>
                        </TooltipContent>
                      )}
                    </Tooltip>
                  </TooltipProvider>

                  {/* Review */}
                  <TooltipProvider>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span>
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            disabled={isBusy || !canReview(row.state)}
                            onClick={() =>
                              router.push(
                                `/admin/toolbox-talks/talks/${toolboxTalkId}/translations/${row.languageCode}/review`
                              )
                            }
                          >
                            Review
                          </Button>
                        </span>
                      </TooltipTrigger>
                      {!canReview(row.state) && (
                        <TooltipContent>
                          <p>Review only available after validation.</p>
                        </TooltipContent>
                      )}
                    </Tooltip>
                  </TooltipProvider>

                  {/* Send for external review — only when ReviewerAccepted */}
                  {canSendForExternalReview(row.state) && (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={isBusy}
                      onClick={() => {
                        setSendReviewLanguageCode(row.languageCode);
                        setSendReviewLanguageName(row.languageName);
                        setSendReviewFlaggedCount(dto?.flaggedWordCount ?? 0);
                      }}
                    >
                      <Send className="mr-1 h-3 w-3" />
                      Send for review
                    </Button>
                  )}

                  {/* Cancel external review — only when AwaitingThirdParty */}
                  {canCancelExternalReview(row.state) && (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={isBusy}
                      onClick={() => setCancelReviewLanguageCode(row.languageCode)}
                    >
                      <X className="mr-1 h-3 w-3" />
                      Cancel invitation
                    </Button>
                  )}

                  {/* Accept as final — only when ThirdPartyReviewed */}
                  {canAcceptExternalReview(row.state) && (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={isBusy}
                      onClick={() => handleAcceptExternalReview(row.languageCode)}
                    >
                      {isAccepting ? (
                        <CheckCircle2 className="mr-1 h-3 w-3 animate-pulse" />
                      ) : (
                        <CheckCircle2 className="mr-1 h-3 w-3" />
                      )}
                      Accept this language as final
                    </Button>
                  )}

                  {/* View history */}
                  <TooltipProvider>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span>
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            onClick={() => {
                              setHistoryLanguageCode(row.languageCode);
                              setHistoryLanguageName(row.languageName);
                            }}
                          >
                            <History className="h-3 w-3" />
                          </Button>
                        </span>
                      </TooltipTrigger>
                      <TooltipContent>
                        <p>View history</p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>

      {/* Workflow history modal */}
      <WorkflowHistoryModal
        toolboxTalkId={toolboxTalkId}
        languageCode={historyLanguageCode}
        languageName={historyLanguageName}
        open={historyLanguageCode !== null}
        onOpenChange={(open) => {
          if (!open) {
            setHistoryLanguageCode(null);
            setHistoryLanguageName(null);
          }
        }}
      />

      {/* Send for external review dialog */}
      <SendExternalReviewDialog
        open={sendReviewLanguageCode !== null}
        onOpenChange={(open) => {
          if (!open) {
            setSendReviewLanguageCode(null);
            setSendReviewLanguageName(null);
          }
        }}
        onConfirm={handleSendForExternalReview}
        isLoading={initiateExternalReviewMutation.isPending}
        flaggedWordCount={sendReviewFlaggedCount}
        languageName={sendReviewLanguageName ?? ''}
        sections={sortedSections.map((s) => ({ title: s.title }))}
      />

      {/* Cancel external review dialog */}
      <CancelExternalReviewDialog
        open={cancelReviewLanguageCode !== null}
        onOpenChange={(open) => {
          if (!open) setCancelReviewLanguageCode(null);
        }}
        onConfirm={handleCancelExternalReview}
        isLoading={cancelExternalReviewMutation.isPending}
      />

      {/* Overwrite confirmation for Accepted state */}
      <AlertDialog
        open={overwriteLanguageCode !== null}
        onOpenChange={(open) => {
          if (!open) {
            setOverwriteLanguageCode(null);
            setOverwriteLanguageName(null);
            setOverwriteWasExternallyReviewed(false);
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Overwrite accepted translation?</AlertDialogTitle>
            <AlertDialogDescription>
              {overwriteWasExternallyReviewed ? (
                <>
                  <span className="font-medium text-foreground">{overwriteLanguageName}</span>{' '}
                  was reviewed and edited by a trusted external reviewer. Re-translating will
                  discard those edits. If you need the same trust level afterwards, a new
                  external review round would be required. Continue?
                </>
              ) : (
                <>
                  <span className="font-medium text-foreground">{overwriteLanguageName}</span>{' '}
                  has been accepted as final. Reviewer edits and validation results for this
                  language will be replaced with a fresh AI translation.
                </>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => {
                if (overwriteLanguageCode && overwriteLanguageName) {
                  fireTranslateMutation(overwriteLanguageCode, overwriteLanguageName);
                }
                setOverwriteLanguageCode(null);
                setOverwriteLanguageName(null);
                setOverwriteWasExternallyReviewed(false);
              }}
            >
              Overwrite and regenerate
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
