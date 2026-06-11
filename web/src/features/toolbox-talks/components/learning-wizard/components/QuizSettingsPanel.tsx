'use client';

import { useEffect, useRef } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Input } from '@/components/ui/input';
import { quizSettingsSchema, type QuizSettingsFormData } from '../schemas/quizSettingsSchema';
import type { ToolboxTalk } from '@/types/toolbox-talks';
import type { UpdateTalkQuizSettingsRequest } from '@/lib/api/toolbox-talks/toolbox-talks';

interface QuizSettingsPanelProps {
  talk: ToolboxTalk;
  onSave: (settings: UpdateTalkQuizSettingsRequest) => void;
  isSaving?: boolean;
}

export function QuizSettingsPanel({ talk, onSave, isSaving }: QuizSettingsPanelProps) {
  const saveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const form = useForm<QuizSettingsFormData>({
    resolver: zodResolver(quizSettingsSchema),
    defaultValues: {
      requiresQuiz: talk.requiresQuiz,
      passingScore: talk.passingScore ?? 80,
      quizQuestionCount: talk.quizQuestionCount ?? null,
      shuffleQuestions: talk.shuffleQuestions ?? false,
      shuffleOptions: talk.shuffleOptions ?? false,
      useQuestionPool: talk.useQuestionPool ?? false,
      allowRetry: talk.allowRetry ?? true,
    },
  });

  // Auto-save on blur — debounce to avoid rapid consecutive saves
  function scheduleSave() {
    if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    saveTimerRef.current = setTimeout(() => {
      const values = form.getValues();
      if (form.formState.isValid) {
        onSave({
          requiresQuiz: values.requiresQuiz,
          passingScore: values.passingScore,
          quizQuestionCount: values.quizQuestionCount ?? null,
          shuffleQuestions: values.shuffleQuestions,
          shuffleOptions: values.shuffleOptions,
          useQuestionPool: values.useQuestionPool,
          allowRetry: values.allowRetry,
        });
      }
    }, 300);
  }

  // Clean up timer on unmount
  useEffect(() => {
    return () => {
      if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    };
  }, []);

  return (
    <div className="border rounded-lg p-4 space-y-5 bg-card">
      <h3 className="text-sm font-semibold">Quiz Settings</h3>

      {/* Passing score */}
      <div className="space-y-1.5">
        <Label htmlFor="passingScore">Passing Score (%)</Label>
        <div className="flex items-center gap-3">
          <Input
            id="passingScore"
            type="number"
            min={1}
            max={100}
            className="w-24"
            {...form.register('passingScore', { valueAsNumber: true })}
            onBlur={scheduleSave}
          />
          <span className="text-sm text-muted-foreground">
            Employees must score at least this % to pass
          </span>
        </div>
        {form.formState.errors.passingScore && (
          <p className="text-xs text-destructive">{form.formState.errors.passingScore.message}</p>
        )}
      </div>

      {/* Question pool count */}
      <div className="space-y-1.5">
        <Label htmlFor="quizQuestionCount">Questions to Show</Label>
        <div className="flex items-center gap-3">
          <Input
            id="quizQuestionCount"
            type="number"
            min={1}
            className="w-24"
            placeholder="All"
            value={form.watch('quizQuestionCount') ?? ''}
            onChange={(e) => {
              const v = e.target.value ? parseInt(e.target.value, 10) : null;
              form.setValue('quizQuestionCount', v);
            }}
            onBlur={scheduleSave}
          />
          <span className="text-sm text-muted-foreground">Leave blank to show all questions</span>
        </div>
      </div>

      {/* Toggles */}
      <div className="space-y-3">
        <SettingsToggle
          id="shuffleQuestions"
          label="Shuffle Questions"
          description="Randomise the order of questions each time"
          checked={form.watch('shuffleQuestions')}
          onCheckedChange={(v) => {
            form.setValue('shuffleQuestions', v);
            scheduleSave();
          }}
          disabled={isSaving}
        />
        <SettingsToggle
          id="shuffleOptions"
          label="Shuffle Answer Options"
          description="Randomise the order of multiple-choice options"
          checked={form.watch('shuffleOptions')}
          onCheckedChange={(v) => {
            form.setValue('shuffleOptions', v);
            scheduleSave();
          }}
          disabled={isSaving}
        />
        <SettingsToggle
          id="useQuestionPool"
          label="Use Question Pool"
          description="Draw a random subset from all available questions"
          checked={form.watch('useQuestionPool')}
          onCheckedChange={(v) => {
            form.setValue('useQuestionPool', v);
            scheduleSave();
          }}
          disabled={isSaving}
        />
        <SettingsToggle
          id="allowRetry"
          label="Allow Retry"
          description="Employees may retake a failed quiz without rewatching the video first"
          checked={form.watch('allowRetry')}
          onCheckedChange={(v) => {
            form.setValue('allowRetry', v);
            scheduleSave();
          }}
          disabled={isSaving}
        />
      </div>
    </div>
  );
}

interface SettingsToggleProps {
  id: string;
  label: string;
  description: string;
  checked: boolean;
  onCheckedChange: (v: boolean) => void;
  disabled?: boolean;
}

function SettingsToggle({ id, label, description, checked, onCheckedChange, disabled }: SettingsToggleProps) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div className="space-y-0.5">
        <Label htmlFor={id} className="cursor-pointer">{label}</Label>
        <p className="text-xs text-muted-foreground">{description}</p>
      </div>
      <Switch
        id={id}
        checked={checked}
        onCheckedChange={onCheckedChange}
        disabled={disabled}
        className="shrink-0"
      />
    </div>
  );
}
