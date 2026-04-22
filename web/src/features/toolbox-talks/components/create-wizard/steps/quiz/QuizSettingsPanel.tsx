'use client';

import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Minus, Plus } from 'lucide-react';
import type { QuizSettings } from '@/types/content-creation';

interface QuizSettingsPanelProps {
  settings: QuizSettings;
  onChange: (settings: QuizSettings) => void;
  isSaving?: boolean;
}

export function QuizSettingsPanel({ settings, onChange, isSaving }: QuizSettingsPanelProps) {
  const updateField = <K extends keyof QuizSettings>(key: K, value: QuizSettings[K]) => {
    onChange({ ...settings, [key]: value });
  };

  const adjustScore = (delta: number) => {
    const newScore = Math.min(100, Math.max(50, settings.passingScore + delta));
    updateField('passingScore', newScore);
  };

  return (
    <div className="rounded-lg border bg-muted/30 p-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {/* Require Quiz */}
        <div className="flex items-center justify-between gap-3">
          <Label htmlFor="require-quiz" className="text-sm">
            Require Quiz
          </Label>
          <Switch
            id="require-quiz"
            checked={settings.requireQuiz}
            onCheckedChange={(v) => updateField('requireQuiz', v)}
            disabled={isSaving}
          />
        </div>

        {/* Passing Score */}
        <div className="flex items-center justify-between gap-3">
          <Label className="text-sm">Passing Score</Label>
          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="icon"
              className="h-7 w-7"
              onClick={() => adjustScore(-5)}
              disabled={settings.passingScore <= 50 || isSaving}
            >
              <Minus className="h-3 w-3" />
            </Button>
            <span className="w-12 text-center text-sm font-medium tabular-nums">
              {settings.passingScore}%
            </span>
            <Button
              variant="outline"
              size="icon"
              className="h-7 w-7"
              onClick={() => adjustScore(5)}
              disabled={settings.passingScore >= 100 || isSaving}
            >
              <Plus className="h-3 w-3" />
            </Button>
          </div>
        </div>

        {/* Shuffle Questions */}
        <div className="flex items-center justify-between gap-3">
          <Label htmlFor="shuffle-questions" className="text-sm">
            Shuffle Questions
          </Label>
          <Switch
            id="shuffle-questions"
            checked={settings.shuffleQuestions}
            onCheckedChange={(v) => updateField('shuffleQuestions', v)}
            disabled={isSaving}
          />
        </div>

        {/* Shuffle Options */}
        <div className="flex items-center justify-between gap-3">
          <Label htmlFor="shuffle-options" className="text-sm">
            Shuffle Options
          </Label>
          <Switch
            id="shuffle-options"
            checked={settings.shuffleOptions}
            onCheckedChange={(v) => updateField('shuffleOptions', v)}
            disabled={isSaving}
          />
        </div>

        {/* Allow Retry */}
        <div className="flex items-center justify-between gap-3">
          <Label htmlFor="allow-retry" className="text-sm">
            Allow Retry
          </Label>
          <Switch
            id="allow-retry"
            checked={settings.allowRetry}
            onCheckedChange={(v) => updateField('allowRetry', v)}
            disabled={isSaving}
          />
        </div>
      </div>
    </div>
  );
}
