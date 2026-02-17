'use client';

import { useState, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import {
  Info,
  CheckCircle2,
  Loader2,
  BookOpen,
  HelpCircle,
  Video,
  FileText,
  AlertCircle,
  PartyPopper,
  Send,
  Save,
  Target,
  Clock,
  Sparkles,
  Settings,
  RefreshCw,
  Award,
  UserPlus,
} from 'lucide-react';
import { toast } from 'sonner';
import { updateToolboxTalk } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalkWizardData, GeneratedSection, GeneratedQuestion } from '../page';
import type {
  CreateToolboxTalkSectionRequest,
  CreateToolboxTalkQuestionRequest,
  ToolboxTalkStatus,
  QuestionType,
  VideoSource,
} from '@/types/toolbox-talks';

interface PublishStepProps {
  data: ToolboxTalkWizardData;
  updateData: (updates: Partial<ToolboxTalkWizardData>) => void;
  onBack: () => void;
  onComplete: () => void;
}

// Reusable section component for consistent card styling
function SettingsSection({
  icon: Icon,
  title,
  description,
  children,
}: {
  icon: React.ElementType;
  title: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="pb-4">
        <CardTitle className="flex items-center gap-2 text-base">
          <Icon className="h-5 w-5 text-muted-foreground" />
          {title}
        </CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent className="space-y-4">{children}</CardContent>
    </Card>
  );
}

// Transform wizard section to API format
function transformSection(section: GeneratedSection): CreateToolboxTalkSectionRequest {
  return {
    id: section.id,
    sectionNumber: section.sortOrder,
    title: section.title,
    content: section.content,
    requiresAcknowledgment: section.requiresAcknowledgment,
    source: section.source,
  };
}

// Transform wizard question to API format
function transformQuestion(question: GeneratedQuestion): CreateToolboxTalkQuestionRequest {
  return {
    id: question.id,
    questionNumber: question.sortOrder,
    questionText: question.questionText,
    questionType: question.questionType as QuestionType,
    options: question.questionType === 'TrueFalse' ? undefined : question.options,
    correctAnswer: question.options[question.correctAnswerIndex],
    points: question.points,
    source: question.source,
  };
}

// Map wizard video source to API video source
function mapVideoSource(source: string | null): VideoSource {
  switch (source) {
    case 'DirectUrl':
      return 'DirectUrl';
    case 'YouTube':
      return 'YouTube';
    case 'GoogleDrive':
      return 'GoogleDrive';
    case 'Vimeo':
      return 'Vimeo';
    default:
      return 'None';
  }
}

