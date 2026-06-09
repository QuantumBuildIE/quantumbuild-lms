'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import { Languages, History, ClipboardCheck } from 'lucide-react';
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
import {
  useAvailableLanguages,
  useGenerateContentTranslations,
  useWorkflowStates,
  useValidateTranslation,
} from '@/lib/api/toolbox-talks';
import type { ToolboxTalkTranslation } from '@/types/toolbox-talks';
import type { TranslationWorkflowState, ValidationOutcome } from '@/types/workflows';

interface TranslationWorkflowPanelProps {
  toolboxTalkId: string;
  existingTranslations: ToolboxTalkTranslation[];
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
  return state === 'Validated' || state === 'ReviewerAccepted' || state === 'ThirdPartyReviewed';
}

export function TranslationWorkflowPanel({
  toolboxTalkId,
  existingTranslations,
}: TranslationWorkflowPanelProps) {
  const router = useRouter();
  const { data: languagesData } = useAvailableLanguages();
  const { data: workflowStates } = useWorkflowStates(toolboxTalkId);
  const generateMutation = useGenerateContentTranslations();
  const validateMutation = useValidateTranslation();

  const [pendingByLanguage, setPendingByLanguage] = useState<
    Record<string, 'translating' | 'validating' | null>
  >({});
  const [overwriteLanguageCode, setOverwriteLanguageCode] = useState<string | null>(null);
  const [overwriteLanguageName, setOverwriteLanguageName] = useState<string | null>(null);

  const existingCodes = new Set(existingTranslations.map((t) => t.languageCode));

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
            const isBusy = isTranslating || isValidating;

            return (
              <div
                key={row.languageCode}
                className="flex flex-wrap items-center gap-3 rounded-md border p-3"
              >
                {/* Language name */}
                <div className="min-w-[120px]">
                  <span className="font-medium text-sm">{row.languageName}</span>
                  <span className="ml-1.5 text-xs text-muted-foreground">
                    {row.languageCode}
                  </span>
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

                  {/* View history — stubbed for 3c.5 */}
                  <TooltipProvider>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span>
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            disabled
                          >
                            <History className="h-3 w-3" />
                          </Button>
                        </span>
                      </TooltipTrigger>
                      <TooltipContent>
                        <p>Available shortly</p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>

      {/* Overwrite confirmation for Accepted state */}
      <AlertDialog
        open={overwriteLanguageCode !== null}
        onOpenChange={(open) => {
          if (!open) {
            setOverwriteLanguageCode(null);
            setOverwriteLanguageName(null);
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Overwrite accepted translation?</AlertDialogTitle>
            <AlertDialogDescription>
              <span className="font-medium text-foreground">{overwriteLanguageName}</span>{' '}
              has been accepted as final. Reviewer edits and validation results for this
              language will be replaced with a fresh AI translation.
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
