'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Check, ChevronLeft } from 'lucide-react';
import { cn } from '@/lib/utils';

// Step components
import { BasicInfoStep } from './steps/basic-info-step';
import { ContentStep } from './steps/content-step';
import { GenerateStep } from './steps/generate-step';
import { ReviewStep } from './steps/review-step';
import { PublishStep } from './steps/publish-step';

// Types for wizard state
export interface GeneratedSection {
  id: string;
  sortOrder: number;
  title: string;
  content: string;
  source: 'Video' | 'Pdf' | 'Both' | 'Manual';
  requiresAcknowledgment: boolean;
}

export interface GeneratedQuestion {
  id: string;
  sortOrder: number;
  questionText: string;
  questionType: 'MultipleChoice' | 'TrueFalse';
  options: string[];
  correctAnswerIndex: number;
  source: 'Video' | 'Pdf' | 'Manual';
  points: number;
}

export interface ToolboxTalkWizardData {
  // Step 1: Basic Info
  id?: string;
  title: string;
  description: string;
  category: string;
  frequency: 'Once' | 'Weekly' | 'Monthly' | 'Annually';

  // Step 2: Content
  videoSource: 'DirectUrl' | 'YouTube' | 'GoogleDrive' | 'Vimeo' | null;
  videoUrl: string | null;
  videoFileName: string | null;
  pdfUrl: string | null;
  pdfFileName: string | null;

  // Step 3: Generate options
  generateFromVideo: boolean;
  generateFromPdf: boolean;
  minimumSections: number;
  minimumQuestions: number;

  // Step 4: Generated content (populated after generation)
  sections: GeneratedSection[];
  questions: GeneratedQuestion[];

  // Step 5: Publish settings
  passThreshold: number;
  minimumVideoWatchPercent: number;
  requiresQuiz: boolean;
  isActive: boolean;
  status: 'Draft' | 'Published';

  // Source language
  sourceLanguageCode: string;

  // Quiz randomization
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  useQuestionPool: boolean;
  quizQuestionCount: number;

  // Slideshow
  generateSlidesFromPdf: boolean;

  // Refresher
  requiresRefresher: boolean;
  refresherIntervalMonths: number;

  // Certificate
  generateCertificate: boolean;

  // Auto-assignment
  autoAssignToNewEmployees: boolean;
  autoAssignDueDays: number;
}

const STEPS = [
  { id: 1, name: 'Basic Info', description: 'Title and description' },
  { id: 2, name: 'Content', description: 'Video and PDF' },
  { id: 3, name: 'Generate', description: 'AI content generation' },
  { id: 4, name: 'Review', description: 'Edit sections and quiz' },
  { id: 5, name: 'Publish', description: 'Finalize and publish' },
];

const initialData: ToolboxTalkWizardData = {
  title: '',
  description: '',
  category: '',
  frequency: 'Once',
  videoSource: null,
  videoUrl: null,
  videoFileName: null,
  pdfUrl: null,
  pdfFileName: null,
  generateFromVideo: false,
  generateFromPdf: false,
  minimumSections: 7,
  minimumQuestions: 5,
  sections: [],
  questions: [],
  passThreshold: 80,
  minimumVideoWatchPercent: 80,
  requiresQuiz: true,
  isActive: false,
  status: 'Draft',
  sourceLanguageCode: 'en',
  shuffleQuestions: false,
  shuffleOptions: false,
  useQuestionPool: false,
  quizQuestionCount: 5,
  generateSlidesFromPdf: false,
  requiresRefresher: false,
  refresherIntervalMonths: 12,
  generateCertificate: false,
  autoAssignToNewEmployees: false,
  autoAssignDueDays: 14,
};

