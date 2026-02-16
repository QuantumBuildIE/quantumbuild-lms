'use client';

import { useState, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SortableList, DragHandle, useSortableItem } from '@/components/ui/sortable';
import {
  BookOpen,
  HelpCircle,
  Plus,
  Trash2,
  Edit2,
  Check,
  X,
  Video,
  FileText,
  PenLine,
  AlertCircle,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ToolboxTalkWizardData, GeneratedSection, GeneratedQuestion } from '../page';

interface ReviewStepProps {
  data: ToolboxTalkWizardData;
  updateData: (updates: Partial<ToolboxTalkWizardData>) => void;
  onNext: () => void;
  onBack: () => void;
}

// Source badge component
function SourceBadge({ source }: { source: 'Video' | 'Pdf' | 'Both' | 'Manual' }) {
  const variants: Record<string, { icon: React.ReactNode; className: string }> = {
    Video: {
      icon: <Video className="h-3 w-3" />,
      className: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
    },
    Pdf: {
      icon: <FileText className="h-3 w-3" />,
      className: 'bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-400',
    },
    Both: {
      icon: <span className="text-[10px]">V+P</span>,
      className: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400',
    },
    Manual: {
      icon: <PenLine className="h-3 w-3" />,
      className: 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-400',
    },
  };

  const { icon, className } = variants[source];

  return (
    <Badge variant="outline" className={cn('gap-1 text-xs font-normal', className)}>
      {icon}
      {source}
    </Badge>
  );
}

// Sortable Section Card
interface SectionCardProps {
  section: GeneratedSection;
  onEdit: (section: GeneratedSection) => void;
  onDelete: (id: string) => void;
}

