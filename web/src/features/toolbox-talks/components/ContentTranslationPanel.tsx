'use client';

import { useState, useEffect } from 'react';
import { format } from 'date-fns';
import {
  useAvailableLanguages,
  useGenerateContentTranslations,
} from '@/lib/api/toolbox-talks';
import type { ToolboxTalkTranslation } from '@/types/toolbox-talks';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Loader2,
  Languages,
  CheckCircle,
  Globe,
  Trash2,
  RefreshCw,
} from 'lucide-react';
import { toast } from 'sonner';

interface ContentTranslationPanelProps {
  toolboxTalkId: string;
  existingTranslations?: ToolboxTalkTranslation[];
  onTranslationsGenerated?: () => void;
}

export function ContentTranslationPanel({
  toolboxTalkId,
  existingTranslations = [],
  onTranslationsGenerated,
}: ContentTranslationPanelProps) {
  const { data: languagesData, isLoading: isLoadingLanguages } =
    useAvailableLanguages();

  const generateMutation = useGenerateContentTranslations();

  const [selectedLanguages, setSelectedLanguages] = useState<string[]>([]);
  const [showAllLanguages, setShowAllLanguages] = useState(false);

  // Pre-select employee languages when data loads
  useEffect(() => {
    if (languagesData?.employeeLanguages && selectedLanguages.length === 0) {
      setSelectedLanguages(
        languagesData.employeeLanguages.map((l) => l.language)
      );
    }
  }, [languagesData, selectedLanguages.length]);

  const handleGenerateTranslations = async () => {
    if (selectedLanguages.length === 0) {
      toast.error('Please select at least one language');
      return;
    }

    try {
      const result = await generateMutation.mutateAsync({
        toolboxTalkId,
        request: { languages: selectedLanguages },
      });

      const successCount = result.languageResults.filter((r) => r.success).length;
      const failedCount = result.languageResults.filter((r) => !r.success).length;

      if (successCount > 0) {
        toast.success(`Successfully generated ${successCount} translation(s)`);
        onTranslationsGenerated?.();
      }

      if (failedCount > 0) {
        const failedLanguages = result.languageResults
          .filter((r) => !r.success)
          .map((r) => r.language)
          .join(', ');
        toast.error(`Failed to generate translations for: ${failedLanguages}`);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to generate translations';
      toast.error(message);
    }
  };

  const toggleLanguage = (language: string) => {
    setSelectedLanguages((prev) =>
      prev.includes(language)
        ? prev.filter((l) => l !== language)
        : [...prev, language]
    );
  };

  const displayedLanguages = showAllLanguages
    ? languagesData?.allSupportedLanguages || []
    : languagesData?.employeeLanguages || [];

  const existingLanguageCodes = existingTranslations.map((t) => t.languageCode);

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Languages className="h-5 w-5" />
              Content Translations
            </CardTitle>
            <CardDescription>
              Translate sections and quiz questions for employees
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        {/* Existing Translations */}
        {existingTranslations.length > 0 && (
          <div className="space-y-3">
            <Label>Existing Translations</Label>
            <div className="grid gap-2">
              {existingTranslations.map((translation) => (
                <div
                  key={translation.languageCode}
                  className="flex items-center justify-between p-3 bg-muted rounded-md"
                >
                  <div className="flex items-center gap-3">
                    <CheckCircle className="h-4 w-4 text-green-500" />
                    <div>
                      <span className="font-medium">{translation.language}</span>
                      <span className="text-xs text-muted-foreground ml-2">
                        ({translation.languageCode})
                      </span>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-muted-foreground">
                      {translation.translatedAt
                        ? format(new Date(translation.translatedAt), 'dd MMM yyyy HH:mm')
                        : 'Unknown'}
                    </span>
                    <Badge variant="outline" className="text-xs">
                      {translation.translationProvider || 'Unknown'}
                    </Badge>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Language Selection */}
        <div className="space-y-3 pt-4 border-t">
          <div className="flex items-center justify-between">
            <Label>Generate New Translations</Label>
            <Button
              type="button"
              variant="link"
              size="sm"
              className="h-auto p-0"
              onClick={() => setShowAllLanguages(!showAllLanguages)}
            >
              {showAllLanguages
                ? 'Show employee languages'
                : 'Show all languages'}
            </Button>
          </div>

          {isLoadingLanguages ? (
            <div className="flex items-center justify-center py-4">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <>
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-2 max-h-48 overflow-y-auto p-2 border rounded-md">
                {displayedLanguages.map((lang) => {
                  const isExisting = existingLanguageCodes.includes(lang.languageCode);
                  const employeeCount = 'employeeCount' in lang
                    ? (lang as { employeeCount: number }).employeeCount
                    : undefined;

                  return (
                    <div
                      key={lang.languageCode}
                      className="flex items-center gap-2"
                    >
                      <Checkbox
                        id={`content-lang-${lang.languageCode}`}
                        checked={selectedLanguages.includes(lang.language)}
                        onCheckedChange={() => toggleLanguage(lang.language)}
                      />
                      <label
                        htmlFor={`content-lang-${lang.languageCode}`}
                        className="text-sm cursor-pointer flex-1 flex items-center gap-1"
                      >
                        {lang.language}
                        {employeeCount !== undefined && (
                          <span className="text-muted-foreground">
                            ({employeeCount})
                          </span>
                        )}
                        {isExisting && (
                          <span title="Will regenerate existing translation">
                            <RefreshCw className="h-3 w-3 text-muted-foreground" />
                          </span>
                        )}
                      </label>
                    </div>
                  );
                })}
                {displayedLanguages.length === 0 && (
                  <p className="col-span-full text-sm text-muted-foreground text-center py-4">
                    {showAllLanguages
                      ? 'No languages available'
                      : 'No employee languages configured'}
                  </p>
                )}
              </div>
              <p className="text-xs text-muted-foreground">
                {selectedLanguages.length} language(s) selected
                {selectedLanguages.some(
                  (lang) =>
                    existingLanguageCodes.includes(
                      languagesData?.allSupportedLanguages?.find((l) => l.language === lang)
                        ?.languageCode || ''
                    )
                ) && ' (some will be regenerated)'}
              </p>
            </>
          )}
        </div>

        {/* Generate Button */}
        <Button
          type="button"
          onClick={handleGenerateTranslations}
          disabled={
            selectedLanguages.length === 0 ||
            generateMutation.isPending ||
            isLoadingLanguages
          }
          className="w-full"
        >
          {generateMutation.isPending ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Generating Translations...
            </>
          ) : (
            <>
              <Globe className="mr-2 h-4 w-4" />
              Generate Translations
            </>
          )}
        </Button>

        {/* Info Alert */}
        <Alert>
          <Languages className="h-4 w-4" />
          <AlertDescription>
            Translations are generated using AI (Claude) and include section titles,
            content, quiz questions, and answer options. The original English content
            is preserved and shown as fallback when translations are unavailable.
          </AlertDescription>
        </Alert>
      </CardContent>
    </Card>
  );
}