export default function NewToolboxTalkWizard() {
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState(1);
  const [wizardData, setWizardData] = useState<ToolboxTalkWizardData>(initialData);

  // DEBUG: Log on mount
  useEffect(() => {
    console.log('🧙 Wizard mounted with initial data:', initialData);
    console.log('🧙 Initial ID:', initialData.id);
  }, []);

  // DEBUG: Log state changes
  useEffect(() => {
    console.log('🧙 Wizard data changed:', wizardData);
    console.log('🧙 Current ID:', wizardData.id);
  }, [wizardData]);

  const updateWizardData = (updates: Partial<ToolboxTalkWizardData>) => {
    console.log('🧙 updateWizardData called with:', updates);
    setWizardData((prev) => {
      const newState = { ...prev, ...updates };
      console.log('🧙 New wizard state:', newState);
      return newState;
    });
  };

  const goToNextStep = () => {
    if (currentStep < STEPS.length) {
      setCurrentStep((prev) => prev + 1);
    }
  };

  const goToPreviousStep = () => {
    if (currentStep > 1) {
      setCurrentStep((prev) => prev - 1);
    }
  };

  const goToStep = (step: number) => {
    // Only allow going to completed steps or the current step
    if (step <= currentStep) {
      setCurrentStep(step);
    }
  };

  const handleCancel = () => {
    if (confirm('Are you sure you want to cancel? All progress will be lost.')) {
      router.push('/admin/toolbox-talks/talks');
    }
  };

  const renderStepContent = () => {
    switch (currentStep) {
      case 1:
        return (
          <BasicInfoStep
            data={wizardData}
            updateData={updateWizardData}
            onNext={goToNextStep}
            onCancel={handleCancel}
          />
        );
      case 2:
        return (
          <ContentStep
            data={wizardData}
            updateData={updateWizardData}
            onNext={goToNextStep}
            onBack={goToPreviousStep}
          />
        );
      case 3:
        return (
          <GenerateStep
            data={wizardData}
            updateData={updateWizardData}
            onNext={goToNextStep}
            onBack={goToPreviousStep}
          />
        );
      case 4:
        return (
          <ReviewStep
            data={wizardData}
            updateData={updateWizardData}
            onNext={goToNextStep}
            onBack={goToPreviousStep}
          />
        );
      case 5:
        return (
          <PublishStep
            data={wizardData}
            updateData={updateWizardData}
            onBack={goToPreviousStep}
            onComplete={() => router.push('/admin/toolbox-talks/talks')}
          />
        );
      default:
        return null;
    }
  };

  return (
    <div className="container mx-auto py-6 max-w-5xl">
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
            <h1 className="text-2xl font-bold">Create Learning</h1>
            <p className="text-muted-foreground">
              Follow the steps below to create a new safety training module
            </p>
          </div>
        </div>
      </div>

      {/* Step Indicator */}
      <nav aria-label="Progress" className="mb-8">
        <ol className="flex items-center">
          {STEPS.map((step, stepIdx) => (
            <li
              key={step.id}
              className={cn(
                'relative',
                stepIdx !== STEPS.length - 1 ? 'pr-8 sm:pr-20 flex-1' : ''
              )}
            >
              {/* Connector line */}
              {stepIdx !== STEPS.length - 1 && (
                <div
                  className={cn(
                    'absolute top-4 left-7 -ml-px mt-0.5 h-0.5 w-full',
                    step.id < currentStep ? 'bg-primary' : 'bg-muted'
                  )}
                />
              )}

              <button
                onClick={() => goToStep(step.id)}
                disabled={step.id > currentStep}
                className={cn(
                  'group relative flex items-start',
                  step.id > currentStep ? 'cursor-not-allowed' : 'cursor-pointer'
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
                      <span>{step.id}</span>
                    )}
                  </span>
                </span>
                <span className="ml-3 flex min-w-0 flex-col">
                  <span
                    className={cn(
                      'text-sm font-medium',
                      step.id <= currentStep ? 'text-primary' : 'text-muted-foreground'
                    )}
                  >
                    {step.name}
                  </span>
                  <span className="text-xs text-muted-foreground hidden sm:block">
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
