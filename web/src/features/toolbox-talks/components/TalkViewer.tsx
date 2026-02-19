'use client';

import * as React from 'react';
import Link from 'next/link';
import { format } from 'date-fns';
import {
  ChevronLeft,
  Clock,
  Calendar,
  AlertTriangle,
  CheckCircle2,
  Video,
  FileText,
  FileDown,
  HelpCircle,
  PenLine,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import {
  useMyToolboxTalk,
  useStartToolboxTalk,
  useMarkSectionRead,
  useUpdateVideoProgress,
  useResetVideoProgress,
  useSubmitQuizAnswers,
  useCompleteToolboxTalk,
} from '@/lib/api/toolbox-talks/use-my-toolbox-talks';
import { useGeolocation } from '@/hooks/use-geolocation';
import type { MyToolboxTalk, QuizResult } from '@/types/toolbox-talks';
import { toast } from 'sonner';
import { SCHEDULED_TALK_STATUS_VARIANTS as statusVariants } from '@/lib/constants/status';

import { SectionContent } from './SectionContent';
import { VideoPlayer } from './VideoPlayer';
import { SlideshowSection } from './SlideshowSection';
import { QuizSection } from './QuizSection';
import { SignatureCapture } from './SignatureCapture';
import { CompletionSuccess } from './CompletionSuccess';

// Steps in the talk completion flow
type ViewerStep = 'video' | 'sections' | 'quiz' | 'signature' | 'complete';

interface StepIndicatorProps {
  steps: { key: ViewerStep; label: string; icon: React.ElementType; available: boolean }[];
  currentStep: ViewerStep;
  onStepClick: (step: ViewerStep) => void;
}

function StepIndicator({ steps, currentStep, onStepClick }: StepIndicatorProps) {
  const activeSteps = steps.filter((s) => s.available);
  const currentIndex = activeSteps.findIndex((s) => s.key === currentStep);

  return (
    <div className="hidden md:flex items-center gap-2">
      {activeSteps.map((step, index) => {
        const Icon = step.icon;
        const isActive = step.key === currentStep;
        const isCompleted = index < currentIndex;
        const isClickable = index <= currentIndex;

        return (
          <React.Fragment key={step.key}>
            <button
              onClick={() => isClickable && onStepClick(step.key)}
              disabled={!isClickable}
              className={cn(
                'flex items-center gap-2 px-3 py-1.5 rounded-full text-sm transition-colors',
                isActive && 'bg-primary text-primary-foreground',
                isCompleted && 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
                !isActive && !isCompleted && 'bg-muted text-muted-foreground',
                isClickable && !isActive && 'hover:bg-muted/80 cursor-pointer',
                !isClickable && 'opacity-50 cursor-not-allowed'
              )}
            >
              {isCompleted ? (
                <CheckCircle2 className="h-4 w-4" />
              ) : (
                <Icon className="h-4 w-4" />
              )}
              <span className="hidden lg:inline">{step.label}</span>
            </button>
            {index < activeSteps.length - 1 && (
              <div
                className={cn(
                  'w-8 h-0.5',
                  index < currentIndex ? 'bg-green-500' : 'bg-muted'
                )}
              />
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
}

// Mobile step selector
function MobileStepSelector({
  steps,
  currentStep,
  onStepClick,
}: StepIndicatorProps) {
  const activeSteps = steps.filter((s) => s.available);
  const currentIndex = activeSteps.findIndex((s) => s.key === currentStep);
  const current = activeSteps[currentIndex];

  return (
    <div className="md:hidden">
      <div className="flex items-center justify-between mb-2">
        <span className="text-sm text-muted-foreground">
          Step {currentIndex + 1} of {activeSteps.length}
        </span>
        <Progress value={((currentIndex + 1) / activeSteps.length) * 100} className="w-24 h-2" />
      </div>
      {current && (
        <div className="flex items-center gap-2 text-sm font-medium">
          <current.icon className="h-4 w-4" />
          {current.label}
        </div>
      )}
    </div>
  );
}

interface SectionNavigatorProps {
  sections: MyToolboxTalk['sections'];
  currentIndex: number;
  onSectionClick: (index: number) => void;
}

function SectionNavigator({ sections, currentIndex, onSectionClick }: SectionNavigatorProps) {
  return (
    <div className="hidden lg:block w-64 shrink-0">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">Sections</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <nav className="space-y-1 p-2">
            {sections.map((section, index) => (
              <button
                key={section.sectionId}
                onClick={() => onSectionClick(index)}
                className={cn(
                  'w-full flex items-center gap-2 px-3 py-2 rounded-md text-sm text-left transition-colors',
                  index === currentIndex && 'bg-primary text-primary-foreground',
                  index !== currentIndex && 'hover:bg-muted',
                  section.isRead && index !== currentIndex && 'text-muted-foreground'
                )}
              >
                <span className="flex-shrink-0 w-5 h-5 rounded-full border flex items-center justify-center text-xs">
                  {section.isRead ? (
                    <CheckCircle2 className="h-3 w-3 text-green-600" />
                  ) : (
                    section.sectionNumber
                  )}
                </span>
                <span className="truncate">{section.title}</span>
              </button>
            ))}
          </nav>
        </CardContent>
      </Card>
    </div>
  );
}

interface TalkViewerProps {
  scheduledTalkId: string;
}

export function TalkViewer({ scheduledTalkId }: TalkViewerProps) {
  const { data: talk, isLoading, error } = useMyToolboxTalk(scheduledTalkId);

  // Mutations
  const startTalk = useStartToolboxTalk();
  const markSectionRead = useMarkSectionRead();
  const updateVideoProgress = useUpdateVideoProgress();
  const resetVideoProgress = useResetVideoProgress();
  const submitQuizAnswers = useSubmitQuizAnswers();
  const completeTalk = useCompleteToolboxTalk();

  // Geolocation
  const { getLocation } = useGeolocation();

  // Local state
  const [currentStep, setCurrentStep] = React.useState<ViewerStep>('sections');
  const [currentSectionIndex, setCurrentSectionIndex] = React.useState(0);
  const [initialStepSet, setInitialStepSet] = React.useState(false);
  const [hasRecordedStart, setHasRecordedStart] = React.useState(false);

  // Determine available steps based on talk configuration
  const getAvailableSteps = React.useCallback((talk: MyToolboxTalk | undefined) => {
    if (!talk) return [];

    const steps: { key: ViewerStep; label: string; icon: React.ElementType; available: boolean }[] = [];

    // Video step (if has video)
    if (talk.videoUrl && talk.videoSource !== 'None') {
      steps.push({ key: 'video', label: 'Video', icon: Video, available: true });
    }

    // Sections step (always)
    steps.push({ key: 'sections', label: 'Sections', icon: FileText, available: true });

    // Quiz step (if requires quiz)
    if (talk.requiresQuiz && talk.questions.length > 0) {
      steps.push({ key: 'quiz', label: 'Quiz', icon: HelpCircle, available: true });
    }

    // Signature step (always)
    steps.push({ key: 'signature', label: 'Sign', icon: PenLine, available: true });

    // Complete step (always)
    steps.push({ key: 'complete', label: 'Complete', icon: CheckCircle2, available: true });

    return steps;
  }, []);

  // Set initial step based on progress (only on first load, not on data updates)
  React.useEffect(() => {
    if (!talk || initialStepSet) return;

    // Mark that we've set the initial step - this prevents auto-redirecting
    // when video progress updates cause a data refetch
    setInitialStepSet(true);

    // If already completed, show completion
    if (talk.status === 'Completed' && talk.completedAt) {
      setCurrentStep('complete');
      return;
    }

    // Determine starting step based on progress
    const hasVideo = talk.videoUrl && talk.videoSource !== 'None';
    const videoComplete = !hasVideo || (talk.videoWatchPercent ?? 0) >= talk.minimumVideoWatchPercent;
    const sectionsComplete = talk.completedSections === talk.totalSections;
    const quizComplete = !talk.requiresQuiz || talk.lastQuizPassed === true;

    if (!videoComplete && hasVideo) {
      setCurrentStep('video');
    } else if (!sectionsComplete) {
      setCurrentStep('sections');
      // Find first unread section
      const firstUnread = talk.sections.findIndex((s) => !s.isRead);
      setCurrentSectionIndex(firstUnread >= 0 ? firstUnread : 0);
    } else if (!quizComplete && talk.requiresQuiz) {
      setCurrentStep('quiz');
    } else {
      setCurrentStep('signature');
    }
  }, [talk, initialStepSet]);

  // Record start with geolocation when talk is first viewed
  React.useEffect(() => {
    if (!talk || hasRecordedStart) return;
    if (talk.status === 'Completed' || talk.status === 'Cancelled') return;

    setHasRecordedStart(true);

    const recordStart = async () => {
      try {
        const location = await getLocation();
        await startTalk.mutateAsync({
          scheduledTalkId,
          data: location
            ? {
                latitude: location.latitude,
                longitude: location.longitude,
                accuracyMeters: location.accuracyMeters,
              }
            : undefined,
        });
      } catch {
        // Silently fail - start recording is not critical
      }
    };

    recordStart();
  }, [talk, hasRecordedStart, scheduledTalkId, getLocation, startTalk]);

  // Handlers
  const handleMarkSectionRead = async (sectionId: string, timeSpentSeconds?: number) => {
    try {
      await markSectionRead.mutateAsync({
        scheduledTalkId,
        sectionId,
        data: timeSpentSeconds ? { timeSpentSeconds } : undefined,
      });
    } catch {
      toast.error('Failed to mark section as read');
      throw new Error('Failed to mark section as read');
    }
  };

  const handleVideoProgress = async (percent: number) => {
    try {
      await updateVideoProgress.mutateAsync({
        scheduledTalkId,
        data: { watchPercent: percent },
      });
    } catch {
      // Silently fail - video progress is not critical
    }
  };

  const handleQuizSubmit = async (answers: Record<string, string>): Promise<QuizResult> => {
    try {
      const result = await submitQuizAnswers.mutateAsync({
        scheduledTalkId,
        data: { answers },
      });
      return result;
    } catch {
      toast.error('Failed to submit quiz');
      throw new Error('Failed to submit quiz');
    }
  };

  const handleComplete = async (signatureData: string, signedByName: string) => {
    try {
      // Capture location before completing
      const location = await getLocation();

      await completeTalk.mutateAsync({
        scheduledTalkId,
        data: {
          signatureData,
          signedByName,
          latitude: location?.latitude,
          longitude: location?.longitude,
          accuracyMeters: location?.accuracyMeters,
        },
      });
      setCurrentStep('complete');
      toast.success('Learning completed successfully!');
    } catch {
      toast.error('Failed to complete learning');
      throw new Error('Failed to complete');
    }
  };

  const handleRewatchVideo = async () => {
    try {
      await resetVideoProgress.mutateAsync({ scheduledTalkId });
      setCurrentStep('video');
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch {
      toast.error('Failed to reset video progress');
    }
  };

  const handleNextSection = () => {
    if (!talk) return;

    if (currentSectionIndex < talk.sections.length - 1) {
      setCurrentSectionIndex(currentSectionIndex + 1);
    } else {
      // All sections done, move to next step
      if (talk.requiresQuiz && talk.questions.length > 0) {
        setCurrentStep('quiz');
      } else {
        setCurrentStep('signature');
      }
    }
  };

  const handlePreviousSection = () => {
    if (currentSectionIndex > 0) {
      setCurrentSectionIndex(currentSectionIndex - 1);
    }
  };

  // Check if can proceed to next step
  const canProceedFromVideo = React.useMemo(() => {
    if (!talk) return false;
    if (!talk.videoUrl || talk.videoSource === 'None') return true;
    return (talk.videoWatchPercent ?? 0) >= talk.minimumVideoWatchPercent;
  }, [talk]);

  const canProceedFromSections = React.useMemo(() => {
    if (!talk) return false;
    return talk.completedSections === talk.totalSections;
  }, [talk]);

  // Loading state
  if (isLoading) {
    return <TalkViewerSkeleton />;
  }

  // Error state
  if (error || !talk) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/toolbox-talks">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <h1 className="text-xl font-semibold">Learning</h1>
        </div>
        <Card className="p-8">
          <div className="text-center">
            <AlertTriangle className="h-12 w-12 text-destructive mx-auto mb-4" />
            <h2 className="text-lg font-semibold mb-2">Failed to load learning</h2>
            <p className="text-muted-foreground mb-4">
              {error instanceof Error ? error.message : 'An unexpected error occurred'}
            </p>
            <Button asChild>
              <Link href="/toolbox-talks">Back to My Learnings</Link>
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  // If completed, show success
  if (currentStep === 'complete' && talk.completedAt) {
    // Create a minimal completion object for display
    const completion = {
      id: scheduledTalkId,
      scheduledTalkId,
      completedAt: talk.completedAt,
      totalTimeSpentSeconds: talk.sections.reduce((sum, s) => sum + s.timeSpentSeconds, 0),
      videoWatchPercent: talk.videoWatchPercent,
      quizScore: talk.lastQuizScore,
      quizMaxScore: talk.questions.reduce((sum, q) => sum + q.points, 0) || null,
      quizPassed: talk.lastQuizPassed,
      signatureData: '',
      signedAt: talk.completedAt,
      signedByName: '',
      ipAddress: null,
      userAgent: null,
      certificateUrl: talk.certificateUrl,
      completedLatitude: null,
      completedLongitude: null,
      completedAccuracyMeters: null,
      completedLocationTimestamp: null,
    };

    return (
      <div className="max-w-2xl mx-auto">
        <CompletionSuccess
          talkTitle={talk.title}
          completion={completion}
          totalSections={talk.totalSections}
          hasQuiz={talk.requiresQuiz}
        />
      </div>
    );
  }

  const availableSteps = getAvailableSteps(talk);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/toolbox-talks">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold">{talk.title}</h1>
              {talk.pdfUrl && (
                <Button variant="outline" size="sm" asChild>
                  <a href={talk.pdfUrl} target="_blank" rel="noopener noreferrer" download>
                    <FileDown className="h-4 w-4 mr-1.5" />
                    {talk.pdfFileName || 'Download PDF'}
                  </a>
                </Button>
              )}
            </div>
            <div className="flex items-center gap-3 text-sm text-muted-foreground mt-1">
              <Badge variant={statusVariants[talk.status]}>{talk.statusDisplay}</Badge>
              <span className="flex items-center gap-1">
                <Calendar className="h-3.5 w-3.5" />
                Due {format(new Date(talk.dueDate), 'MMM d, yyyy')}
              </span>
            </div>
          </div>
        </div>

        {/* Step indicator */}
        <StepIndicator
          steps={availableSteps}
          currentStep={currentStep}
          onStepClick={setCurrentStep}
        />
      </div>

      {/* Mobile step selector */}
      <MobileStepSelector
        steps={availableSteps}
        currentStep={currentStep}
        onStepClick={setCurrentStep}
      />

      {/* Progress bar */}
      <Card className="p-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm font-medium">Overall Progress</span>
          <span className="text-sm text-muted-foreground">
            {talk.completedSections} of {talk.totalSections} sections
          </span>
        </div>
        <Progress value={talk.progressPercent} className="h-2" />
      </Card>

      {/* Main content area */}
      <div className="flex gap-6">
        {/* Section navigator (desktop) */}
        {currentStep === 'sections' && (
          <SectionNavigator
            sections={talk.sections}
            currentIndex={currentSectionIndex}
            onSectionClick={setCurrentSectionIndex}
          />
        )}

        {/* Main content */}
        <div className="flex-1 min-w-0">
          {/* Video step */}
          {currentStep === 'video' && talk.videoUrl && (
            <div className="space-y-4">
              <VideoPlayer
                videoUrl={talk.videoUrl}
                videoSource={talk.videoSource}
                minimumWatchPercent={talk.minimumVideoWatchPercent}
                currentWatchPercent={talk.videoWatchPercent}
                onProgressUpdate={handleVideoProgress}
                scheduledTalkId={scheduledTalkId}
                preferredLanguageCode={talk.employeePreferredLanguage}
              />
              {talk.hasSlideshow && (
                <SlideshowSection
                  scheduledTalkId={scheduledTalkId}
                  languageCode={talk.employeePreferredLanguage}
                />
              )}
              <div className="flex justify-end">
                <Button
                  onClick={() => {
                    // Navigate to next step: sections if available, otherwise quiz, otherwise signature
                    if (talk.sections.length > 0) {
                      setCurrentStep('sections');
                    } else if (talk.requiresQuiz && talk.questions.length > 0) {
                      setCurrentStep('quiz');
                    } else {
                      setCurrentStep('signature');
                    }
                  }}
                  disabled={!canProceedFromVideo}
                >
                  {talk.sections.length > 0
                    ? 'Continue to Sections'
                    : talk.requiresQuiz && talk.questions.length > 0
                    ? 'Continue to Quiz'
                    : 'Continue to Sign'}
                </Button>
              </div>
            </div>
          )}

          {/* Sections step */}
          {currentStep === 'sections' && !talk.videoUrl && talk.hasSlideshow && (
            <SlideshowSection
              scheduledTalkId={scheduledTalkId}
              languageCode={talk.employeePreferredLanguage}
            />
          )}
          {currentStep === 'sections' && talk.sections[currentSectionIndex] && (
            <SectionContent
              section={talk.sections[currentSectionIndex]}
              currentIndex={currentSectionIndex}
              totalSections={talk.sections.length}
              onAcknowledge={handleMarkSectionRead}
              onPrevious={handlePreviousSection}
              onNext={handleNextSection}
              isAcknowledging={markSectionRead.isPending}
            />
          )}

          {/* Quiz step */}
          {currentStep === 'quiz' && talk.requiresQuiz && (
            <QuizSection
              questions={talk.questions}
              passingScore={talk.passingScore}
              lastQuizPassed={talk.lastQuizPassed}
              lastQuizScore={talk.lastQuizScore}
              attemptCount={talk.quizAttemptCount}
              onSubmit={handleQuizSubmit}
              onContinue={() => setCurrentStep('signature')}
              onRewatchVideo={talk.videoUrl && talk.videoSource !== 'None' ? handleRewatchVideo : undefined}
            />
          )}

          {/* Signature step */}
          {currentStep === 'signature' && (
            <SignatureCapture
              onComplete={handleComplete}
              defaultName=""
            />
          )}
        </div>
      </div>
    </div>
  );
}

// Skeleton for loading state
function TalkViewerSkeleton() {
  return (
    <div className="space-y-6">
      {/* Header skeleton */}
      <div className="flex items-center gap-4">
        <Skeleton className="h-10 w-10 rounded-md" />
        <div className="space-y-2">
          <Skeleton className="h-6 w-64" />
          <div className="flex gap-2">
            <Skeleton className="h-5 w-20 rounded-full" />
            <Skeleton className="h-5 w-32" />
          </div>
        </div>
      </div>

      {/* Progress skeleton */}
      <Card className="p-4">
        <div className="flex justify-between mb-2">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-4 w-24" />
        </div>
        <Skeleton className="h-2 w-full rounded" />
      </Card>

      {/* Content skeleton */}
      <div className="flex gap-6">
        <div className="hidden lg:block w-64">
          <Card className="p-4 space-y-2">
            <Skeleton className="h-4 w-20" />
            <div className="space-y-2">
              {Array.from({ length: 5 }).map((_, i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          </Card>
        </div>
        <div className="flex-1">
          <Card className="p-6 space-y-4">
            <Skeleton className="h-6 w-3/4" />
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-2/3" />
            <Skeleton className="h-32 w-full" />
          </Card>
        </div>
      </div>
    </div>
  );
}
