'use client';

import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface StepItem {
  number: number;
  label: string;
  reachable: boolean;
  /** Step intentionally bypassed by user config (e.g. requiresQuiz=false skips Quiz). */
  skipped?: boolean;
  subtitle?: string;
}

interface StepIndicatorProps {
  steps: StepItem[];
  currentStep: number;
  onStepClick: (step: number) => void;
}

type StepState = 'current' | 'complete' | 'reachable' | 'skipped' | 'unreachable';

function getStepState(step: StepItem, currentStep: number): StepState {
  if (step.number === currentStep) return 'current';
  if (step.skipped) return 'skipped';
  if (step.number < currentStep) return 'complete';
  if (step.reachable) return 'reachable';
  return 'unreachable';
}

function getAriaLabel(step: StepItem, currentStep: number): string {
  const state = getStepState(step, currentStep);
  const total = 7;
  const base = `Step ${step.number} of ${total}: ${step.label}`;
  if (state === 'current') return `${base}, current step`;
  if (state === 'complete') return `${base}, completed`;
  if (state === 'skipped') return `${base}, skipped`;
  if (state === 'unreachable') return `${base}, not yet reachable`;
  return base;
}

export function StepIndicator({ steps, currentStep, onStepClick }: StepIndicatorProps) {
  return (
    <nav aria-label="Wizard progress">
      <ol
        className="flex items-center overflow-x-auto gap-1 sm:gap-2 pb-1 min-w-0"
        role="list"
      >
        {steps.map((step, idx) => {
          const state = getStepState(step, currentStep);
          const isDisabled = state === 'unreachable' || state === 'skipped';
          const isCurrent = state === 'current';

          return (
            <li key={step.number} className="flex items-center min-w-0 shrink-0">
              {idx > 0 && (
                <div
                  className={cn(
                    'h-px w-6 shrink-0 mx-1',
                    state === 'unreachable' ? 'bg-muted' : 'bg-border'
                  )}
                  aria-hidden="true"
                />
              )}
              <button
                type="button"
                onClick={() => !isDisabled && onStepClick(step.number)}
                disabled={isDisabled}
                aria-current={isCurrent ? 'step' : undefined}
                aria-label={getAriaLabel(step, currentStep)}
                className={cn(
                  'flex flex-col items-center gap-1 min-h-[44px] min-w-[44px] px-2 py-1 rounded-md',
                  'transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  isCurrent && 'cursor-default',
                  !isDisabled && !isCurrent && 'hover:bg-accent',
                  isDisabled && 'cursor-not-allowed opacity-40',
                )}
              >
                <span
                  className={cn(
                    'flex items-center justify-center h-7 w-7 rounded-full border-2 text-xs font-semibold shrink-0',
                    state === 'current'     && 'bg-primary border-primary text-primary-foreground',
                    state === 'complete'    && 'bg-primary/20 border-primary/40 text-primary',
                    state === 'reachable'   && 'bg-background border-border text-foreground',
                    state === 'skipped'     && 'bg-muted border-muted-foreground/30 text-muted-foreground',
                    state === 'unreachable' && 'bg-muted border-muted-foreground/30 text-muted-foreground',
                  )}
                  aria-hidden="true"
                >
                  {state === 'complete' ? (
                    <Check className="h-3.5 w-3.5" />
                  ) : (
                    step.number
                  )}
                </span>
                <span
                  className={cn(
                    'hidden sm:block text-xs whitespace-nowrap',
                    isCurrent ? 'font-semibold text-foreground' : 'text-muted-foreground',
                    isDisabled && 'opacity-40',
                    state === 'skipped' && 'line-through',
                  )}
                >
                  {state === 'skipped' ? `${step.label} — Skipped` : step.label}
                </span>
                {step.subtitle && !isDisabled && (
                  <span className="hidden sm:block text-[10px] text-muted-foreground whitespace-nowrap leading-tight -mt-0.5">
                    {step.subtitle}
                  </span>
                )}
              </button>
            </li>
          );
        })}
      </ol>

      {/* Live region for step transitions */}
      <div
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
      >
        {`Now on step ${currentStep} of ${steps.length}: ${steps.find((s) => s.number === currentStep)?.label ?? ''}`}
      </div>
    </nav>
  );
}