function SectionCard({ section, onEdit, onDelete }: SectionCardProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editData, setEditData] = useState(section);

  const { attributes, listeners, setNodeRef, style, isDragging } = useSortableItem({
    id: section.id,
  });

  const handleSave = () => {
    onEdit(editData);
    setIsEditing(false);
  };

  const handleCancel = () => {
    setEditData(section);
    setIsEditing(false);
  };

  if (isEditing) {
    return (
      <div
        ref={setNodeRef}
        style={style}
        className={cn(
          'rounded-lg border bg-card p-4 space-y-4',
          isDragging && 'opacity-50 shadow-lg'
        )}
      >
        <div className="flex items-center gap-3">
          <DragHandle {...listeners} {...attributes} disabled />
          <div className="flex-1 space-y-3">
            <div>
              <Label htmlFor={`section-title-${section.id}`}>Title</Label>
              <Input
                id={`section-title-${section.id}`}
                value={editData.title}
                onChange={(e) => setEditData({ ...editData, title: e.target.value })}
                placeholder="Section title"
              />
            </div>
            <div>
              <Label htmlFor={`section-content-${section.id}`}>Content</Label>
              <Textarea
                id={`section-content-${section.id}`}
                value={editData.content}
                onChange={(e) => setEditData({ ...editData, content: e.target.value })}
                placeholder="Section content"
                rows={4}
              />
            </div>
            <div className="flex items-center space-x-2">
              <Checkbox
                id={`section-ack-${section.id}`}
                checked={editData.requiresAcknowledgment}
                onCheckedChange={(checked) =>
                  setEditData({ ...editData, requiresAcknowledgment: checked === true })
                }
              />
              <Label htmlFor={`section-ack-${section.id}`} className="text-sm font-normal">
                Requires acknowledgment from employee
              </Label>
            </div>
          </div>
        </div>
        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={handleCancel}>
            <X className="h-4 w-4 mr-1" /> Cancel
          </Button>
          <Button size="sm" onClick={handleSave}>
            <Check className="h-4 w-4 mr-1" /> Save
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'rounded-lg border bg-card p-4 transition-colors',
        isDragging && 'opacity-50 shadow-lg'
      )}
    >
      <div className="flex items-start gap-3">
        <DragHandle {...listeners} {...attributes} className="mt-1" />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span className="text-sm font-medium text-muted-foreground">
              {section.sortOrder}.
            </span>
            <h4 className="font-medium truncate">{section.title}</h4>
            <SourceBadge source={section.source} />
            {section.requiresAcknowledgment && (
              <Badge variant="secondary" className="text-xs">
                Ack Required
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground line-clamp-2">{section.content}</p>
        </div>
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8"
            onClick={() => setIsEditing(true)}
          >
            <Edit2 className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-destructive hover:text-destructive"
            onClick={() => onDelete(section.id)}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}

// Sortable Question Card
interface QuestionCardProps {
  question: GeneratedQuestion;
  onEdit: (question: GeneratedQuestion) => void;
  onDelete: (id: string) => void;
}

function QuestionCard({ question, onEdit, onDelete }: QuestionCardProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editData, setEditData] = useState(question);

  const { attributes, listeners, setNodeRef, style, isDragging } = useSortableItem({
    id: question.id,
  });

  const handleSave = () => {
    onEdit(editData);
    setIsEditing(false);
  };

  const handleCancel = () => {
    setEditData(question);
    setIsEditing(false);
  };

  const handleOptionChange = (index: number, value: string) => {
    const newOptions = [...editData.options];
    newOptions[index] = value;
    setEditData({ ...editData, options: newOptions });
  };

  if (isEditing) {
    return (
      <div
        ref={setNodeRef}
        style={style}
        className={cn(
          'rounded-lg border bg-card p-4 space-y-4',
          isDragging && 'opacity-50 shadow-lg'
        )}
      >
        <div className="flex items-start gap-3">
          <DragHandle {...listeners} {...attributes} disabled className="mt-1" />
          <div className="flex-1 space-y-4">
            <div>
              <Label htmlFor={`question-text-${question.id}`}>Question</Label>
              <Textarea
                id={`question-text-${question.id}`}
                value={editData.questionText}
                onChange={(e) => setEditData({ ...editData, questionText: e.target.value })}
                placeholder="Question text"
                rows={2}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <Label>Question Type</Label>
                <Select
                  value={editData.questionType}
                  onValueChange={(value: 'MultipleChoice' | 'TrueFalse') =>
                    setEditData({
                      ...editData,
                      questionType: value,
                      options:
                        value === 'TrueFalse' ? ['True', 'False'] : editData.options,
                      correctAnswerIndex:
                        value === 'TrueFalse'
                          ? editData.options[editData.correctAnswerIndex] === 'True'
                            ? 0
                            : 1
                          : editData.correctAnswerIndex,
                    })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="MultipleChoice">Multiple Choice</SelectItem>
                    <SelectItem value="TrueFalse">True/False</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor={`question-points-${question.id}`}>Points</Label>
                <Input
                  id={`question-points-${question.id}`}
                  type="number"
                  min={1}
                  max={10}
                  value={editData.points}
                  onChange={(e) =>
                    setEditData({ ...editData, points: parseInt(e.target.value) || 1 })
                  }
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label>Answer Options</Label>
              {editData.options.map((option, index) => (
                <div key={index} className="flex items-center gap-2">
                  <input
                    type="radio"
                    name={`correct-answer-${question.id}`}
                    checked={editData.correctAnswerIndex === index}
                    onChange={() => setEditData({ ...editData, correctAnswerIndex: index })}
                    className="h-4 w-4"
                  />
                  <Input
                    value={option}
                    onChange={(e) => handleOptionChange(index, e.target.value)}
                    placeholder={`Option ${index + 1}`}
                    disabled={editData.questionType === 'TrueFalse'}
                    className="flex-1"
                  />
                  {editData.questionType === 'MultipleChoice' && editData.options.length > 2 && (
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8"
                      onClick={() => {
                        const newOptions = editData.options.filter((_, i) => i !== index);
                        const newCorrectIndex =
                          editData.correctAnswerIndex >= index && editData.correctAnswerIndex > 0
                            ? editData.correctAnswerIndex - 1
                            : editData.correctAnswerIndex;
                        setEditData({
                          ...editData,
                          options: newOptions,
                          correctAnswerIndex: Math.min(newCorrectIndex, newOptions.length - 1),
                        });
                      }}
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              ))}
              {editData.questionType === 'MultipleChoice' && editData.options.length < 6 && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setEditData({
                      ...editData,
                      options: [...editData.options, ''],
                    })
                  }
                >
                  <Plus className="h-4 w-4 mr-1" /> Add Option
                </Button>
              )}
              <p className="text-xs text-muted-foreground">
                Select the radio button next to the correct answer
              </p>
            </div>
          </div>
        </div>
        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={handleCancel}>
            <X className="h-4 w-4 mr-1" /> Cancel
          </Button>
          <Button size="sm" onClick={handleSave}>
            <Check className="h-4 w-4 mr-1" /> Save
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'rounded-lg border bg-card p-4 transition-colors',
        isDragging && 'opacity-50 shadow-lg'
      )}
    >
      <div className="flex items-start gap-3">
        <DragHandle {...listeners} {...attributes} className="mt-1" />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-2">
            <span className="text-sm font-medium text-muted-foreground">
              Q{question.sortOrder}.
            </span>
            <Badge variant="outline" className="text-xs">
              {question.questionType === 'TrueFalse' ? 'True/False' : 'Multiple Choice'}
            </Badge>
            <SourceBadge source={question.source} />
            <Badge variant="secondary" className="text-xs">
              {question.points} {question.points === 1 ? 'point' : 'points'}
            </Badge>
          </div>
          <p className="font-medium mb-2">{question.questionText}</p>
          <div className="grid gap-1 sm:grid-cols-2">
            {question.options.map((option, index) => (
              <div
                key={index}
                className={cn(
                  'text-sm px-2 py-1 rounded',
                  index === question.correctAnswerIndex
                    ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
                    : 'text-muted-foreground'
                )}
              >
                {String.fromCharCode(65 + index)}. {option}
                {index === question.correctAnswerIndex && (
                  <Check className="h-3 w-3 inline ml-1" />
                )}
              </div>
            ))}
          </div>
        </div>
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8"
            onClick={() => setIsEditing(true)}
          >
            <Edit2 className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-destructive hover:text-destructive"
            onClick={() => onDelete(question.id)}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}

export function ReviewStep({ data, updateData, onNext, onBack }: ReviewStepProps) {
  const [activeTab, setActiveTab] = useState('sections');
  const [deleteDialog, setDeleteDialog] = useState<{
    open: boolean;
    type: 'section' | 'question';
    id: string;
    title: string;
  }>({ open: false, type: 'section', id: '', title: '' });

  // Section handlers
  const handleSectionsReorder = useCallback(
    (reorderedSections: GeneratedSection[]) => {
      const updated = reorderedSections.map((section, index) => ({
        ...section,
        sortOrder: index + 1,
      }));
      updateData({ sections: updated });
    },
    [updateData]
  );

  const handleSectionEdit = useCallback(
    (editedSection: GeneratedSection) => {
      updateData({
        sections: data.sections.map((s) =>
          s.id === editedSection.id ? editedSection : s
        ),
      });
    },
    [data.sections, updateData]
  );

  const handleSectionDelete = useCallback((id: string) => {
    const section = data.sections.find((s) => s.id === id);
    if (section) {
      setDeleteDialog({
        open: true,
        type: 'section',
        id,
        title: section.title,
      });
    }
  }, [data.sections]);

  const handleAddSection = useCallback(() => {
    const newSection: GeneratedSection = {
      id: crypto.randomUUID(),
      sortOrder: data.sections.length + 1,
      title: '',
      content: '',
      source: 'Manual',
      requiresAcknowledgment: false,
    };
    updateData({ sections: [...data.sections, newSection] });
  }, [data.sections, updateData]);

  // Question handlers
  const handleQuestionsReorder = useCallback(
    (reorderedQuestions: GeneratedQuestion[]) => {
      const updated = reorderedQuestions.map((question, index) => ({
        ...question,
        sortOrder: index + 1,
      }));
      updateData({ questions: updated });
    },
    [updateData]
  );

  const handleQuestionEdit = useCallback(
    (editedQuestion: GeneratedQuestion) => {
      updateData({
        questions: data.questions.map((q) =>
          q.id === editedQuestion.id ? editedQuestion : q
        ),
      });
    },
    [data.questions, updateData]
  );

  const handleQuestionDelete = useCallback((id: string) => {
    const question = data.questions.find((q) => q.id === id);
    if (question) {
      setDeleteDialog({
        open: true,
        type: 'question',
        id,
        title: question.questionText,
      });
    }
  }, [data.questions]);

  const handleAddQuestion = useCallback(() => {
    const newQuestion: GeneratedQuestion = {
      id: crypto.randomUUID(),
      sortOrder: data.questions.length + 1,
      questionText: '',
      questionType: 'MultipleChoice',
      options: ['', '', '', ''],
      correctAnswerIndex: 0,
      source: 'Manual',
      points: 1,
    };
    updateData({ questions: [...data.questions, newQuestion] });
  }, [data.questions, updateData]);

  // Confirm deletion
  const confirmDelete = useCallback(() => {
    if (deleteDialog.type === 'section') {
      const updatedSections = data.sections
        .filter((s) => s.id !== deleteDialog.id)
        .map((s, i) => ({ ...s, sortOrder: i + 1 }));
      updateData({ sections: updatedSections });
    } else {
      const updatedQuestions = data.questions
        .filter((q) => q.id !== deleteDialog.id)
        .map((q, i) => ({ ...q, sortOrder: i + 1 }));
      updateData({ questions: updatedQuestions });
    }
    setDeleteDialog({ open: false, type: 'section', id: '', title: '' });
  }, [deleteDialog, data.sections, data.questions, updateData]);

  const hasSections = data.sections.length > 0;
  const hasQuestions = data.questions.length > 0;

  // Validation warnings
  const sectionWarnings = data.sections.filter(
    (s) => !s.title.trim() || !s.content.trim()
  );
  const questionWarnings = data.questions.filter(
    (q) =>
      !q.questionText.trim() ||
      q.options.some((o) => !o.trim()) ||
      q.correctAnswerIndex < 0 ||
      q.correctAnswerIndex >= q.options.length
  );

  const canProceed =
    hasSections &&
    sectionWarnings.length === 0 &&
    (data.requiresQuiz ? hasQuestions && questionWarnings.length === 0 : true);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-lg font-semibold">Review Content</h2>
        <p className="text-sm text-muted-foreground">
          Review, edit, reorder, or add new sections and quiz questions
        </p>
      </div>

      {/* Validation warnings */}
      {(sectionWarnings.length > 0 || questionWarnings.length > 0) && (
        <div className="rounded-lg border border-yellow-200 bg-yellow-50 dark:border-yellow-900/50 dark:bg-yellow-900/20 p-4">
          <div className="flex items-start gap-3">
            <AlertCircle className="h-5 w-5 text-yellow-600 dark:text-yellow-500 mt-0.5" />
            <div className="text-sm">
              <p className="font-medium text-yellow-800 dark:text-yellow-400">
                Please complete the following items:
              </p>
              <ul className="mt-1 text-yellow-700 dark:text-yellow-500 list-disc list-inside">
                {sectionWarnings.length > 0 && (
                  <li>
                    {sectionWarnings.length} section(s) have missing title or content
                  </li>
                )}
                {questionWarnings.length > 0 && (
                  <li>
                    {questionWarnings.length} question(s) have incomplete information
                  </li>
                )}
              </ul>
            </div>
          </div>
        </div>
      )}

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="sections" className="gap-2">
            <BookOpen className="h-4 w-4" />
            Sections ({data.sections.length})
          </TabsTrigger>
          <TabsTrigger value="questions" className="gap-2">
            <HelpCircle className="h-4 w-4" />
            Quiz Questions ({data.questions.length})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="sections" className="mt-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
              <CardTitle className="text-base font-medium flex items-center gap-2">
                <BookOpen className="h-4 w-4" />
                Training Sections
              </CardTitle>
              <Button size="sm" variant="outline" onClick={handleAddSection}>
                <Plus className="h-4 w-4 mr-1" />
                Add Section
              </Button>
            </CardHeader>
            <CardContent>
              {hasSections ? (
                <SortableList
                  items={data.sections}
                  keyExtractor={(s) => s.id}
                  onReorder={handleSectionsReorder}
                >
                  <div className="space-y-3">
                    {data.sections.map((section) => (
                      <SectionCard
                        key={section.id}
                        section={section}
                        onEdit={handleSectionEdit}
                        onDelete={handleSectionDelete}
                      />
                    ))}
                  </div>
                </SortableList>
              ) : (
                <div className="text-center py-12 text-muted-foreground">
                  <BookOpen className="h-12 w-12 mx-auto mb-3 opacity-50" />
                  <p className="mb-4">No sections yet</p>
                  <Button variant="outline" onClick={handleAddSection}>
                    <Plus className="h-4 w-4 mr-1" />
                    Add First Section
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="questions" className="mt-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
              <CardTitle className="text-base font-medium flex items-center gap-2">
                <HelpCircle className="h-4 w-4" />
                Quiz Questions
              </CardTitle>
              <Button size="sm" variant="outline" onClick={handleAddQuestion}>
                <Plus className="h-4 w-4 mr-1" />
                Add Question
              </Button>
            </CardHeader>
            <CardContent>
              {hasQuestions ? (
                <SortableList
                  items={data.questions}
                  keyExtractor={(q) => q.id}
                  onReorder={handleQuestionsReorder}
                >
                  <div className="space-y-3">
                    {data.questions.map((question) => (
                      <QuestionCard
                        key={question.id}
                        question={question}
                        onEdit={handleQuestionEdit}
                        onDelete={handleQuestionDelete}
                      />
                    ))}
                  </div>
                </SortableList>
              ) : (
                <div className="text-center py-12 text-muted-foreground">
                  <HelpCircle className="h-12 w-12 mx-auto mb-3 opacity-50" />
                  <p className="mb-4">
                    {data.requiresQuiz
                      ? 'No quiz questions yet. Add questions to test employee understanding.'
                      : 'Quiz is not required for this training. You can still add questions if needed.'}
                  </p>
                  <Button variant="outline" onClick={handleAddQuestion}>
                    <Plus className="h-4 w-4 mr-1" />
                    Add First Question
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Summary */}
      <div className="rounded-lg bg-muted/50 p-4">
        <h4 className="font-medium mb-2">Content Summary</h4>
        <div className="grid gap-4 sm:grid-cols-3 text-sm">
          <div>
            <span className="text-muted-foreground">Total Sections:</span>{' '}
            <span className="font-medium">{data.sections.length}</span>
          </div>
          <div>
            <span className="text-muted-foreground">Total Questions:</span>{' '}
            <span className="font-medium">{data.questions.length}</span>
          </div>
          <div>
            <span className="text-muted-foreground">Total Points:</span>{' '}
            <span className="font-medium">
              {data.questions.reduce((sum, q) => sum + q.points, 0)}
            </span>
          </div>
        </div>
        {hasSections && (
          <div className="mt-3 text-sm">
            <span className="text-muted-foreground">Sources:</span>{' '}
            <div className="inline-flex flex-wrap gap-2 ml-2">
              {['Video', 'Pdf', 'Both', 'Manual'].map((source) => {
                const count =
                  data.sections.filter((s) => s.source === source).length +
                  data.questions.filter((q) => q.source === source).length;
                if (count === 0) return null;
                return (
                  <span key={source} className="text-xs">
                    <SourceBadge source={source as 'Video' | 'Pdf' | 'Both' | 'Manual'} />
                    <span className="ml-1">({count})</span>
                  </span>
                );
              })}
            </div>
          </div>
        )}
      </div>

      <div className="flex justify-between pt-4 border-t">
        <Button type="button" variant="outline" onClick={onBack}>
          Back
        </Button>
        <Button onClick={onNext} disabled={!canProceed}>
          Next: Publish Settings
        </Button>
      </div>

      {/* Delete Confirmation Dialog */}
      <AlertDialog
        open={deleteDialog.open}
        onOpenChange={(open) =>
          !open && setDeleteDialog({ open: false, type: 'section', id: '', title: '' })
        }
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              Delete {deleteDialog.type === 'section' ? 'Section' : 'Question'}?
            </AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete{' '}
              {deleteDialog.type === 'section' ? 'this section' : 'this question'}?
              <span className="block mt-2 font-medium text-foreground">
                &quot;{deleteDialog.title.substring(0, 100)}
                {deleteDialog.title.length > 100 ? '...' : ''}&quot;
              </span>
              This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={confirmDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
