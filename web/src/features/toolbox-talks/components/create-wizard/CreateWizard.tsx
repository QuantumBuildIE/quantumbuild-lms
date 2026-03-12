'use client';

import { useState, useCallback, lazy, Suspense } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Check, ChevronLeft, Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { WizardStep, InputMode, OutputType, ParsedSection } from '@/types/content-creation';

// Lazy-load step components
const InputConfigStep = lazy(() =>
  import('./steps/InputConfigStep').then((m) => ({ default: m.InputConfigStep }))
);
const ParseStep = lazy(() =>
  import('./steps/ParseStep').then((m) => ({ default: m.ParseStep }))
);
const TranslateValidateStep = lazy(() =>
  import('./steps/TranslateValidateStep').then((m) => ({
    default: m.TranslateValidateStep,
  }))
);
const QuizStep = lazy(() =>
  import('./steps/QuizStep').then((m) => ({ default: m.QuizStep }))
);
const SettingsStep = lazy(() =>
  import('./steps/SettingsStep').then((m) => ({ default: m.SettingsStep }))
);
const PublishStep = lazy(() =>
  import('./steps/PublishStep').then((m) => ({ default: m.PublishStep }))
);

// ============================================
// Wizard Steps Config
// ============================================

const STEPS = [
  { id: 1 as WizardStep, name: 'Input & Config', description: 'Content source and settings' },
  { id: 2 as WizardStep, name: 'Parse', description: 'Extract sections from content' },
  { id: 3 as WizardStep, name: 'Quiz', description: 'Review generated questions' },
  { id: 4 as WizardStep, name: 'Settings', description: 'Title, category, behaviour' },
  { id: 5 as WizardStep, name: 'Translate & Validate', description: 'AI translation with validation' },
  { id: 6 as WizardStep, name: 'Publish', description: 'Review and publish' },
];

// ============================================
// Wizard State
// ============================================

export interface WizardState {
  sessionId: string | null;
  inputMode: InputMode | null;
  sourceText: string;
  sourceFile: File | null;
  sourceFileName: string | null;
  videoUrl: string;
  targetLanguageCodes: string[];
  passThreshold: number;
  includeQuiz: boolean;
  // Audit metadata
  reviewerName: string;
  reviewerOrg: string;
  reviewerRole: string;
  documentRef: string;
  clientName: string;
  auditPurpose: string;
  // Parse results
  parsedSections: ParsedSection[];
  suggestedOutputType: OutputType | null;
  selectedOutputType: OutputType | null;
  // Validation
  validationRunIds: string[];
}

const initialState: WizardState = {
  sessionId: null,
  inputMode: null,
  sourceText: '',
  sourceFile: null,
  sourceFileName: null,
  videoUrl: '',
  targetLanguageCodes: [],
  passThreshold: 75,
  includeQuiz: true,
  reviewerName: '',
  reviewerOrg: '',
  reviewerRole: '',
  documentRef: '',
  clientName: '',
  auditPurpose: '',
  parsedSections: [],
  suggestedOutputType: null,
  selectedOutputType: null,
  validationRunIds: [],
};

// ============================================
// Component
// ============================================

