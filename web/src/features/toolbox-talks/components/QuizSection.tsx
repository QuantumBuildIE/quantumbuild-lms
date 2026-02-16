'use client';

import * as React from 'react';
import {
  Check,
  X,
  RotateCcw,
  Loader2,
  HelpCircle,
  Trophy,
  AlertCircle,
  Video,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';
import type { MyToolboxTalkQuestion, QuizResult, QuestionResult } from '@/types/toolbox-talks';

interface QuizSectionProps {
  questions: MyToolboxTalkQuestion[];
  passingScore: number | null;
  lastQuizPassed: boolean | null;
  lastQuizScore: number | null;
  attemptCount: number;
  onSubmit: (answers: Record<string, string>) => Promise<QuizResult>;
  onContinue: () => void;
  onRewatchVideo?: () => void;
  className?: string;
}

interface QuestionProps {
  question: MyToolboxTalkQuestion;
  answer: string;
  onAnswerChange: (answer: string) => void;
  result?: QuestionResult;
  showResults: boolean;
  disabled: boolean;
}

function Question({
  question,
  answer,
  onAnswerChange,
  result,
  showResults,
  disabled,
}: QuestionProps) {
  const isCorrect = result?.isCorrect;

  return (
    <div
      className={cn(
        'p-4 rounded-lg border transition-colors',
        showResults && isCorrect && 'border-green-200 bg-green-50 dark:border-green-800 dark:bg-green-950',
        showResults && !isCorrect && 'border-red-200 bg-red-50 dark:border-red-800 dark:bg-red-950',
        !showResults && 'bg-card'
      )}
    >
      <div className="flex items-start gap-3">
        <span className="flex-shrink-0 w-6 h-6 rounded-full bg-primary text-primary-foreground text-sm font-medium flex items-center justify-center">
          {question.questionNumber}
        </span>
        <div className="flex-1 space-y-3">
          <p className="font-medium">{question.questionText}</p>

          {/* Multiple Choice - sends original option index for translation-safe grading */}
          {question.questionType === 'MultipleChoice' && question.options && (
            <div className="space-y-2">
              {question.options.map((option, displayIndex) => {
                // Map display index to original index (handles shuffled options)
                const originalIndex = question.optionOriginalIndices
                  ? question.optionOriginalIndices[displayIndex]
                  : displayIndex;
                const indexStr = originalIndex.toString();
                const isSelected = answer === indexStr;
                const isCorrectOption = result?.correctOptionIndex != null
                  ? originalIndex === result.correctOptionIndex
                  : showResults && result?.correctAnswer === option;

                return (
                  <label
                    key={displayIndex}
                    className={cn(
                      'flex items-center gap-3 p-3 rounded-md border cursor-pointer transition-colors',
                      !disabled && 'hover:bg-muted',
                      isSelected && 'border-primary bg-primary/5',
                      disabled && 'cursor-default opacity-75',
                      showResults && isCorrectOption && 'border-green-500 bg-green-100 dark:bg-green-900',
                      showResults && isSelected && !isCorrect && 'border-red-500'
                    )}
                  >
                    <input
                      type="radio"
                      name={`question-${question.id}`}
                      value={indexStr}
                      checked={isSelected}
                      onChange={(e) => onAnswerChange(e.target.value)}
                      disabled={disabled}
                      className="h-4 w-4"
                    />
                    <span className="flex-1">{option}</span>
                    {showResults && isCorrectOption && (
                      <Check className="h-4 w-4 text-green-600" />
                    )}
                    {showResults && isSelected && !isCorrect && (
                      <X className="h-4 w-4 text-red-600" />
                    )}
                  </label>
                );
              })}
            </div>
          )}

          {/* True/False */}
          {question.questionType === 'TrueFalse' && (
            <div className="flex gap-4">
              {['True', 'False'].map((option) => (
                <label
                  key={option}
                  className={cn(
                    'flex items-center gap-2 px-4 py-2 rounded-md border cursor-pointer transition-colors',
                    !disabled && 'hover:bg-muted',
                    answer === option && 'border-primary bg-primary/5',
                    disabled && 'cursor-default opacity-75',
                    showResults && result?.correctAnswer === option && 'border-green-500 bg-green-100 dark:bg-green-900',
                    showResults && answer === option && !isCorrect && 'border-red-500'
                  )}
                >
                  <input
                    type="radio"
                    name={`question-${question.id}`}
                    value={option}
                    checked={answer === option}
                    onChange={(e) => onAnswerChange(e.target.value)}
                    disabled={disabled}
                    className="h-4 w-4"
                  />
                  <span>{option}</span>
                </label>
              ))}
            </div>
          )}

          {/* Short Answer */}
          {question.questionType === 'ShortAnswer' && (
            <div className="space-y-2">
              <Input
                value={answer}
                onChange={(e) => onAnswerChange(e.target.value)}
                placeholder="Type your answer..."
                disabled={disabled}
                className={cn(
                  showResults && isCorrect && 'border-green-500',
                  showResults && !isCorrect && 'border-red-500'
                )}
              />
              {showResults && !isCorrect && result?.correctAnswer && (
                <p className="text-sm text-green-600">
                  Correct answer: {result.correctAnswer}
                </p>
              )}
            </div>
          )}

          {/* Points display */}
          <div className="text-xs text-muted-foreground">
            {showResults ? (
              <span>
                {result?.pointsEarned ?? 0} / {result?.maxPoints ?? question.points} points
              </span>
            ) : (
              <span>{question.points} point{question.points !== 1 ? 's' : ''}</span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

interface QuizResultsProps {
  result: QuizResult;
  onRetry: () => void;
  onContinue: () => void;
  onRewatchVideo?: () => void;
}

function QuizResults({ result, onRetry, onContinue, onRewatchVideo }: QuizResultsProps) {
  return (
    <div className="text-center space-y-6 py-6">
      <div
        className={cn(
          'inline-flex items-center justify-center w-20 h-20 rounded-full',
          result.passed ? 'bg-green-100 dark:bg-green-900' : 'bg-red-100 dark:bg-red-900'
        )}
      >
        {result.passed ? (
          <Trophy className="h-10 w-10 text-green-600 dark:text-green-400" />
        ) : (
          <AlertCircle className="h-10 w-10 text-red-600 dark:text-red-400" />
        )}
      </div>

      <div>
        <h3 className={cn('text-2xl font-bold', result.passed ? 'text-green-600' : 'text-red-600')}>
          {result.passed ? 'Congratulations!' : 'Not Quite'}
        </h3>
        <p className="text-muted-foreground mt-1">
          {result.passed
            ? 'You passed the quiz!'
            : `You need ${result.passingScore}% to pass. Try again!`}
        </p>
      </div>

      <div className="flex items-center justify-center gap-4">
        <div className="text-center">
          <div className="text-3xl font-bold">{result.score}</div>
          <div className="text-sm text-muted-foreground">Score</div>
        </div>
        <div className="w-px h-12 bg-border" />
        <div className="text-center">
          <div className="text-3xl font-bold">{result.maxScore}</div>
          <div className="text-sm text-muted-foreground">Max Score</div>
        </div>
        <div className="w-px h-12 bg-border" />
        <div className="text-center">
          <div
            className={cn(
              'text-3xl font-bold',
              result.passed ? 'text-green-600' : 'text-red-600'
            )}
          >
            {result.percentage}%
          </div>
          <div className="text-sm text-muted-foreground">Percentage</div>
        </div>
      </div>

      <div className="flex justify-center gap-4">
        {!result.passed && (
          <>
            <Button variant="outline" onClick={onRetry} className="gap-2">
              <RotateCcw className="h-4 w-4" />
              Try Again
            </Button>
            {onRewatchVideo && (
              <Button variant="outline" onClick={onRewatchVideo} className="gap-2">
                <Video className="h-4 w-4" />
                Rewatch Video
              </Button>
            )}
          </>
        )}
        {result.passed && (
          <Button onClick={onContinue} className="gap-2">
            Continue
            <Check className="h-4 w-4" />
          </Button>
        )}
      </div>
    </div>
  );
}

export function QuizSection({
  questions,
  passingScore,
  lastQuizPassed,
  lastQuizScore,
  attemptCount,
  onSubmit,
  onContinue,
  onRewatchVideo,
  className,
}: QuizSectionProps) {
  const [answers, setAnswers] = React.useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [result, setResult] = React.useState<QuizResult | null>(null);
  const [showResults, setShowResults] = React.useState(false);

  // If already passed, show success state
  const alreadyPassed = lastQuizPassed === true;

  const handleAnswerChange = (questionId: string, answer: string) => {
    setAnswers((prev) => ({ ...prev, [questionId]: answer }));
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);
    try {
      const quizResult = await onSubmit(answers);
      setResult(quizResult);
      setShowResults(true);
    } catch {
      // Error handled in parent
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleRetry = () => {
    setAnswers({});
    setResult(null);
    setShowResults(false);
  };

  const handleContinue = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
    onContinue();
  };

  // Calculate progress
  const answeredCount = Object.keys(answers).filter((key) => answers[key]?.trim()).length;
  const totalQuestions = questions.length;
  const progressPercent = (answeredCount / totalQuestions) * 100;
  const allAnswered = answeredCount === totalQuestions;

  // Get result for a specific question
  const getQuestionResult = (questionId: string): QuestionResult | undefined => {
    return result?.questionResults.find((r) => r.questionId === questionId);
  };

  if (alreadyPassed) {
    return (
      <Card className={className}>
        <CardContent className="py-12">
          <div className="text-center space-y-4">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-green-100 dark:bg-green-900">
              <Trophy className="h-8 w-8 text-green-600 dark:text-green-400" />
            </div>
            <div>
              <h3 className="text-xl font-semibold text-green-600">Quiz Passed!</h3>
              <p className="text-muted-foreground mt-1">
                You scored {lastQuizScore}% on this quiz.
              </p>
            </div>
            <Button onClick={handleContinue}>
              Continue to Completion
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <HelpCircle className="h-5 w-5 text-muted-foreground" />
            <CardTitle className="text-lg">Knowledge Check</CardTitle>
          </div>
          {attemptCount > 0 && (
            <span className="text-sm text-muted-foreground">
              Attempt {attemptCount + 1}
            </span>
          )}
        </div>
        {passingScore && (
          <p className="text-sm text-muted-foreground">
            You need to score at least {passingScore}% to pass.
          </p>
        )}
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Show results summary if submitted */}
        {showResults && result && (
          <QuizResults result={result} onRetry={handleRetry} onContinue={handleContinue} onRewatchVideo={onRewatchVideo} />
        )}

        {/* Progress indicator */}
        {!showResults && (
          <div className="space-y-2">
            <div className="flex justify-between text-sm text-muted-foreground">
              <span>Progress</span>
              <span>
                {answeredCount} of {totalQuestions} answered
              </span>
            </div>
            <Progress value={progressPercent} className="h-2" />
          </div>
        )}

        {/* Questions */}
        <div className="space-y-4">
          {questions.map((question) => (
            <Question
              key={question.id}
              question={question}
              answer={answers[question.id] || ''}
              onAnswerChange={(answer) => handleAnswerChange(question.id, answer)}
              result={getQuestionResult(question.id)}
              showResults={showResults}
              disabled={isSubmitting || showResults}
            />
          ))}
        </div>
      </CardContent>

      {/* Submit button */}
      {!showResults && (
        <CardFooter className="border-t pt-4">
          <Button
            onClick={handleSubmit}
            disabled={!allAnswered || isSubmitting}
            className="w-full gap-2"
          >
            {isSubmitting ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Submitting...
              </>
            ) : (
              <>
                Submit Quiz
                {!allAnswered && (
                  <span className="text-xs opacity-75">
                    ({totalQuestions - answeredCount} remaining)
                  </span>
                )}
              </>
            )}
          </Button>
        </CardFooter>
      )}
    </Card>
  );
}

// Skeleton for loading state
export function QuizSectionSkeleton() {
  return (
    <Card>
      <CardHeader>
        <div className="h-6 w-32 bg-muted rounded animate-pulse" />
      </CardHeader>
      <CardContent className="space-y-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="p-4 rounded-lg border space-y-3">
            <div className="flex gap-3">
              <div className="w-6 h-6 rounded-full bg-muted animate-pulse" />
              <div className="flex-1 space-y-2">
                <div className="h-4 w-full bg-muted rounded animate-pulse" />
                <div className="h-4 w-3/4 bg-muted rounded animate-pulse" />
                <div className="space-y-2 mt-4">
                  <div className="h-10 w-full bg-muted rounded animate-pulse" />
                  <div className="h-10 w-full bg-muted rounded animate-pulse" />
                </div>
              </div>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
