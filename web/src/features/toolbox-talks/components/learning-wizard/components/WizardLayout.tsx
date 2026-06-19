'use client';

import { ChevronLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { StepIndicator } from './StepIndicator';
import type { StepItem } from './StepIndicator';

interface WizardLayoutProps {
  title: string;
  steps: StepItem[];
  currentStep: number;
  onStepClick: (step: number) => void;
  children: React.ReactNode;
  // Back/Continue bar
  canGoBack?: boolean;
  canGoNext?: boolean;
  onBack?: () => void;
  onNext?: () => void;
  nextLabel?: string;
  isNavigating?: boolean;
  // Allow custom footer slot (e.g., publish button on last step)
  footer?: React.ReactNode;
}

export function WizardLayout({
  title,
  steps,
  currentStep,
  onStepClick,
  children,
  canGoBack = false,
  canGoNext = false,
  onBack,
  onNext,
  nextLabel = 'Continue',
  isNavigating = false,
  footer,
}: WizardLayoutProps) {
  return (
    <div className="flex flex-col min-h-0">
      {/* Page heading */}
      <div className="mb-6">
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
      </div>

      {/* Step indicator */}
      <div className="mb-8">
        <StepIndicator
          steps={steps}
          currentStep={currentStep}
          onStepClick={onStepClick}
        />
      </div>

      {/* Step content */}
      <Card>
        <CardContent>
          {children}
        </CardContent>
      </Card>

      {/* Navigation bar */}
      <div className="mt-8 pt-4 border-t flex items-center justify-between gap-4">
        <div>
          {canGoBack && onBack && (
            <Button
              type="button"
              variant="ghost"
              onClick={onBack}
              disabled={isNavigating}
              className="gap-1.5"
            >
              <ChevronLeft className="h-4 w-4" aria-hidden="true" />
              Back
            </Button>
          )}
        </div>

        <div className="flex items-center gap-3">
          {footer}
          {canGoNext && onNext && (
            <Button
              type="button"
              onClick={onNext}
              disabled={isNavigating}
            >
              {isNavigating ? 'Saving…' : nextLabel}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