export function PublishStep({
  data,
  updateData,
  onBack,
  onComplete,
}: PublishStepProps) {
  const [isPublishing, setIsPublishing] = useState(false);
  const [isSavingDraft, setIsSavingDraft] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  const hasSections = data.sections.length > 0;
  const hasQuestions = data.questions.length > 0;
  const hasVideo = !!data.videoUrl;
  const hasPdf = !!data.pdfUrl;
  const questionCount = data.questions.length;

  // Calculate quiz stats
  const totalPoints = useMemo(
    () => data.questions.reduce((sum, q) => sum + q.points, 0),
    [data.questions]
  );

  const passingPoints = useMemo(
    () => Math.ceil((data.passThreshold / 100) * totalPoints),
    [data.passThreshold, totalPoints]
  );

  const avgPointsPerQuestion = totalPoints / (questionCount || 1);
  const minCorrectAnswers = Math.ceil(passingPoints / avgPointsPerQuestion);

  // Validation checks
  const validationIssues: string[] = [];
  if (!hasSections) {
    validationIssues.push('At least one training section is required');
  }
  if (data.requiresQuiz && !hasQuestions) {
    validationIssues.push('Quiz is enabled but no questions have been added');
  }
  if (data.requiresQuiz && questionCount < 3) {
    validationIssues.push('At least 3 quiz questions are recommended');
  }
  if (data.passThreshold < 50 || data.passThreshold > 100) {
    validationIssues.push('Pass threshold must be between 50% and 100%');
  }

  const incompleteSections = data.sections.filter(
    (s) => !s.title.trim() || !s.content.trim()
  );
  const incompleteQuestions = data.questions.filter(
    (q) =>
      !q.questionText.trim() ||
      q.options.some((o) => !o.trim()) ||
      q.correctAnswerIndex < 0
  );

  if (incompleteSections.length > 0) {
    validationIssues.push(
      `${incompleteSections.length} section(s) have missing title or content`
    );
  }
  if (incompleteQuestions.length > 0) {
    validationIssues.push(
      `${incompleteQuestions.length} question(s) have incomplete information`
    );
  }

  const canPublish = validationIssues.length === 0;
  const canSaveDraft = hasSections || hasQuestions || data.title;

  const saveToolboxTalk = async (status: ToolboxTalkStatus) => {
    if (!data.id) {
      toast.error('Learning not found', {
        description: 'Please go back to the first step and try again.',
      });
      return;
    }

    const transformedSections = data.sections.map(transformSection);
    const transformedQuestions = data.requiresQuiz ? data.questions.map(transformQuestion) : [];

    try {
      await updateToolboxTalk(data.id, {
        id: data.id,
        title: data.title,
        description: data.description || undefined,
        category: data.category || undefined,
        frequency: data.frequency,
        videoUrl: data.videoUrl || undefined,
        videoSource: mapVideoSource(data.videoSource),
        minimumVideoWatchPercent: data.minimumVideoWatchPercent,
        requiresQuiz: data.requiresQuiz,
        passingScore: data.requiresQuiz ? data.passThreshold : undefined,
        isActive: status === 'Published' ? data.isActive : false,
        sourceLanguageCode: data.sourceLanguageCode,
        shuffleQuestions: data.shuffleQuestions,
        shuffleOptions: data.shuffleOptions,
        useQuestionPool: data.useQuestionPool,
        quizQuestionCount: data.quizQuestionCount,
        generateSlidesFromPdf: data.generateSlidesFromPdf,
        requiresRefresher: data.requiresRefresher,
        refresherIntervalMonths: data.refresherIntervalMonths,
        generateCertificate: data.generateCertificate,
        autoAssignToNewEmployees: data.autoAssignToNewEmployees,
        autoAssignDueDays: data.autoAssignDueDays,
        sections: transformedSections,
        questions: transformedQuestions,
      });

      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error occurred';
      throw new Error(message);
    }
  };

  const handlePublish = async () => {
    setIsPublishing(true);
    try {
      await saveToolboxTalk('Published');
      setShowSuccess(true);
      toast.success('Learning published successfully!', {
        description: 'It is now available for scheduling and assignments.',
      });

      setTimeout(() => {
        onComplete();
      }, 2000);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to publish';
      toast.error('Failed to publish', { description: message });
      setIsPublishing(false);
    }
  };

  const handleSaveDraft = async () => {
    setIsSavingDraft(true);
    try {
      await saveToolboxTalk('Draft');
      toast.success('Draft saved successfully', {
        description: 'You can continue editing later.',
      });
      onComplete();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to save';
      toast.error('Failed to save draft', { description: message });
    } finally {
      setIsSavingDraft(false);
    }
  };

  // Success celebration screen
  if (showSuccess) {
    return (
      <div className="flex flex-col items-center justify-center py-12 space-y-6">
        <div className="relative">
          <div className="absolute inset-0 animate-ping">
            <PartyPopper className="h-16 w-16 text-primary/30" />
          </div>
          <PartyPopper className="h-16 w-16 text-primary" />
        </div>
        <div className="text-center space-y-2">
          <h2 className="text-2xl font-bold">Learning Published!</h2>
          <p className="text-muted-foreground">
            Your training is now ready to be assigned to employees.
          </p>
        </div>
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          <span>Redirecting to talks list...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-lg font-semibold">Publish Settings</h2>
        <p className="text-sm text-muted-foreground">
          Review your Learning and configure final settings before publishing
        </p>
      </div>

      {/* Summary Card */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center gap-2">
            <Sparkles className="h-4 w-4" />
            Summary
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <h4 className="text-lg font-semibold">{data.title || 'Untitled'}</h4>
            <p className="text-sm text-muted-foreground line-clamp-2">
              {data.description || 'No description provided'}
            </p>
            {data.category && (
              <Badge variant="secondary" className="mt-2">
                {data.category}
              </Badge>
            )}
          </div>

          <Separator />

          <div className="space-y-2">
            <h5 className="text-sm font-medium text-muted-foreground">Content Sources</h5>
            <div className="flex flex-wrap gap-3">
              {hasVideo ? (
                <div className="flex items-center gap-2 text-sm">
                  <div className="flex items-center justify-center h-8 w-8 rounded-full bg-blue-100 dark:bg-blue-900/30">
                    <Video className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                  </div>
                  <div>
                    <span className="font-medium">Video</span>
                    <CheckCircle2 className="h-3 w-3 text-green-500 inline ml-1" />
                  </div>
                </div>
              ) : (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <div className="flex items-center justify-center h-8 w-8 rounded-full bg-muted">
                    <Video className="h-4 w-4" />
                  </div>
                  <span>No video</span>
                </div>
              )}

              {hasPdf ? (
                <div className="flex items-center gap-2 text-sm">
                  <div className="flex items-center justify-center h-8 w-8 rounded-full bg-orange-100 dark:bg-orange-900/30">
                    <FileText className="h-4 w-4 text-orange-600 dark:text-orange-400" />
                  </div>
                  <div>
                    <span className="font-medium">PDF</span>
                    <CheckCircle2 className="h-3 w-3 text-green-500 inline ml-1" />
                  </div>
                </div>
              ) : (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <div className="flex items-center justify-center h-8 w-8 rounded-full bg-muted">
                    <FileText className="h-4 w-4" />
                  </div>
                  <span>No PDF</span>
                </div>
              )}
            </div>
          </div>

          <Separator />

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <div className="text-center p-3 rounded-lg bg-muted/50">
              <div className="flex items-center justify-center gap-1 mb-1">
                <BookOpen className="h-4 w-4 text-muted-foreground" />
              </div>
              <p className="text-2xl font-bold">{data.sections.length}</p>
              <p className="text-xs text-muted-foreground">Sections</p>
            </div>

            <div className="text-center p-3 rounded-lg bg-muted/50">
              <div className="flex items-center justify-center gap-1 mb-1">
                <HelpCircle className="h-4 w-4 text-muted-foreground" />
              </div>
              <p className="text-2xl font-bold">{questionCount}</p>
              <p className="text-xs text-muted-foreground">Questions</p>
            </div>

            <div className="text-center p-3 rounded-lg bg-muted/50">
              <div className="flex items-center justify-center gap-1 mb-1">
                <Target className="h-4 w-4 text-muted-foreground" />
              </div>
              <p className="text-2xl font-bold">{totalPoints}</p>
              <p className="text-xs text-muted-foreground">Total Points</p>
            </div>

            <div className="text-center p-3 rounded-lg bg-muted/50">
              <div className="flex items-center justify-center gap-1 mb-1">
                <Clock className="h-4 w-4 text-muted-foreground" />
              </div>
              <p className="text-2xl font-bold capitalize">{data.frequency}</p>
              <p className="text-xs text-muted-foreground">Frequency</p>
            </div>
          </div>

          {hasVideo && data.questions.some((q) => q.source === 'Video') && (
            <Alert className="bg-blue-50 border-blue-200 dark:bg-blue-950/30 dark:border-blue-900">
              <Info className="h-4 w-4 text-blue-600 dark:text-blue-400" />
              <AlertDescription className="text-blue-700 dark:text-blue-300">
                Includes quiz question(s) from video content to ensure complete viewing
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      {/* Settings Sections - Two Column Grid */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Left Column */}
        <div className="space-y-6">
          {/* Basic Settings */}
          <SettingsSection
            icon={Settings}
            title="Basic Settings"
            description="Core configuration for this learning"
          >
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Active</Label>
                <p className="text-sm text-muted-foreground">
                  Inactive talks cannot be assigned to employees
                </p>
              </div>
              <Switch
                checked={data.isActive}
                onCheckedChange={(checked) => updateData({ isActive: checked })}
              />
            </div>
          </SettingsSection>

          {/* Video Settings */}
          {hasVideo && (
            <SettingsSection
              icon={Video}
              title="Video Settings"
              description="Configure video playback requirements"
            >
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <Label>Minimum Watch Percentage</Label>
                  <span className="text-2xl font-bold text-primary">
                    {data.minimumVideoWatchPercent}%
                  </span>
                </div>
                <input
                  type="range"
                  min={50}
                  max={100}
                  step={5}
                  value={data.minimumVideoWatchPercent}
                  onChange={(e) =>
                    updateData({ minimumVideoWatchPercent: parseInt(e.target.value) || 80 })
                  }
                  className="w-full h-2 bg-muted rounded-lg appearance-none cursor-pointer accent-primary"
                />
                <div className="flex justify-between text-xs text-muted-foreground">
                  <span>50%</span>
                  <span>75%</span>
                  <span>100%</span>
                </div>
                <p className="text-sm text-muted-foreground">
                  Employees must watch at least this percentage of the video
                </p>
              </div>
            </SettingsSection>
          )}

          {/* Quiz Settings */}
          <SettingsSection
            icon={HelpCircle}
            title="Quiz Settings"
            description="Configure quiz requirements and behavior"
          >
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Require Quiz</Label>
                <p className="text-sm text-muted-foreground">
                  Employees must pass the quiz to complete this talk
                </p>
              </div>
              <Switch
                checked={data.requiresQuiz}
                onCheckedChange={(checked) => updateData({ requiresQuiz: checked })}
              />
            </div>

            {data.requiresQuiz && (
              <>
                {/* Passing Score */}
                <div className="space-y-4 pt-4 border-t">
                  <div className="flex items-center justify-between">
                    <Label>Passing Score</Label>
                    <span className="text-2xl font-bold text-primary">
                      {data.passThreshold}%
                    </span>
                  </div>
                  <input
                    type="range"
                    min={50}
                    max={100}
                    step={5}
                    value={data.passThreshold}
                    onChange={(e) =>
                      updateData({ passThreshold: parseInt(e.target.value) || 80 })
                    }
                    className="w-full h-2 bg-muted rounded-lg appearance-none cursor-pointer accent-primary"
                  />
                  <div className="flex justify-between text-xs text-muted-foreground">
                    <span>50%</span>
                    <span>75%</span>
                    <span>100%</span>
                  </div>

                  {hasQuestions && (
                    <div className="p-3 rounded-lg bg-muted/50 text-sm">
                      <p className="font-medium">What this means:</p>
                      <p className="text-muted-foreground mt-1">
                        Employees must score at least{' '}
                        <span className="font-medium text-foreground">
                          {passingPoints} of {totalPoints} points
                        </span>
                        {data.questions.every((q) => q.points === 1) && (
                          <>
                            {' '}
                            ({minCorrectAnswers} of {questionCount} questions correct)
                          </>
                        )}
                      </p>
                    </div>
                  )}
                </div>

                {/* Randomization */}
                <div className="space-y-4 pt-4 border-t">
                  <Label className="text-sm font-medium">Randomization</Label>

                  <div className="space-y-3">
                    <div className="flex items-center space-x-2">
                      <Checkbox
                        id="shuffleQuestions"
                        checked={data.shuffleQuestions}
                        onCheckedChange={(checked) =>
                          updateData({ shuffleQuestions: !!checked })
                        }
                      />
                      <Label htmlFor="shuffleQuestions" className="font-normal">
                        Shuffle question order
                      </Label>
                    </div>

                    <div className="flex items-center space-x-2">
                      <Checkbox
                        id="shuffleOptions"
                        checked={data.shuffleOptions}
                        onCheckedChange={(checked) =>
                          updateData({ shuffleOptions: !!checked })
                        }
                      />
                      <Label htmlFor="shuffleOptions" className="font-normal">
                        Shuffle answer options
                      </Label>
                    </div>

                    <div className="flex items-center space-x-2">
                      <Checkbox
                        id="useQuestionPool"
                        checked={data.useQuestionPool}
                        onCheckedChange={(checked) =>
                          updateData({ useQuestionPool: !!checked })
                        }
                      />
                      <Label htmlFor="useQuestionPool" className="font-normal">
                        Use question pool (show different questions each attempt)
                      </Label>
                    </div>

                    {data.useQuestionPool && (
                      <div className="ml-6 space-y-2">
                        <Label htmlFor="quizQuestionCount">Questions per attempt</Label>
                        <Input
                          id="quizQuestionCount"
                          type="number"
                          min={1}
                          max={questionCount}
                          className="w-24"
                          value={data.quizQuestionCount}
                          onChange={(e) =>
                            updateData({
                              quizQuestionCount: parseInt(e.target.value) || 5,
                            })
                          }
                        />
                        <p className="text-sm text-muted-foreground">
                          You have {questionCount} questions. Pool mode requires at least{' '}
                          {data.quizQuestionCount * 2} questions.
                        </p>
                        {questionCount < data.quizQuestionCount * 2 && (
                          <p className="text-sm text-amber-600">
                            Add more questions or reduce questions per attempt
                          </p>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              </>
            )}
          </SettingsSection>
        </div>

        {/* Right Column */}
        <div className="space-y-6">
          {/* Refresher Training */}
          <SettingsSection
            icon={RefreshCw}
            title="Refresher Training"
            description="Schedule automatic refresher training for employees"
          >
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Require Refresher</Label>
                <p className="text-sm text-muted-foreground">
                  Employees will be automatically scheduled for refresher training
                </p>
              </div>
              <Switch
                checked={data.requiresRefresher}
                onCheckedChange={(checked) => updateData({ requiresRefresher: checked })}
              />
            </div>

            {data.requiresRefresher && (
              <div className="space-y-2 pt-4 border-t">
                <Label htmlFor="refresherInterval">Refresher Interval</Label>
                <div className="flex items-center gap-2">
                  <Input
                    id="refresherInterval"
                    type="number"
                    min={1}
                    max={60}
                    className="w-24"
                    value={data.refresherIntervalMonths}
                    onChange={(e) =>
                      updateData({
                        refresherIntervalMonths: parseInt(e.target.value) || 12,
                      })
                    }
                  />
                  <span className="text-sm text-muted-foreground">months after completion</span>
                </div>
              </div>
            )}
          </SettingsSection>

          {/* Certificate */}
          <SettingsSection
            icon={Award}
            title="Certificate"
            description="Award certificates upon completion"
          >
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Generate Certificate</Label>
                <p className="text-sm text-muted-foreground">
                  Employees will receive a PDF certificate when they complete this talk
                </p>
              </div>
              <Switch
                checked={data.generateCertificate}
                onCheckedChange={(checked) => updateData({ generateCertificate: checked })}
              />
            </div>
          </SettingsSection>

          {/* Auto-Assignment */}
          <SettingsSection
            icon={UserPlus}
            title="Auto-Assignment"
            description="Automatically assign to new employees"
          >
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <Label>Auto-Assign to New Employees</Label>
                <p className="text-sm text-muted-foreground">
                  Automatically assign this talk when new employees are created
                </p>
              </div>
              <Switch
                checked={data.autoAssignToNewEmployees}
                onCheckedChange={(checked) =>
                  updateData({ autoAssignToNewEmployees: checked })
                }
              />
            </div>

            {data.autoAssignToNewEmployees && (
              <div className="space-y-2 pt-4 border-t">
                <Label htmlFor="autoAssignDueDays">Due Date</Label>
                <div className="flex items-center gap-2">
                  <Input
                    id="autoAssignDueDays"
                    type="number"
                    min={1}
                    max={365}
                    className="w-24"
                    value={data.autoAssignDueDays}
                    onChange={(e) =>
                      updateData({
                        autoAssignDueDays: parseInt(e.target.value) || 14,
                      })
                    }
                  />
                  <span className="text-sm text-muted-foreground">
                    days after employee start date
                  </span>
                </div>
              </div>
            )}
          </SettingsSection>
        </div>
      </div>

      {/* Validation Issues */}
      {validationIssues.length > 0 && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Cannot Publish</AlertTitle>
          <AlertDescription>
            <ul className="list-disc list-inside mt-2 space-y-1">
              {validationIssues.map((issue, index) => (
                <li key={index}>{issue}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {/* Ready to Publish */}
      {canPublish && (
        <Alert className="border-green-200 bg-green-50 text-green-800 dark:border-green-800 dark:bg-green-950 dark:text-green-200">
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Ready to Publish</AlertTitle>
          <AlertDescription>
            Your Learning is complete and ready to be published. Once
            published, it can be scheduled and assigned to employees.
          </AlertDescription>
        </Alert>
      )}

      <div className="flex justify-between pt-4 border-t">
        <Button
          type="button"
          variant="outline"
          onClick={onBack}
          disabled={isPublishing || isSavingDraft}
        >
          Back
        </Button>
        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={handleSaveDraft}
            disabled={isPublishing || isSavingDraft || !canSaveDraft}
          >
            {isSavingDraft ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Save as Draft
          </Button>
          <Button
            onClick={handlePublish}
            disabled={!canPublish || isPublishing || isSavingDraft}
          >
            {isPublishing ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Send className="mr-2 h-4 w-4" />
            )}
            Publish Learning
          </Button>
        </div>
      </div>
    </div>
  );
}
