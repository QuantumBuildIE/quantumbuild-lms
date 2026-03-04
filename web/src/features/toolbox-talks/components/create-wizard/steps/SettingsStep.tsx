'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import {
  useCreationSession,
  useSessionSettings,
  useUpdateSessionSettings,
  useUploadCoverImage,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { TitleDescriptionPanel } from './settings/TitleDescriptionPanel';
import { CategoryPanel } from './settings/CategoryPanel';
import { RefresherPanel } from './settings/RefresherPanel';
import { BehaviourPanel } from './settings/BehaviourPanel';
import type { WizardState } from '../CreateWizard';
import type { ContentCreationSettings, ParsedSection } from '@/types/content-creation';

interface SettingsStepProps {
  state: WizardState;
  updateState: (updates: Partial<WizardState>) => void;
  onNext: () => void;
  onBack: () => void;
}

const DEFAULT_SETTINGS: ContentCreationSettings = {
  title: '',
  description: '',
  coverImageUrl: null,
  category: null,
  refresherFrequency: 'Once',
  isActiveOnPublish: true,
  generateCertificate: true,
  minimumWatchPercent: 90,
  autoAssign: false,
  autoAssignDueDays: 14,
};

export function SettingsStep({ state, onNext, onBack }: SettingsStepProps) {
  const sessionId = state.sessionId;

  // API hooks
  const { data: session } = useCreationSession(sessionId);
  const { data: serverSettings, isLoading } = useSessionSettings(sessionId);
  const updateSettings = useUpdateSessionSettings();
  const uploadCoverImage = useUploadCoverImage();

  // Local state
  const [settings, setSettings] = useState<ContentCreationSettings>(DEFAULT_SETTINGS);
  const [hydrated, setHydrated] = useState(false);

  // Hydrate from server, with fallback to parsed content for title
  useEffect(() => {
    if (!serverSettings || hydrated) return;

    let merged = { ...DEFAULT_SETTINGS, ...serverSettings };

    // If title is still empty, derive from parsed sections
    if (!merged.title && session?.parsedSectionsJson) {
      try {
        const sections: ParsedSection[] = JSON.parse(session.parsedSectionsJson);
        if (sections.length > 0) {
          merged = { ...merged, title: sections[0].title };
        }
      } catch { /* ignore */ }
    }

    setSettings(merged);
    setHydrated(true);
  }, [serverSettings, session?.parsedSectionsJson, hydrated]);

  // Debounced auto-save (500ms)
  const saveRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const handleChange = useCallback(
    (newSettings: ContentCreationSettings) => {
      setSettings(newSettings);
      if (saveRef.current) clearTimeout(saveRef.current);
      saveRef.current = setTimeout(() => {
        if (sessionId) {
          updateSettings.mutate(
            { sessionId, settings: newSettings },
            { onError: () => toast.error('Failed to save settings') }
          );
        }
      }, 500);
    },
    [sessionId, updateSettings]
  );

  // Cleanup debounce timer on unmount
  useEffect(() => {
    return () => {
      if (saveRef.current) clearTimeout(saveRef.current);
    };
  }, []);

  const handleUploadCoverImage = useCallback(
    (file: File) => {
      if (!sessionId) return;
      uploadCoverImage.mutate(
        { sessionId, file },
        {
          onSuccess: (updatedSession) => {
            // Extract the new cover image URL from the updated settings
            if (updatedSession.settingsJson) {
              try {
                const parsed: ContentCreationSettings = JSON.parse(
                  updatedSession.settingsJson
                );
                setSettings((prev) => ({
                  ...prev,
                  coverImageUrl: parsed.coverImageUrl,
                }));
              } catch { /* ignore */ }
            }
            toast.success('Cover image uploaded');
          },
          onError: () => toast.error('Failed to upload cover image'),
        }
      );
    },
    [sessionId, uploadCoverImage]
  );

  const isSaving = updateSettings.isPending;
  const canContinue = settings.title.trim().length > 0;

  if (isLoading && !serverSettings) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mr-2" />
        <span className="text-muted-foreground">Loading settings...</span>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Panel A — Title, Description, Cover Image */}
      <TitleDescriptionPanel
        settings={settings}
        onChange={handleChange}
        onUploadCoverImage={handleUploadCoverImage}
        isUploading={uploadCoverImage.isPending}
        isSaving={isSaving}
      />

      {/* Panel B — Category */}
      <CategoryPanel
        settings={settings}
        onChange={handleChange}
        isSaving={isSaving}
      />

      <div className="grid gap-6 sm:grid-cols-2">
        {/* Panel C — Refresher Frequency */}
        <RefresherPanel
          settings={settings}
          onChange={handleChange}
          isSaving={isSaving}
        />

        {/* Panel D — Behaviour */}
        <BehaviourPanel
          settings={settings}
          onChange={handleChange}
          isSaving={isSaving}
        />
      </div>

      {/* Navigation */}
      <div className="flex justify-between pt-4 border-t">
        <Button variant="outline" onClick={onBack}>
          Back
        </Button>
        <Button onClick={onNext} disabled={!canContinue || isSaving}>
          Continue
        </Button>
      </div>
    </div>
  );
}
