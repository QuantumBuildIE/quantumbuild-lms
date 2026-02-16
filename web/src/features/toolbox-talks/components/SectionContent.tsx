'use client';

import * as React from 'react';
import { ChevronLeft, ChevronRight, Check, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import type { MyToolboxTalkSection } from '@/types/toolbox-talks';

interface SectionContentProps {
  section: MyToolboxTalkSection;
  currentIndex: number;
  totalSections: number;
  onAcknowledge: (sectionId: string, timeSpentSeconds?: number) => Promise<void>;
  onPrevious: () => void;
  onNext: () => void;
  isAcknowledging?: boolean;
}

const SECTION_TIMER_SECONDS = 5;

export function SectionContent({
  section,
  currentIndex,
  totalSections,
  onAcknowledge,
  onPrevious,
  onNext,
  isAcknowledging = false,
}: SectionContentProps) {
  const [acknowledged, setAcknowledged] = React.useState(section.isRead);
  const [startTime] = React.useState(Date.now());
  const [secondsRemaining, setSecondsRemaining] = React.useState(SECTION_TIMER_SECONDS);
  const [timerComplete, setTimerComplete] = React.useState(false);

  // Track if section was already read when mounted
  const wasAlreadyRead = React.useRef(section.isRead);

  // Reset acknowledged state when section changes
  React.useEffect(() => {
    setAcknowledged(section.isRead);
    wasAlreadyRead.current = section.isRead;
  }, [section.sectionId, section.isRead]);

  // Countdown timer - resets on section change, skips for already-read sections
  React.useEffect(() => {
    if (section.isRead) {
      setTimerComplete(true);
      setSecondsRemaining(0);
      return;
    }

    setSecondsRemaining(SECTION_TIMER_SECONDS);
    setTimerComplete(false);

    const timer = setInterval(() => {
      setSecondsRemaining((prev) => {
        if (prev <= 1) {
          clearInterval(timer);
          setTimerComplete(true);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [section.sectionId, section.isRead]);

  const handleAcknowledge = async () => {
    if (acknowledged || wasAlreadyRead.current) return;

    const timeSpentSeconds = Math.floor((Date.now() - startTime) / 1000);
    try {
      await onAcknowledge(section.sectionId, timeSpentSeconds);
      setAcknowledged(true);
    } catch {
      // Error handled in parent
    }
  };

  const canProceed = timerComplete && (!section.requiresAcknowledgment || acknowledged);
  const isLastSection = currentIndex === totalSections - 1;
  const isFirstSection = currentIndex === 0;

  return (
    <Card className="flex flex-col h-full">
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between gap-4">
          <CardTitle className="text-lg">
            Section {section.sectionNumber}: {section.title}
          </CardTitle>
          {acknowledged && (
            <div className="flex items-center gap-1 text-sm text-green-600">
              <Check className="h-4 w-4" />
              <span className="hidden sm:inline">Read</span>
            </div>
          )}
        </div>
      </CardHeader>

      <CardContent className="flex-1 overflow-auto">
        {/* Section content - rendered as HTML */}
        <div
          className={cn(
            'prose prose-sm max-w-none dark:prose-invert',
            // Ensure proper styling for common HTML elements
            '[&>p]:mb-4 [&>ul]:mb-4 [&>ol]:mb-4',
            '[&>h1]:text-xl [&>h2]:text-lg [&>h3]:text-base',
            '[&>ul]:list-disc [&>ul]:pl-6',
            '[&>ol]:list-decimal [&>ol]:pl-6',
            '[&>blockquote]:border-l-4 [&>blockquote]:border-muted [&>blockquote]:pl-4 [&>blockquote]:italic',
            '[&_img]:max-w-full [&_img]:rounded-lg',
            '[&_table]:border-collapse [&_td]:border [&_td]:p-2 [&_th]:border [&_th]:p-2 [&_th]:bg-muted'
          )}
          dangerouslySetInnerHTML={{ __html: section.content }}
        />
      </CardContent>

      <CardFooter className="flex flex-col gap-4 pt-4 border-t">
        {/* Countdown timer */}
        {!timerComplete && !wasAlreadyRead.current && (
          <div className="w-full space-y-2">
            <div className="h-1.5 bg-muted rounded-full overflow-hidden">
              <div
                className="h-full bg-primary rounded-full transition-all duration-1000 ease-linear"
                style={{ width: `${((SECTION_TIMER_SECONDS - secondsRemaining) / SECTION_TIMER_SECONDS) * 100}%` }}
              />
            </div>
            <p className="text-xs text-muted-foreground text-center">
              Please read this section ({secondsRemaining}s)
            </p>
          </div>
        )}

        {/* Acknowledgment checkbox */}
        {section.requiresAcknowledgment && !wasAlreadyRead.current && (
          <div className="flex items-start gap-3 w-full p-4 bg-muted/50 rounded-lg">
            <Checkbox
              id={`acknowledge-${section.sectionId}`}
              checked={acknowledged}
              onCheckedChange={() => handleAcknowledge()}
              disabled={!timerComplete || isAcknowledging || acknowledged}
              className="mt-0.5"
            />
            <label
              htmlFor={`acknowledge-${section.sectionId}`}
              className={cn(
                'text-sm font-medium leading-none',
                timerComplete && !acknowledged ? 'cursor-pointer' : 'cursor-not-allowed opacity-70'
              )}
            >
              I have read and understood this section
              {isAcknowledging && <Loader2 className="inline-block ml-2 h-4 w-4 animate-spin" />}
            </label>
          </div>
        )}

        {/* Navigation buttons */}
        <div className="flex justify-between w-full gap-4">
          <Button
            variant="outline"
            onClick={onPrevious}
            disabled={isFirstSection}
            className="gap-2"
          >
            <ChevronLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Previous</span>
          </Button>

          <div className="flex items-center text-sm text-muted-foreground">
            Section {currentIndex + 1} of {totalSections}
          </div>

          <Button
            onClick={onNext}
            disabled={!canProceed}
            className="gap-2"
          >
            <span className="hidden sm:inline">
              {isLastSection ? 'Continue' : 'Next'}
            </span>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </CardFooter>
    </Card>
  );
}

// Skeleton for loading state
export function SectionContentSkeleton() {
  return (
    <Card className="flex flex-col h-full">
      <CardHeader className="pb-4">
        <div className="h-6 w-2/3 bg-muted rounded animate-pulse" />
      </CardHeader>
      <CardContent className="flex-1 space-y-4">
        <div className="h-4 w-full bg-muted rounded animate-pulse" />
        <div className="h-4 w-5/6 bg-muted rounded animate-pulse" />
        <div className="h-4 w-4/6 bg-muted rounded animate-pulse" />
        <div className="h-4 w-full bg-muted rounded animate-pulse" />
        <div className="h-4 w-3/4 bg-muted rounded animate-pulse" />
      </CardContent>
      <CardFooter className="flex justify-between pt-4 border-t">
        <div className="h-9 w-24 bg-muted rounded animate-pulse" />
        <div className="h-9 w-24 bg-muted rounded animate-pulse" />
      </CardFooter>
    </Card>
  );
}
