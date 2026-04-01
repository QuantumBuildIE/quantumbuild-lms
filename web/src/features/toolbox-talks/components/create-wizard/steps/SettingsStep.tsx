'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Loader2, Languages, ArrowRight } from 'lucide-react';
import { toast } from 'sonner';
import {
  useCreationSession,
  useSessionSettings,
  useUpdateSessionSettings,
  useUploadCoverImage,
  useStartValidation,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { checkSessionTitle } from '@/lib/api/toolbox-talks/content-creation';
import { TitleDescriptionPanel } from './settings/TitleDescriptionPanel';
import { CategoryPanel } from './settings/CategoryPanel';
import { RefresherPanel } from './settings/RefresherPanel';
import { BehaviourPanel } from './settings/BehaviourPanel';
import { SlideshowPanel } from './settings/SlideshowPanel';
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
  generateSlideshow: false,
  slideshowSource: 'sections',
};

export function SettingsStep({ state, onNext, onBack }: SettingsStepProps) {
  const sessionId = state.sessionId;

  // API hooks
  const { data: session } = useCreationSession(sessionId);
  const { data: serverSettings, isLoading } = useSessionSettings(sessionId);
  const updateSettings = useUpdateSessionSettings();
  const uploadCoverImage = useUploadCoverImage();
  const startValidation = useStartValidation();

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
  // Use a ref for the mutation to avoid recreating handleChange when mutation state changes
  const updateSettingsRef = useRef(updateSettings);
  updateSettingsRef.current = updateSettings;
  const saveRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const handleChange = useCallback(
    (newSettings: ContentCreationSettings) => {
      setSettings(newSettings);
      if (saveRef.current) clearTimeout(saveRef.current);
      saveRef.current = setTimeout(() => {
        if (sessionId) {
          updateSettingsRef.current.mutate(
            { sessionId, settings: newSettings },
            { onError: () => toast.error('Failed to save settings') }
          );
        }
      }, 500);
    },
    [sessionId]
  );

  // Cleanup debounce timer on unmount
  useEffect(() => {
    return () => {
      if (saveRef.current) clearTimeout(saveRef.current);
    };
  }, []);

  const uploadCoverImageRef = useRef(uploadCoverImage);
  uploadCoverImageRef.current = uploadCoverImage;
  const handleUploadCoverImage = useCallback(
    (file: File) => {
      if (!sessionId) return;
      uploadCoverImageRef.current.mutate(
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
    [sessionId]
  );

  const [isStartingValidation, setIsStartingValidation] = useState(false);
  const [titleError, setTitleError] = useState<string | null>(null);
  const isSaving = updateSettings.isPending;
  const canContinue = settings.title.trim().length > 0 && !titleError;

  // Clear title error when user modifies the title
  const handleChangeWithTitleClear = useCallback(
    (newSettings: ContentCreationSettings) => {
      if (newSettings.title !== settings.title) {
        setTitleError(null);
      }
      handleChange(newSettings);
    },
    [handleChange, settings.title]
  );

  // Check title uniqueness on blur
  const handleTitleBlur = useCallback(async () => {
    const title = settings.title.trim();
    if (!title || !sessionId) return;
    try {
      const result = await checkSessionTitle(sessionId, title);
      if (!result.available) {
        setTitleError(result.message || 'A learning with this title already exists. Please choose a different title.');
      }
    } catch {
      // Don't block the user if the check fails
    }
  }, [settings.title, sessionId]);

  // Flush pending settings save, then start translate-validate, then navigate
  const handleContinue = useCallback(async () => {
    if (!sessionId || !canContinue) return;

    setIsStartingValidation(true);
    try {
      // Flush any pending debounced save by saving immediately
      if (saveRef.current) {
        clearTimeout(saveRef.current);
        saveRef.current = null;
      }
      await updateSettings.mutateAsync({ sessionId, settings });

      // Start translate-validate (backend will sync quiz + settings to draft talk)
      await startValidation.mutateAsync({
        sessionId,
        request: {
          targetLanguageCodes: state.targetLanguageCodes,
        },
      });

      onNext();
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to start validation';
      toast.error('Error', { description: message });
    } finally {
      setIsStartingValidation(false);
    }
  }, [sessionId, canContinue, settings, state.targetLanguageCodes, updateSettings, startValidation, onNext]);

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
        onChange={handleChangeWithTitleClear}
        onUploadCoverImage={handleUploadCoverImage}
        onTitleBlur={handleTitleBlur}
        titleError={titleError}
        isUploading={uploadCoverImage.isPending}
        isSaving={isSaving}
      />

      <div className="grid gap-6 sm:grid-cols-2">
        {/* Panel B — Category */}
        <CategoryPanel
          settings={settings}
          onChange={handleChange}
          isSaving={isSaving}
        />

        {/* Panel C — Refresher Frequency */}
        <RefresherPanel
          settings={settings}
          onChange={handleChange}
          isSaving={isSaving}
        />
      </div>

      {/* Panel D — Behaviour */}
      <BehaviourPanel
        settings={settings}
        onChange={handleChange}
        isSaving={isSaving}
      />

      {/* Panel E — Slideshow */}
      <SlideshowPanel
        settings={settings}
        onChange={handleChange}
        inputMode={session?.inputMode ?? 'Text'}
        isSaving={isSaving}
      />

      {/* Navigation */}
      <div className="flex justify-between pt-4 border-t">
        <Button variant="outline" onClick={onBack} disabled={isStartingValidation}>
          Back
        </Button>
        <Button
          onClick={handleContinue}
          disabled={!canContinue || isSaving || isStartingValidation}
        >
          {isStartingValidation ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              {state.targetLanguageCodes.length > 0 ? 'Starting Validation...' : 'Preparing...'}
            </>
          ) : state.targetLanguageCodes.length > 0 ? (
            <>
              <Languages className="mr-2 h-4 w-4" />
              Translate & Validate
              <ArrowRight className="ml-2 h-4 w-4" />
            </>
          ) : (
            <>
              Continue to Publish
              <ArrowRight className="ml-2 h-4 w-4" />
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