export function CreateWizard() {
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState<WizardStep>(1);
  const [highestStep, setHighestStep] = useState<WizardStep>(1);
  const [wizardState, setWizardState] = useState<WizardState>(initialState);

  const updateState = useCallback((updates: Partial<WizardState>) => {
    setWizardState((prev) => ({ ...prev, ...updates }));
  }, []);

  const goToNextStep = useCallback(() => {
    setCurrentStep((prev) => {
      let next = Math.min(prev + 1, 6) as WizardStep;
      // Skip Quiz step when quiz is excluded
      if (next === 3 && !wizardState.includeQuiz) next = 4 as WizardStep;
      setHighestStep((h) => Math.max(h, next) as WizardStep);
      return next;
    });
  }, [wizardState.includeQuiz]);

  const goToPreviousStep = useCallback(() => {
    setCurrentStep((prev) => {
      let next = Math.max(prev - 1, 1) as WizardStep;
      // Skip Quiz step when quiz is excluded
      if (next === 3 && !wizardState.includeQuiz) next = 2 as WizardStep;
      return next;
    });
  }, [wizardState.includeQuiz]);

  const goToStep = useCallback(
    (step: WizardStep) => {
      // Prevent navigating to Quiz step when quiz is excluded
      if (step === 3 && !wizardState.includeQuiz) return;
      if (step <= highestStep) {
        setCurrentStep(step);
      }
    },
    [highestStep, wizardState.includeQuiz]
  );

  // Filter out Quiz step from display when quiz is excluded
  const visibleSteps = wizardState.includeQuiz
    ? STEPS
    : STEPS.filter((s) => s.id !== 3);

  const handleCancel = () => {
    if (confirm('Are you sure you want to cancel? All progress will be lost.')) {
      router.push('/admin/toolbox-talks/talks');
    }
  };

  const renderStepContent = () => {
    switch (currentStep) {
      case 1:
        return (
          <Suspense fallback={<StepLoader />}>
            <InputConfigStep
              state={wizardState}
              updateState={updateState}
              onNext={goToNextStep}
              onCancel={handleCancel}
            />
          </Suspense>
        );
      case 2:
        return (
          <Suspense fallback={<StepLoader />}>
            <ParseStep
              state={wizardState}
              updateState={updateState}
              onNext={goToNextStep}
              onBack={goToPreviousStep}
            />
          </Suspense>
        );
      case 3:
        return (
          <Suspense fallback={<StepLoader />}>
            <QuizStep
              state={wizardState}
              updateState={updateState}
              onNext={goToNextStep}
              onBack={goToPreviousStep}
            />
          </Suspense>
        );
      case 4:
        return (
          <Suspense fallback={<StepLoader />}>
            <SettingsStep
              state={wizardState}
              updateState={updateState}
              onNext={goToNextStep}
              onBack={goToPreviousStep}
            />
          </Suspense>
        );
      case 5:
        return (
          <Suspense fallback={<StepLoader />}>
            <TranslateValidateStep
              state={wizardState}
              updateState={updateState}
              onNext={goToNextStep}
              onBack={goToPreviousStep}
            />
          </Suspense>
        );
      case 6:
        return (
          <Suspense fallback={<StepLoader />}>
            <PublishStep
              state={wizardState}
              updateState={updateState}
              onBack={goToPreviousStep}
            />
          </Suspense>
        );
      default:
        return null;
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="mb-8">
        <div className="flex items-center gap-4 mb-2">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.push('/admin/toolbox-talks/talks')}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold">Create Content</h1>
            <p className="text-muted-foreground">
              AI-powered content creation with translation validation
            </p>
          </div>
        </div>
      </div>

      {/* Step Indicator */}
      <nav aria-label="Progress" className="mb-8">
        <ol className="flex items-center">
          {visibleSteps.map((step, stepIdx) => (
            <li
              key={step.id}
              className={cn(
                'relative',
                stepIdx !== visibleSteps.length - 1 ? 'flex-1 pr-8 sm:pr-20' : ''
              )}
            >
              {/* Connector line */}
              {stepIdx !== visibleSteps.length - 1 && (
                <div
                  className={cn(
                    'absolute left-7 top-4 -ml-px mt-0.5 h-0.5 w-full',
                    step.id < currentStep ? 'bg-primary' : 'bg-muted'
                  )}
                />
              )}

              <button
                onClick={() => goToStep(step.id)}
                disabled={step.id > highestStep}
                className={cn(
                  'group relative flex items-start',
                  step.id > highestStep
                    ? 'cursor-not-allowed'
                    : 'cursor-pointer'
                )}
              >
                <span className="flex h-9 items-center">
                  <span
                    className={cn(
                      'relative z-10 flex h-8 w-8 items-center justify-center rounded-full',
                      step.id < currentStep
                        ? 'bg-primary text-primary-foreground'
                        : step.id === currentStep
                          ? 'border-2 border-primary bg-background text-primary'
                          : 'border-2 border-muted bg-background text-muted-foreground'
                    )}
                  >
                    {step.id < currentStep ? (
                      <Check className="h-4 w-4" />
                    ) : (
                      <span className="text-xs">{step.id}</span>
                    )}
                  </span>
                </span>
                <span className="ml-3 flex min-w-0 flex-col">
                  <span
                    className={cn(
                      'text-sm font-medium',
                      step.id <= currentStep
                        ? 'text-primary'
                        : 'text-muted-foreground'
                    )}
                  >
                    {step.name}
                  </span>
                  <span className="hidden text-xs text-muted-foreground sm:block">
                    {step.description}
                  </span>
                </span>
              </button>
            </li>
          ))}
        </ol>
      </nav>

      {/* Step Content */}
      <Card>
        <CardContent className="pt-6">{renderStepContent()}</CardContent>
      </Card>
    </div>
  );
}

function StepLoader() {
  return (
    <div className="flex items-center justify-center py-12">
      <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
    </div>
  );
}
