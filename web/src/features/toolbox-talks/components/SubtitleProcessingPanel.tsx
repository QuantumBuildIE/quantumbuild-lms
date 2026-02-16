'use client';

import { useState, useEffect } from 'react';
import {
  useSubtitleProcessing,
  useAvailableLanguages,
} from '@/lib/api/toolbox-talks';
import type { SubtitleVideoSourceType } from '@/types/toolbox-talks';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import { Progress } from '@/components/ui/progress';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Loader2,
  Play,
  CheckCircle,
  XCircle,
  Clock,
  Globe,
  FileVideo,
  Languages,
  ExternalLink,
  Wifi,
  WifiOff,
  RefreshCw,
  StopCircle,
  RotateCcw,
  Ban,
} from 'lucide-react';
import { toast } from 'sonner';

interface SubtitleProcessingPanelProps {
  toolboxTalkId: string;
  currentVideoUrl?: string;
}

export function SubtitleProcessingPanel({
  toolboxTalkId,
  currentVideoUrl,
}: SubtitleProcessingPanelProps) {
  const {
    status,
    isLoading,
    error,
    isConnected,
    startProcessing,
    isStarting,
    cancelProcessing,
    isCancelling,
    cancelError,
    retryProcessing,
    isRetrying,
    retryError,
    hasFailedTranslations,
    refreshStatus,
  } = useSubtitleProcessing(toolboxTalkId);

  const { data: languagesData, isLoading: isLoadingLanguages } =
    useAvailableLanguages();

  const [videoUrl, setVideoUrl] = useState(currentVideoUrl || '');
  const [videoSourceType, setVideoSourceType] =
    useState<SubtitleVideoSourceType>('GoogleDrive');
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

  // Update video URL if prop changes
  useEffect(() => {
    if (currentVideoUrl && !videoUrl) {
      setVideoUrl(currentVideoUrl);
    }
  }, [currentVideoUrl, videoUrl]);

  const handleStartProcessing = async () => {
    if (!videoUrl || selectedLanguages.length === 0) {
      toast.error('Please enter a video URL and select at least one language');
      return;
    }

    try {
      await startProcessing({
        videoUrl,
        videoSourceType,
        targetLanguages: selectedLanguages,
      });
      toast.success('Subtitle processing started');
    } catch (err) {
      const message = getErrorMessage(err);
      toast.error(message);
    }
  };

  const handleCancelProcessing = async () => {
    try {
      await cancelProcessing();
      toast.success('Processing cancelled');
    } catch (err) {
      const message = getErrorMessage(err);
      toast.error(message);
    }
  };

  const handleRetryProcessing = async () => {
    try {
      await retryProcessing();
      toast.success('Retrying failed translations');
    } catch (err) {
      const message = getErrorMessage(err);
      toast.error(message);
    }
  };

  // Helper to extract error message from various error types
  const getErrorMessage = (err: unknown): string => {
    if (err instanceof Error) {
      // Check for axios error with response data
      const axiosError = err as { response?: { data?: { error?: string; Error?: string } } };
      if (axiosError.response?.data?.error) {
        return axiosError.response.data.error;
      }
      if (axiosError.response?.data?.Error) {
        return axiosError.response.data.Error;
      }
      return err.message;
    }
    return 'An unexpected error occurred';
  };

  const toggleLanguage = (language: string) => {
    setSelectedLanguages((prev) =>
      prev.includes(language)
        ? prev.filter((l) => l !== language)
        : [...prev, language]
    );
  };

  const getStatusIcon = (langStatus: string) => {
    switch (langStatus) {
      case 'Completed':
        return <CheckCircle className="h-4 w-4 text-green-500" />;
      case 'InProgress':
        return <Loader2 className="h-4 w-4 text-blue-500 animate-spin" />;
      case 'Failed':
        return <XCircle className="h-4 w-4 text-red-500" />;
      default:
        return <Clock className="h-4 w-4 text-gray-400" />;
    }
  };

  const getStatusBadge = (processingStatus: string) => {
    const variants: Record<
      string,
      'default' | 'secondary' | 'destructive' | 'outline'
    > = {
      Pending: 'secondary',
      Transcribing: 'default',
      Translating: 'default',
      Uploading: 'default',
      Completed: 'outline',
      Failed: 'destructive',
      Cancelled: 'secondary',
    };
    return (
      <Badge variant={variants[processingStatus] || 'secondary'}>
        {processingStatus}
      </Badge>
    );
  };

  const isProcessing =
    status && !['Completed', 'Failed', 'Cancelled'].includes(status.status);
  const canStartNew = !isProcessing;
  const showRetryButton = status && hasFailedTranslations && ['Completed', 'Failed'].includes(status.status);

  const displayedLanguages = showAllLanguages
    ? languagesData?.allSupportedLanguages || []
    : languagesData?.employeeLanguages || [];

  return (
    <Card data-testid="subtitle-processing-panel">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Languages className="h-5 w-5" />
              Subtitle Processing
            </CardTitle>
            <CardDescription>
              Automatically transcribe and translate video subtitles
            </CardDescription>
          </div>
          <div className="flex items-center gap-2">
            {isConnected ? (
              <span
                className="flex items-center gap-1 text-xs text-green-600"
                data-testid="connection-status-live"
              >
                <Wifi className="h-3 w-3" />
                Live
              </span>
            ) : (
              <span
                className="flex items-center gap-1 text-xs text-muted-foreground"
                data-testid="connection-status-polling"
              >
                <WifiOff className="h-3 w-3" />
                Polling
              </span>
            )}
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              onClick={() => refreshStatus()}
              title="Refresh status"
              data-testid="refresh-status-button"
            >
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {/* Current Status */}
        {status && (
          <div className="space-y-4" data-testid="processing-status">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Status</span>
              <span data-testid="status-badge">{getStatusBadge(status.status)}</span>
            </div>

            {isProcessing && (
              <div className="space-y-3" data-testid="processing-progress">
                <div className="flex justify-between text-sm">
                  <span data-testid="current-step">{status.currentStep}</span>
                  <span data-testid="progress-percentage">{status.overallPercentage}%</span>
                </div>
                <Progress value={status.overallPercentage} data-testid="progress-bar" />
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleCancelProcessing}
                  disabled={isCancelling}
                  className="w-full"
                  data-testid="cancel-processing-button"
                >
                  {isCancelling ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Cancelling...
                    </>
                  ) : (
                    <>
                      <StopCircle className="mr-2 h-4 w-4" />
                      Cancel Processing
                    </>
                  )}
                </Button>
                {cancelError && (
                  <p className="text-xs text-destructive" data-testid="cancel-error">
                    {getErrorMessage(cancelError)}
                  </p>
                )}
              </div>
            )}

            {/* Language Progress */}
            {status.languages.length > 0 && (
              <div className="space-y-2" data-testid="language-progress">
                <Label>Languages</Label>
                <div className="grid gap-2">
                  {status.languages.map((lang) => (
                    <div
                      key={lang.languageCode}
                      className="flex items-center justify-between p-2 bg-muted rounded-md"
                      data-testid={`language-status-${lang.languageCode}`}
                      data-status={lang.status}
                    >
                      <div className="flex items-center gap-2">
                        {getStatusIcon(lang.status)}
                        <span className="text-sm">{lang.language}</span>
                      </div>
                      <div className="flex items-center gap-2">
                        {lang.status === 'InProgress' && (
                          <span className="text-xs text-muted-foreground">
                            {lang.percentage}%
                          </span>
                        )}
                        {lang.status === 'Failed' && lang.errorMessage && (
                          <span
                            className="text-xs text-destructive"
                            title={lang.errorMessage}
                            data-testid={`language-error-${lang.languageCode}`}
                          >
                            Error
                          </span>
                        )}
                        {lang.srtUrl && (
                          <a
                            href={lang.srtUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-blue-500 hover:text-blue-700"
                            title="Download SRT file"
                            data-testid={`download-srt-${lang.languageCode}`}
                          >
                            <ExternalLink className="h-4 w-4" />
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {status.status === 'Failed' && status.errorMessage && (
              <Alert variant="destructive" data-testid="processing-error-alert">
                <AlertDescription>{status.errorMessage}</AlertDescription>
              </Alert>
            )}

            {status.status === 'Cancelled' && (
              <Alert data-testid="processing-cancelled-alert">
                <Ban className="h-4 w-4" />
                <AlertDescription>
                  Processing was cancelled.{' '}
                  {status.errorMessage || 'You can start a new processing job below.'}
                </AlertDescription>
              </Alert>
            )}

            {status.status === 'Completed' && (
              <Alert data-testid="processing-complete-alert">
                <CheckCircle className="h-4 w-4" />
                <AlertDescription>
                  Subtitle processing completed successfully.{' '}
                  {status.languages.length} subtitle file(s) generated with {status.totalSubtitles} caption(s) each.
                </AlertDescription>
              </Alert>
            )}

            {/* Retry Button for Failed Translations */}
            {showRetryButton && (
              <div className="space-y-2" data-testid="retry-section">
                <Button
                  type="button"
                  variant="outline"
                  onClick={handleRetryProcessing}
                  disabled={isRetrying}
                  className="w-full"
                  data-testid="retry-processing-button"
                >
                  {isRetrying ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Retrying...
                    </>
                  ) : (
                    <>
                      <RotateCcw className="mr-2 h-4 w-4" />
                      Retry Failed Translations
                    </>
                  )}
                </Button>
                {retryError && (
                  <p className="text-xs text-destructive" data-testid="retry-error">
                    {getErrorMessage(retryError)}
                  </p>
                )}
              </div>
            )}
          </div>
        )}

        {/* New Processing Form */}
        {canStartNew && (
          <div className="space-y-4 pt-4 border-t" data-testid="subtitle-processing-form">
            <h4 className="font-medium">
              {status ? 'Start New Processing' : 'Process Subtitles'}
            </h4>

            {/* Video URL */}
            <div className="space-y-2">
              <Label htmlFor="videoUrl">Video URL</Label>
              <Input
                id="videoUrl"
                value={videoUrl}
                onChange={(e) => setVideoUrl(e.target.value)}
                placeholder="https://drive.google.com/file/d/..."
                data-testid="video-url-input"
              />
            </div>

            {/* Video Source Type */}
            <div className="space-y-2">
              <Label>Video Source</Label>
              <Select
                value={videoSourceType}
                onValueChange={(v) =>
                  setVideoSourceType(v as SubtitleVideoSourceType)
                }
              >
                <SelectTrigger data-testid="video-source-select">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="GoogleDrive" data-testid="source-google-drive">
                    <div className="flex items-center gap-2">
                      <FileVideo className="h-4 w-4" />
                      Google Drive
                    </div>
                  </SelectItem>
                  <SelectItem value="AzureBlob" data-testid="source-azure-blob">
                    <div className="flex items-center gap-2">
                      <FileVideo className="h-4 w-4" />
                      Azure Blob Storage
                    </div>
                  </SelectItem>
                  <SelectItem value="DirectUrl" data-testid="source-direct-url">
                    <div className="flex items-center gap-2">
                      <Globe className="h-4 w-4" />
                      Direct URL
                    </div>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Language Selection */}
            <div className="space-y-2" data-testid="language-selection">
              <div className="flex items-center justify-between">
                <Label>Target Languages</Label>
                <Button
                  type="button"
                  variant="link"
                  size="sm"
                  className="h-auto p-0"
                  onClick={() => setShowAllLanguages(!showAllLanguages)}
                  data-testid="toggle-languages-button"
                >
                  {showAllLanguages
                    ? 'Show employee languages'
                    : 'Show all languages'}
                </Button>
              </div>

              {isLoadingLanguages ? (
                <div className="flex items-center justify-center py-4" data-testid="languages-loading">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <>
                  <div
                    className="grid grid-cols-2 gap-2 max-h-48 overflow-y-auto p-2 border rounded-md"
                    data-testid="language-grid"
                  >
                    {displayedLanguages.map((lang) => {
                      const employeeCount = 'employeeCount' in lang
                        ? (lang as { employeeCount: number }).employeeCount
                        : undefined;
                      return (
                        <div
                          key={lang.languageCode}
                          className="flex items-center gap-2"
                          data-testid={`language-option-${lang.languageCode}`}
                        >
                          <Checkbox
                            id={`lang-${lang.languageCode}`}
                            checked={selectedLanguages.includes(lang.language)}
                            onCheckedChange={() => toggleLanguage(lang.language)}
                            data-testid={`language-checkbox-${lang.languageCode}`}
                          />
                          <label
                            htmlFor={`lang-${lang.languageCode}`}
                            className="text-sm cursor-pointer flex-1"
                          >
                            {lang.language}
                            {employeeCount !== undefined && (
                              <span className="text-muted-foreground ml-1">
                                ({employeeCount})
                              </span>
                            )}
                          </label>
                        </div>
                      );
                    })}
                    {displayedLanguages.length === 0 && (
                      <p
                        className="col-span-2 text-sm text-muted-foreground text-center py-4"
                        data-testid="no-languages-message"
                      >
                        {showAllLanguages
                          ? 'No languages available'
                          : 'No employee languages configured'}
                      </p>
                    )}
                  </div>
                  <p className="text-xs text-muted-foreground" data-testid="selected-languages-count">
                    {selectedLanguages.length} language(s) selected
                  </p>
                </>
              )}
            </div>

            {/* Start Button */}
            <Button
              type="button"
              onClick={handleStartProcessing}
              disabled={
                !videoUrl ||
                selectedLanguages.length === 0 ||
                isStarting ||
                isLoadingLanguages
              }
              className="w-full"
              data-testid="start-processing-button"
            >
              {isStarting ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Starting...
                </>
              ) : (
                <>
                  <Play className="mr-2 h-4 w-4" />
                  Start Processing
                </>
              )}
            </Button>
          </div>
        )}

        {isLoading && !status && (
          <div className="flex items-center justify-center py-8" data-testid="panel-loading">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        )}
      </CardContent>
    </Card>
  );
}
