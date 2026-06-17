'use client';

import { useState, useCallback, useMemo } from 'react';
import { PencilIcon, XIcon, SaveIcon, FileTextIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
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
import {
  SectionList,
  toSectionDrafts,
  type SectionDraft,
} from '../learning-wizard/components/SectionList';
import { useUpdateToolboxTalk } from '@/lib/api/toolbox-talks';
import { usePermission } from '@/lib/auth/use-auth';
import type { ToolboxTalk } from '@/types/toolbox-talks';
import { toast } from 'sonner';

// ============================================
// Helpers
// ============================================

function sectionsEqual(a: SectionDraft[], b: SectionDraft[]): boolean {
  if (a.length !== b.length) return false;
  return (
    JSON.stringify(
      a.map((s) => ({
        id: s.id,
        title: s.title,
        content: s.content,
        requiresAcknowledgment: s.requiresAcknowledgment,
      }))
    ) ===
    JSON.stringify(
      b.map((s) => ({
        id: s.id,
        title: s.title,
        content: s.content,
        requiresAcknowledgment: s.requiresAcknowledgment,
      }))
    )
  );
}

// ============================================
// Props
// ============================================

interface SectionEditPanelProps {
  talk: ToolboxTalk;
  onRefetch: () => void;
}

// ============================================
// Component
// ============================================

export function SectionEditPanel({ talk, onRefetch }: SectionEditPanelProps) {
  const canManage = usePermission('Learnings.Manage');
  const [isEditMode, setIsEditMode] = useState(false);
  const [editedSections, setEditedSections] = useState<SectionDraft[]>([]);
  const [originalSections, setOriginalSections] = useState<SectionDraft[]>([]);
  const [confirmDiscardOpen, setConfirmDiscardOpen] = useState(false);
  const updateMutation = useUpdateToolboxTalk();

  const isDirty = useMemo(
    () => !sectionsEqual(editedSections, originalSections),
    [editedSections, originalSections]
  );

  const openEditMode = useCallback(() => {
    // Fire-and-forget: ensures latest data is visible before the user starts editing.
    // Does not block opening — the user sees the most-recently-fetched sections instantly.
    onRefetch();
    const drafts = toSectionDrafts(talk.sections);
    setEditedSections(drafts);
    setOriginalSections(drafts);
    setIsEditMode(true);
  }, [talk.sections, onRefetch]);

  const handleSave = useCallback(async () => {
    try {
      await updateMutation.mutateAsync({
        id: talk.id,
        data: {
          id: talk.id,
          code: talk.code,
          title: talk.title,
          description: talk.description ?? undefined,
          category: talk.category ?? undefined,
          frequency: talk.frequency,
          videoUrl: talk.videoUrl ?? undefined,
          videoSource: talk.videoSource,
          attachmentUrl: talk.attachmentUrl ?? undefined,
          minimumVideoWatchPercent: talk.minimumVideoWatchPercent,
          requiresQuiz: talk.requiresQuiz,
          passingScore: talk.passingScore ?? undefined,
          isActive: talk.isActive,
          quizQuestionCount: talk.quizQuestionCount ?? undefined,
          shuffleQuestions: talk.shuffleQuestions,
          shuffleOptions: talk.shuffleOptions,
          useQuestionPool: talk.useQuestionPool,
          sourceLanguageCode: talk.sourceLanguageCode,
          autoAssignToNewEmployees: talk.autoAssignToNewEmployees,
          autoAssignDueDays: talk.autoAssignDueDays,
          generateSlidesFromPdf: talk.generateSlidesFromPdf,
          generateCertificate: talk.generateCertificate,
          requiresRefresher: talk.requiresRefresher,
          refresherIntervalMonths: talk.refresherIntervalMonths,
          sections: editedSections.map((s, i) => ({
            id: s.id,
            sectionNumber: i + 1,
            title: s.title,
            content: s.content,
            requiresAcknowledgment: s.requiresAcknowledgment,
            source: s.source as 'Manual' | 'Video' | 'Pdf' | 'Both' | undefined,
          })),
          // Preserve existing questions unchanged
          questions: talk.questions.map((q, i) => ({
            id: q.id,
            questionNumber: i + 1,
            questionText: q.questionText,
            questionType: q.questionType,
            options: q.options ?? undefined,
            correctAnswer: q.correctAnswer ?? '',
            points: q.points,
            source: q.source,
          })),
        },
      });
      toast.success('Sections saved');
      setIsEditMode(false);
    } catch (error) {
      const msg = error instanceof Error ? error.message : 'Failed to save sections';
      toast.error('Save failed', { description: msg });
    }
  }, [talk, editedSections, updateMutation]);

  const handleCancelClick = useCallback(() => {
    if (isDirty) {
      setConfirmDiscardOpen(true);
    } else {
      setIsEditMode(false);
    }
  }, [isDirty]);

  const handleConfirmDiscard = useCallback(() => {
    setConfirmDiscardOpen(false);
    setIsEditMode(false);
  }, []);

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <FileTextIcon className="h-5 w-5" />
                Sections ({talk.sections.length})
              </CardTitle>
              <CardDescription>Content sections for this learning</CardDescription>
            </div>
            {canManage && !isEditMode && (
              <Button variant="outline" size="sm" onClick={openEditMode}>
                <PencilIcon className="mr-2 h-4 w-4" />
                Edit Sections
              </Button>
            )}
            {isEditMode && (
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleCancelClick}
                  disabled={updateMutation.isPending}
                >
                  <XIcon className="mr-2 h-4 w-4" />
                  Cancel
                </Button>
                <Button
                  size="sm"
                  onClick={handleSave}
                  disabled={updateMutation.isPending || !isDirty}
                >
                  <SaveIcon className="mr-2 h-4 w-4" />
                  {updateMutation.isPending ? 'Saving...' : 'Save Sections'}
                </Button>
              </div>
            )}
          </div>
        </CardHeader>
        <CardContent>
          {isEditMode ? (
            <SectionList
              sections={editedSections}
              onChange={setEditedSections}
              disabled={updateMutation.isPending}
            />
          ) : (
            <Accordion type="multiple" className="w-full">
              {talk.sections.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-4">No sections yet</p>
              ) : (
                talk.sections.map((section) => (
                  <AccordionItem key={section.id} value={section.id}>
                    <AccordionTrigger className="hover:no-underline">
                      <div className="flex items-center gap-3 text-left">
                        <Badge variant="outline" className="shrink-0">
                          {section.sectionNumber}
                        </Badge>
                        <span className="font-medium">{section.title}</span>
                        {section.requiresAcknowledgment && (
                          <Badge variant="secondary" className="text-xs">
                            Acknowledgment Required
                          </Badge>
                        )}
                      </div>
                    </AccordionTrigger>
                    <AccordionContent>
                      <div className="rounded-lg bg-muted/50 p-4 mt-2">
                        <div className="prose prose-sm max-w-none dark:prose-invert">
                          <p className="whitespace-pre-wrap">{section.content}</p>
                        </div>
                      </div>
                    </AccordionContent>
                  </AccordionItem>
                ))
              )}
            </Accordion>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={confirmDiscardOpen} onOpenChange={setConfirmDiscardOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Discard unsaved changes?</AlertDialogTitle>
            <AlertDialogDescription>
              Your section edits have not been saved. This will discard all changes.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmDiscard}>Discard changes</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
