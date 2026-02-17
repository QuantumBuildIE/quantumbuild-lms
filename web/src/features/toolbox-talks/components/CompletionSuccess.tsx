'use client';

import * as React from 'react';
import Link from 'next/link';
import { format } from 'date-fns';
import {
  CheckCircle2,
  Clock,
  Trophy,
  FileText,
  ArrowLeft,
  Download,
  Share2,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import type { ScheduledTalkCompletion } from '@/types/toolbox-talks';

interface CompletionSuccessProps {
  talkTitle: string;
  completion: ScheduledTalkCompletion;
  totalSections: number;
  hasQuiz: boolean;
  className?: string;
}

// Format seconds to human-readable duration
function formatDuration(totalSeconds: number): string {
  if (totalSeconds < 60) {
    return `${totalSeconds} seconds`;
  }

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  const parts: string[] = [];
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0) parts.push(`${minutes}m`);
  if (seconds > 0 && hours === 0) parts.push(`${seconds}s`);

  return parts.join(' ');
}

interface StatCardProps {
  icon: React.ElementType;
  label: string;
  value: string;
  subValue?: string;
  variant?: 'default' | 'success';
}

function StatCard({ icon: Icon, label, value, subValue, variant = 'default' }: StatCardProps) {
  return (
    <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50">
      <div
        className={cn(
          'flex items-center justify-center w-10 h-10 rounded-full',
          variant === 'success' ? 'bg-green-100 dark:bg-green-900' : 'bg-primary/10'
        )}
      >
        <Icon
          className={cn(
            'h-5 w-5',
            variant === 'success' ? 'text-green-600 dark:text-green-400' : 'text-primary'
          )}
        />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className="font-medium truncate">{value}</p>
        {subValue && <p className="text-xs text-muted-foreground">{subValue}</p>}
      </div>
    </div>
  );
}

export function CompletionSuccess({
  talkTitle,
  completion,
  totalSections,
  hasQuiz,
  className,
}: CompletionSuccessProps) {
  const completedDate = new Date(completion.completedAt);

  // Format quiz score display
  const getQuizDisplay = () => {
    if (!hasQuiz || completion.quizScore === null) return null;
    const percentage = Math.round((completion.quizScore / (completion.quizMaxScore || 1)) * 100);
    return {
      score: `${completion.quizScore}/${completion.quizMaxScore}`,
      percentage: `${percentage}%`,
      passed: completion.quizPassed,
    };
  };

  const quizInfo = getQuizDisplay();

  return (
    <div className={cn('space-y-6', className)}>
      {/* Success banner */}
      <Card className="border-green-200 bg-green-50 dark:border-green-800 dark:bg-green-950">
        <CardContent className="pt-6">
          <div className="flex flex-col items-center text-center space-y-4">
            <div className="flex items-center justify-center w-20 h-20 rounded-full bg-green-100 dark:bg-green-900">
              <CheckCircle2 className="h-10 w-10 text-green-600 dark:text-green-400" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-green-800 dark:text-green-200">
                Learning Completed!
              </h1>
              <p className="text-green-700 dark:text-green-300 mt-1">
                Well done! You have successfully completed this training.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Summary card */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            {talkTitle}
          </CardTitle>
        </CardHeader>

        <CardContent className="space-y-6">
          {/* Stats grid */}
          <div className="grid gap-3 sm:grid-cols-2">
            <StatCard
              icon={CheckCircle2}
              label="Completed On"
              value={format(completedDate, 'MMMM d, yyyy')}
              subValue={format(completedDate, 'h:mm a')}
              variant="success"
            />

            <StatCard
              icon={Clock}
              label="Time Spent"
              value={formatDuration(completion.totalTimeSpentSeconds)}
            />

            {completion.videoWatchPercent !== null && (
              <StatCard
                icon={FileText}
                label="Video Watched"
                value={`${Math.round(completion.videoWatchPercent)}%`}
              />
            )}

            {quizInfo && (
              <StatCard
                icon={Trophy}
                label="Quiz Score"
                value={quizInfo.score}
                subValue={quizInfo.percentage}
                variant={quizInfo.passed ? 'success' : 'default'}
              />
            )}
          </div>

          <Separator />

          {/* Signature confirmation */}
          <div className="space-y-2">
            <h3 className="text-sm font-medium">Acknowledgment</h3>
            <div className="flex items-start gap-4 p-4 rounded-lg border bg-muted/30">
              <div className="flex-1 space-y-1">
                <p className="text-sm">
                  <span className="text-muted-foreground">Signed by: </span>
                  <span className="font-medium">{completion.signedByName}</span>
                </p>
                <p className="text-sm">
                  <span className="text-muted-foreground">Date: </span>
                  <span className="font-medium">
                    {format(new Date(completion.signedAt), 'MMMM d, yyyy \'at\' h:mm a')}
                  </span>
                </p>
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              This record confirms completion of all {totalSections} sections
              {hasQuiz ? ' and the knowledge quiz' : ''}.
            </p>
          </div>

          {/* Certificate download (if available) */}
          {completion.certificateUrl && (
            <>
              <Separator />
              <div className="space-y-2">
                <h3 className="text-sm font-medium">Certificate</h3>
                <div className="flex flex-col sm:flex-row gap-2">
                  <Button variant="outline" className="gap-2" asChild>
                    <a href={completion.certificateUrl} download>
                      <Download className="h-4 w-4" />
                      Download Certificate
                    </a>
                  </Button>
                  <Button variant="ghost" className="gap-2">
                    <Share2 className="h-4 w-4" />
                    Share
                  </Button>
                </div>
              </div>
            </>
          )}
        </CardContent>

        <CardFooter className="border-t pt-4">
          <Button asChild className="w-full gap-2">
            <Link href="/toolbox-talks">
              <ArrowLeft className="h-4 w-4" />
              Back to My Learnings
            </Link>
          </Button>
        </CardFooter>
      </Card>

      {/* Additional information */}
      <Card className="bg-muted/30">
        <CardContent className="py-4">
          <p className="text-sm text-center text-muted-foreground">
            This completion record has been saved. Your supervisor can view your training history
            at any time. If you have any questions about this training, please contact your
            supervisor.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

// Skeleton for loading state
export function CompletionSuccessSkeleton() {
  return (
    <div className="space-y-6">
      <Card className="border-green-200 bg-green-50 dark:border-green-800 dark:bg-green-950">
        <CardContent className="pt-6">
          <div className="flex flex-col items-center text-center space-y-4">
            <div className="w-20 h-20 rounded-full bg-muted animate-pulse" />
            <div className="space-y-2">
              <div className="h-8 w-64 bg-muted rounded animate-pulse mx-auto" />
              <div className="h-4 w-48 bg-muted rounded animate-pulse mx-auto" />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="h-6 w-48 bg-muted rounded animate-pulse" />
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid gap-3 sm:grid-cols-2">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-20 bg-muted rounded-lg animate-pulse" />
            ))}
          </div>
          <Separator />
          <div className="space-y-2">
            <div className="h-4 w-24 bg-muted rounded animate-pulse" />
            <div className="h-24 w-full bg-muted rounded-lg animate-pulse" />
          </div>
        </CardContent>
        <CardFooter className="border-t pt-4">
          <div className="h-10 w-full bg-muted rounded animate-pulse" />
        </CardFooter>
      </Card>
    </div>
  );
}
