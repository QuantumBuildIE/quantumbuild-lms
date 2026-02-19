'use client';

import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Sparkles,
  Video,
  FileText,
  Loader2,
  Check,
  AlertCircle,
  Info,
  Rocket,
  RefreshCw,
  ArrowRight,
  Presentation,
  CheckCircle,
} from 'lucide-react';
import type { ToolboxTalkWizardData, GeneratedSection, GeneratedQuestion } from '../page';
import { getToolboxTalk, smartGenerateContent } from '@/lib/api/toolbox-talks/toolbox-talks';
import { getStoredToken } from '@/lib/api/client';
import { HubConnectionBuilder, HubConnection, LogLevel, HubConnectionState } from '@microsoft/signalr';
import type { SmartGenerateContentResult } from '@/types/toolbox-talks';
import { useLookupValues } from '@/hooks/use-lookups';
import { toast } from 'sonner';

interface GenerateStepProps {
  data: ToolboxTalkWizardData;
  updateData: (updates: Partial<ToolboxTalkWizardData>) => void;
  onNext: () => void;
  onBack: () => void;
}

interface GenerationProgress {
  stage: string;
  percentComplete: number;
  message: string;
}

interface GenerationComplete {
  success: boolean;
  partialSuccess?: boolean;
  sectionsGenerated: number;
  questionsGenerated: number;
  hasFinalPortionQuestion: boolean;
  errors: string[];
  warnings: string[];
  totalTokensUsed: number;
  message?: string;
}

export function GenerateStep({ data, updateData, onNext, onBack }: GenerateStepProps) {
  const { data: languages = [] } = useLookupValues('Language');

  // Local state for generation options
  const [includeVideo, setIncludeVideo] = useState(data.videoUrl ? true : false);
  const [includePdf, setIncludePdf] = useState(data.pdfUrl ? true : false);
  const [minimumSections, setMinimumSections] = useState(data.minimumSections);
  const [minimumQuestions, setMinimumQuestions] = useState(data.minimumQuestions);

  // Generation state
  const [isGenerating, setIsGenerating] = useState(false);
  const [progress, setProgress] = useState<GenerationProgress | null>(null);
  const [result, setResult] = useState<GenerationComplete | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);

  // Smart generate state
  const [isSmartGenerating, setIsSmartGenerating] = useState(false);
  const [smartResult, setSmartResult] = useState<SmartGenerateContentResult | null>(null);

  const connectionRef = useRef<HubConnection | null>(null);
  const hasConnectedRef = useRef(false);
  const lastProgressTimeRef = useRef<number>(Date.now());

  const hasVideo = !!data.videoUrl;
  const hasPdf = !!data.pdfUrl;
  const hasContent = hasVideo || hasPdf;

  // Check if content actually exists (regardless of generation status)
  const hasGeneratedContent = useMemo(() => {
    const hasSections = data.sections && data.sections.length > 0;
    const hasQuestions = data.questions && data.questions.length > 0;
    return hasSections || hasQuestions;
  }, [data.sections, data.questions]);

  // Can proceed if generation succeeded OR if content actually exists
  const canProceed = !isGenerating && !isSmartGenerating && (result?.success || smartResult?.sectionsCopied || hasGeneratedContent);

  // Helper to map API response to wizard state
  const mapTalkToWizardData = useCallback((updatedTalk: Awaited<ReturnType<typeof getToolboxTalk>>) => {
    const sections: GeneratedSection[] = updatedTalk.sections.map((s) => ({
      id: s.id,
      sortOrder: s.sectionNumber,
      title: s.title,
      content: s.content,
      source: s.source || 'Manual',
      requiresAcknowledgment: s.requiresAcknowledgment,
    }));

    const questions: GeneratedQuestion[] = updatedTalk.questions.map((q) => {
      const correctAnswerIndex = q.options?.findIndex(opt => opt === q.correctAnswer) ?? 0;
      const mapSource = (src: string | undefined): 'Video' | 'Pdf' | 'Manual' => {
        if (src === 'Video' || src === 'Pdf' || src === 'Manual') return src;
        if (src === 'Both') return 'Video';
        return 'Manual';
      };

      return {
        id: q.id,
        sortOrder: q.questionNumber,
        questionText: q.questionText,
        questionType: q.questionType === 'TrueFalse' ? 'TrueFalse' as const : 'MultipleChoice' as const,
        options: q.options || [],
        correctAnswerIndex: correctAnswerIndex >= 0 ? correctAnswerIndex : 0,
        source: mapSource(q.source),
        points: q.points,
      };
    });

    updateData({
      sections,
      questions,
      generateFromVideo: includeVideo,
      generateFromPdf: includePdf,
      minimumSections,
      minimumQuestions,
    });

    return { sections, questions };
  }, [includeVideo, includePdf, minimumSections, minimumQuestions, updateData]);

  // Set up SignalR connection
  const setupSignalR = useCallback(async () => {
    if (!data.id || hasConnectedRef.current || connectionRef.current?.state === HubConnectionState.Connected) {
      return;
    }

    setIsConnecting(true);

    try {
      const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222';
      const token = getStoredToken('accessToken');

      if (!token) {
        setError('Authentication token not found. Please log in again.');
        setIsConnecting(false);
        return;
      }

      const connection = new HubConnectionBuilder()
        .withUrl(`${apiUrl}/hubs/content-generation`, {
          accessTokenFactory: () => token,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Information)
        .build();

      // Handle progress updates
      connection.on('ContentGenerationProgress', (payload: {
        toolboxTalkId: string;
        stage: string;
        percentComplete: number;
        message: string;
      }) => {
        if (payload.toolboxTalkId === data.id) {
          lastProgressTimeRef.current = Date.now();
          setProgress({
            stage: payload.stage,
            percentComplete: payload.percentComplete,
            message: payload.message,
          });
        }
      });

      // Handle completion
      connection.on('ContentGenerationComplete', async (payload: {
        toolboxTalkId: string;
        success: boolean;
        partialSuccess?: boolean;
        sectionsGenerated: number;
        questionsGenerated: number;
        hasFinalPortionQuestion: boolean;
        errors: string[];
        warnings: string[];
        totalTokensUsed: number;
        message?: string;
      }) => {
        if (payload.toolboxTalkId === data.id) {
          setResult({
            success: payload.success,
            partialSuccess: payload.partialSuccess,
            sectionsGenerated: payload.sectionsGenerated,
            questionsGenerated: payload.questionsGenerated,
            hasFinalPortionQuestion: payload.hasFinalPortionQuestion,
            errors: payload.errors,
            warnings: payload.warnings,
            totalTokensUsed: payload.totalTokensUsed,
            message: payload.message,
          });

          // Fetch and map content
          if (data.id) {
            try {
              const updatedTalk = await getToolboxTalk(data.id);
              const hasContent = (updatedTalk.sections?.length ?? 0) > 0 ||
                                 (updatedTalk.questions?.length ?? 0) > 0;
              if (hasContent) {
                mapTalkToWizardData(updatedTalk);
              }
            } catch (error) {
              console.error('Failed to fetch generated content:', error);
            }
          }

          setIsGenerating(false);
        }
      });

      connection.onreconnecting(() => {
        setIsConnected(false);
      });

      connection.onreconnected(() => {
        setIsConnected(true);
        if (data.id) {
          connection.invoke('SubscribeToToolboxTalk', data.id);
        }
      });

      connection.onclose(() => {
        setIsConnected(false);
        hasConnectedRef.current = false;
      });

      await connection.start();
      await connection.invoke('SubscribeToToolboxTalk', data.id);

      connectionRef.current = connection;
      hasConnectedRef.current = true;
      setIsConnected(true);
      setError(null);
    } catch (err) {
      console.error('Failed to connect to SignalR:', err);
      setError('Failed to establish real-time connection. You can still start generation.');
    } finally {
      setIsConnecting(false);
    }
  }, [data.id, mapTalkToWizardData]);

  // Connect to SignalR when component mounts
  useEffect(() => {
    if (data.id && !hasConnectedRef.current) {
      setupSignalR();
    }

    return () => {
      if (connectionRef.current) {
        if (data.id) {
          connectionRef.current.invoke('UnsubscribeFromToolboxTalk', data.id).catch(() => {});
        }
        connectionRef.current.stop();
        connectionRef.current = null;
        hasConnectedRef.current = false;
      }
    };
  }, [data.id, setupSignalR]);

  // Timeout fallback: check status via API if no SignalR updates
  useEffect(() => {
    if (!isGenerating || result) return;

    const checkInterval = setInterval(async () => {
      const timeSinceLastProgress = Date.now() - lastProgressTimeRef.current;

      if (timeSinceLastProgress > 45000 && data.id) {
        try {
          const updatedTalk = await getToolboxTalk(data.id);

          if (updatedTalk.sections?.length > 0 || updatedTalk.questions?.length > 0) {
            setResult({
              success: true,
              partialSuccess: true,
              sectionsGenerated: updatedTalk.sections?.length || 0,
              questionsGenerated: updatedTalk.questions?.length || 0,
              hasFinalPortionQuestion: false,
              errors: [],
              warnings: ['Progress updates were delayed, but content was generated successfully'],
              totalTokensUsed: 0,
              message: 'Content generated (status recovered)',
            });

            mapTalkToWizardData(updatedTalk);
            setIsGenerating(false);
          }
        } catch (error) {
          console.error('Status check failed:', error);
        }
      }
    }, 15000);

    return () => clearInterval(checkInterval);
  }, [isGenerating, result, data.id, mapTalkToWizardData]);

  // Smart generate: auto-reuse existing content, generate only what's missing
  const handleSmartGenerate = async () => {
    if (!data.id) {
      setError('Learning not found. Please go back and save the basic info first.');
      return;
    }

    if (!includeVideo && !includePdf) {
      setError('Please select at least one content source');
      return;
    }

    setIsSmartGenerating(true);
    setError(null);
    setResult(null);
    setSmartResult(null);

    try {
      // Ensure SignalR is ready (needed if AI generation is queued)
      let currentConnection = connectionRef.current;

      if (!currentConnection || currentConnection.state !== HubConnectionState.Connected) {
        if (currentConnection && currentConnection.state !== HubConnectionState.Disconnected) {
          try { await currentConnection.stop(); } catch { /* ignore */ }
        }

        const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222';
        const token = getStoredToken('accessToken');

        if (!token) {
          throw new Error('Authentication token not found. Please log in again.');
        }

        currentConnection = new HubConnectionBuilder()
          .withUrl(`${apiUrl}/hubs/content-generation`, {
            accessTokenFactory: () => token,
          })
          .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
          .configureLogging(LogLevel.Information)
          .build();

        currentConnection.on('ContentGenerationProgress', (payload: {
          toolboxTalkId: string;
          stage: string;
          percentComplete: number;
          message: string;
        }) => {
          if (payload.toolboxTalkId === data.id) {
            lastProgressTimeRef.current = Date.now();
            setProgress({
              stage: payload.stage,
              percentComplete: payload.percentComplete,
              message: payload.message,
            });
          }
        });

        currentConnection.on('ContentGenerationComplete', async (payload: {
          toolboxTalkId: string;
          success: boolean;
          partialSuccess?: boolean;
          sectionsGenerated: number;
          questionsGenerated: number;
          hasFinalPortionQuestion: boolean;
          errors: string[];
          warnings: string[];
          totalTokensUsed: number;
          message?: string;
        }) => {
          if (payload.toolboxTalkId === data.id) {
            setResult({
              success: payload.success,
              partialSuccess: payload.partialSuccess,
              sectionsGenerated: payload.sectionsGenerated,
              questionsGenerated: payload.questionsGenerated,
              hasFinalPortionQuestion: payload.hasFinalPortionQuestion,
              errors: payload.errors,
              warnings: payload.warnings,
              totalTokensUsed: payload.totalTokensUsed,
              message: payload.message,
            });

            if (data.id) {
              try {
                const updatedTalk = await getToolboxTalk(data.id);
                const hasContent = (updatedTalk.sections?.length ?? 0) > 0 ||
                                   (updatedTalk.questions?.length ?? 0) > 0;
                if (hasContent) {
                  mapTalkToWizardData(updatedTalk);
                }
              } catch (error) {
                console.error('Failed to fetch generated content:', error);
              }
            }

            setIsGenerating(false);
          }
        });

        currentConnection.onreconnecting(() => setIsConnected(false));
        currentConnection.onreconnected(() => {
          setIsConnected(true);
          if (data.id) currentConnection?.invoke('SubscribeToToolboxTalk', data.id);
        });
        currentConnection.onclose(() => {
          setIsConnected(false);
          hasConnectedRef.current = false;
        });

        await currentConnection.start();
        connectionRef.current = currentConnection;
        hasConnectedRef.current = true;
        setIsConnected(true);
      }

      await currentConnection.invoke('SubscribeToToolboxTalk', data.id);

      // Call smart generate endpoint
      const smartRes = await smartGenerateContent(data.id, {
        generateSections: true,
        generateQuestions: true,
        generateSlideshow: data.slideshowSource !== 'none',
        slideshowSource: data.slideshowSource,
        includeVideo,
        includePdf,
        sourceLanguageCode: data.sourceLanguageCode,
        minimumSections,
        minimumQuestions,
        passThreshold: data.passThreshold,
        connectionId: currentConnection.connectionId || '',
      });

      setSmartResult(smartRes);

      // If content was copied, fetch it to update wizard state
      if (smartRes.sectionsCopied > 0 || smartRes.questionsCopied > 0) {
        const updatedTalk = await getToolboxTalk(data.id);
        mapTalkToWizardData(updatedTalk);
      }

      if (smartRes.generationJobQueued) {
        // AI generation was queued - switch to progress tracking mode
        setIsSmartGenerating(false);
        setIsGenerating(true);
        lastProgressTimeRef.current = Date.now();
        setProgress({
          stage: 'Starting',
          percentComplete: 5,
          message: smartRes.contentCopiedFromTitle
            ? `Reused content from "${smartRes.contentCopiedFromTitle}". Generating remaining content...`
            : 'Starting content generation...',
        });
      } else {
        // Everything was copied - no AI generation needed!
        setIsSmartGenerating(false);

        if (smartRes.contentCopiedFromTitle) {
          toast.success('Content ready!', {
            description: `Reused from "${smartRes.contentCopiedFromTitle}" - no AI credits used.`,
          });
        }
      }
    } catch (err: unknown) {
      console.error('Smart generate error:', err);
      const errorMessage = err instanceof Error ? err.message : 'Failed to generate content';
      setError(errorMessage);
      setIsSmartGenerating(false);
    }
  };

  const handleNext = () => {
    updateData({
      generateFromVideo: includeVideo,
      generateFromPdf: includePdf,
      minimumSections,
      minimumQuestions,
    });
    onNext();
  };

  const handleRetry = () => {
    setResult(null);
    setSmartResult(null);
    setProgress(null);
    setError(null);
  };

  const getStageIcon = (stage: string) => {
    switch (stage) {
      case 'Starting':
        return <Loader2 className="h-4 w-4 animate-spin" />;
      case 'Extracting':
        return <FileText className="h-4 w-4" />;
      case 'GeneratingSections':
        return <Sparkles className="h-4 w-4 animate-pulse" />;
      case 'GeneratingQuiz':
        return <Sparkles className="h-4 w-4 animate-pulse" />;
      case 'Saving':
        return <Loader2 className="h-4 w-4 animate-spin" />;
      case 'Complete':
        return <Check className="h-4 w-4 text-green-600" />;
      default:
        return <Loader2 className="h-4 w-4 animate-spin" />;
    }
  };

  const getStageName = (stage: string) => {
    switch (stage) {
      case 'Starting':
        return 'Initializing';
      case 'Extracting':
        return 'Extracting Content';
      case 'GeneratingSections':
        return 'Generating Sections';
      case 'GeneratingQuiz':
        return 'Generating Quiz';
      case 'Saving':
        return 'Saving Results';
      case 'Complete':
        return 'Complete';
      default:
        return stage;
    }
  };

  // Determine if we should show the smart result summary (content was reused without needing AI)
  const showSmartResult = smartResult && !smartResult.generationJobQueued && !result;

  // Determine if we should show the combined result (after AI generation completes + reuse info)
  const showCombinedResult = result && smartResult?.contentCopiedFromTitle;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-lg font-semibold">Generate Content</h2>
        <p className="text-sm text-muted-foreground">
          Use AI to generate sections and quiz questions from your content
        </p>
      </div>

      {/* No content warning */}
      {!hasContent && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>No Content Available</AlertTitle>
          <AlertDescription>
            Please go back and add a video or PDF to generate content from.
          </AlertDescription>
        </Alert>
      )}

      {/* Connection status indicator */}
      {data.id && !isGenerating && !isSmartGenerating && !result && !showSmartResult && (
        <div className="flex items-center gap-2 text-sm">
          {isConnecting ? (
            <>
              <Loader2 className="h-3 w-3 animate-spin text-muted-foreground" />
              <span className="text-muted-foreground">Connecting to real-time updates...</span>
            </>
          ) : isConnected ? (
            <>
              <div className="h-2 w-2 rounded-full bg-green-500" />
              <span className="text-muted-foreground">Real-time updates connected</span>
            </>
          ) : (
            <>
              <div className="h-2 w-2 rounded-full bg-yellow-500" />
              <span className="text-muted-foreground">
                Real-time updates unavailable
                <Button variant="link" size="sm" className="p-0 ml-1 h-auto" onClick={setupSignalR}>
                  Retry
                </Button>
              </span>
            </>
          )}
        </div>
      )}

      {/* Source Selection - only show when not generating and no result */}
      {!isGenerating && !isSmartGenerating && !result && !showSmartResult && hasContent && (
        <Card>
          <CardContent className="pt-6 space-y-6">
            {/* Section 1: Content Generation */}
            <div className="space-y-4">
              <div>
                <h3 className="font-medium">Content Generation</h3>
                <p className="text-sm text-muted-foreground">
                  AI will generate training sections and quiz questions from your selected sources
                </p>
              </div>

              <div className="space-y-3">
                <div className="flex items-start space-x-3">
                  <Checkbox
                    id="generateFromVideo"
                    checked={includeVideo}
                    onCheckedChange={(checked) => setIncludeVideo(checked === true)}
                    disabled={!hasVideo}
                  />
                  <div className="space-y-0.5">
                    <Label
                      htmlFor="generateFromVideo"
                      className={!hasVideo ? 'text-muted-foreground' : ''}
                    >
                      Use video transcript
                    </Label>
                    {!hasVideo && (
                      <p className="text-xs text-muted-foreground">Upload a video first</p>
                    )}
                  </div>
                </div>

                <div className="flex items-start space-x-3">
                  <Checkbox
                    id="generateFromPdf"
                    checked={includePdf}
                    onCheckedChange={(checked) => setIncludePdf(checked === true)}
                    disabled={!hasPdf}
                  />
                  <div className="space-y-0.5">
                    <Label
                      htmlFor="generateFromPdf"
                      className={!hasPdf ? 'text-muted-foreground' : ''}
                    >
                      Use PDF content
                    </Label>
                    {!hasPdf && (
                      <p className="text-xs text-muted-foreground">Upload a PDF first</p>
                    )}
                  </div>
                </div>
              </div>
            </div>

            {/* Section 2: Animated Slideshow */}
            <div className="space-y-4 pt-4 border-t">
              <div>
                <h3 className="font-medium flex items-center gap-2">
                  <Presentation className="h-4 w-4" />
                  Animated Slideshow
                </h3>
                <p className="text-sm text-muted-foreground">
                  Optionally generate a visual slideshow from one of your content sources
                </p>
              </div>

              <RadioGroup
                value={data.slideshowSource}
                onValueChange={(value) => updateData({ slideshowSource: value as 'none' | 'pdf' | 'video' })}
              >
                <div className="flex items-center space-x-3">
                  <RadioGroupItem value="none" id="slideshow-none" />
                  <Label htmlFor="slideshow-none">No slideshow</Label>
                </div>

                <div className="flex items-start space-x-3">
                  <RadioGroupItem value="pdf" id="slideshow-pdf" disabled={!hasPdf} />
                  <div className="space-y-0.5">
                    <Label
                      htmlFor="slideshow-pdf"
                      className={!hasPdf ? 'text-muted-foreground' : ''}
                    >
                      Generate from PDF
                    </Label>
                    {!hasPdf && (
                      <p className="text-xs text-muted-foreground">Upload a PDF first</p>
                    )}
                  </div>
                </div>

                <div className="flex items-start space-x-3">
                  <RadioGroupItem value="video" id="slideshow-video" disabled={!hasVideo} />
                  <div className="space-y-0.5">
                    <Label
                      htmlFor="slideshow-video"
                      className={!hasVideo ? 'text-muted-foreground' : ''}
                    >
                      Generate from video transcript
                    </Label>
                    {!hasVideo && (
                      <p className="text-xs text-muted-foreground">Upload a video first</p>
                    )}
                  </div>
                </div>
              </RadioGroup>
            </div>

            {/* Source Language */}
            <div className="space-y-4 pt-4 border-t">
              <h3 className="font-medium">Original Language</h3>
              <div className="space-y-2">
                <Select
                  value={data.sourceLanguageCode}
                  onValueChange={(value) => updateData({ sourceLanguageCode: value })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select language" />
                  </SelectTrigger>
                  <SelectContent>
                    {languages.map((lang) => (
                      <SelectItem key={lang.code} value={lang.code}>
                        {lang.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-sm text-muted-foreground">
                  The language of your video/PDF content. This helps AI understand and
                  process the content correctly, and determines which languages to translate to.
                </p>
              </div>
            </div>

            {/* Generation Settings */}
            <div className="space-y-4 pt-4 border-t">
              <h3 className="font-medium">Generation Settings</h3>

              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="minimumSections">Minimum Sections</Label>
                  <Input
                    id="minimumSections"
                    type="number"
                    min={3}
                    max={20}
                    value={minimumSections}
                    onChange={(e) => setMinimumSections(parseInt(e.target.value) || 7)}
                  />
                  <p className="text-xs text-muted-foreground">
                    AI will generate at least this many training sections
                  </p>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="minimumQuestions">Minimum Quiz Questions</Label>
                  <Input
                    id="minimumQuestions"
                    type="number"
                    min={3}
                    max={15}
                    value={minimumQuestions}
                    onChange={(e) => setMinimumQuestions(parseInt(e.target.value) || 5)}
                  />
                  <p className="text-xs text-muted-foreground">
                    AI will generate at least this many quiz questions
                  </p>
                </div>
              </div>
            </div>

            {/* Final portion question info */}
            {includeVideo && (
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  At least one quiz question will be generated from the final portion
                  of the video (80-100%) to ensure employees watch the entire content.
                </AlertDescription>
              </Alert>
            )}
          </CardContent>
        </Card>
      )}

      {/* Generation Progress (AI generation running in background) */}
      {isGenerating && progress && (
        <Card>
          <CardContent className="pt-6">
            <div className="space-y-4">
              {/* Show reuse summary above progress if content was partially copied */}
              {smartResult?.contentCopiedFromTitle && (smartResult.sectionsCopied > 0 || smartResult.questionsCopied > 0 || smartResult.slideshowCopied) && (
                <Alert className="bg-green-50 border-green-200">
                  <CheckCircle className="h-4 w-4 text-green-600" />
                  <AlertDescription className="text-green-700">
                    Reused {smartResult.sectionsCopied > 0 ? `${smartResult.sectionsCopied} sections` : ''}
                    {smartResult.sectionsCopied > 0 && smartResult.questionsCopied > 0 ? ', ' : ''}
                    {smartResult.questionsCopied > 0 ? `${smartResult.questionsCopied} questions` : ''}
                    {smartResult.slideshowCopied ? ', slideshow' : ''}
                    {' '}from &quot;{smartResult.contentCopiedFromTitle}&quot;. Generating remaining content...
                  </AlertDescription>
                </Alert>
              )}

              {/* Stage indicator */}
              <div className="flex items-center gap-3">
                {getStageIcon(progress.stage)}
                <div className="flex-1">
                  <p className="font-medium">{getStageName(progress.stage)}</p>
                  <p className="text-sm text-muted-foreground">{progress.message}</p>
                </div>
                <span className="text-sm font-medium text-muted-foreground">
                  {progress.percentComplete}%
                </span>
              </div>

              {/* Progress bar */}
              <Progress value={progress.percentComplete} className="h-2" />

              {/* Stages indicator */}
              <div className="flex justify-between text-xs text-muted-foreground">
                <span className={progress.percentComplete >= 10 ? 'text-primary' : ''}>Extract</span>
                <span className={progress.percentComplete >= 35 ? 'text-primary' : ''}>Sections</span>
                <span className={progress.percentComplete >= 65 ? 'text-primary' : ''}>Quiz</span>
                <span className={progress.percentComplete >= 88 ? 'text-primary' : ''}>Save</span>
              </div>

              {/* Info message */}
              <div className="text-center text-sm text-muted-foreground pt-2 border-t">
                <p>This process usually takes 1-2 minutes.</p>
                <p className="mt-1">
                  You can leave this page - generation will continue in the background.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Smart Result - Content was fully reused (no AI generation needed) */}
      {showSmartResult && smartResult && (
        <Card className="border-green-200 bg-green-50/50">
          <CardContent className="pt-6">
            <div className="space-y-4">
              <div className="flex items-start gap-3">
                <CheckCircle className="h-5 w-5 text-green-600 mt-0.5" />
                <div className="space-y-2">
                  <p className="font-medium text-green-800">Content ready!</p>

                  {smartResult.contentCopiedFromTitle && (
                    <p className="text-sm text-green-700">
                      Content was reused from &quot;{smartResult.contentCopiedFromTitle}&quot; &mdash; saving AI costs!
                    </p>
                  )}

                  <ul className="text-sm text-green-700 space-y-1">
                    {/* Sections */}
                    {smartResult.sectionsCopied > 0 && (
                      <li>
                        &bull; {smartResult.sectionsCopied} sections
                        <span className="text-green-600"> (reused)</span>
                      </li>
                    )}

                    {/* Questions */}
                    {smartResult.questionsCopied > 0 && (
                      <li>
                        &bull; {smartResult.questionsCopied} quiz questions
                        <span className="text-green-600"> (reused)</span>
                      </li>
                    )}

                    {/* Slideshow */}
                    {smartResult.slideshowCopied && (
                      <li>
                        &bull; Animated slideshow
                        <span className="text-green-600"> (reused)</span>
                      </li>
                    )}

                    {/* Translations */}
                    {smartResult.translationsCopied > 0 && (
                      <li>
                        &bull; {smartResult.translationsCopied} translation(s)
                        <span className="text-green-600"> (reused)</span>
                      </li>
                    )}
                  </ul>

                  {smartResult.translationsCopied > 0 && (
                    <p className="text-xs text-muted-foreground mt-2">
                      If employees speak languages not covered by the reused translations,
                      additional translations will be generated automatically in the background.
                    </p>
                  )}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Generation Result (after AI generation completes) */}
      {result && (
        <Card className={
          result.success && !result.partialSuccess
            ? 'border-green-200 bg-green-50/50'
            : result.success && result.partialSuccess
            ? 'border-yellow-200 bg-yellow-50/50'
            : 'border-red-200 bg-red-50/50'
        }>
          <CardContent className="pt-6">
            {result.success ? (
              <div className="space-y-4">
                {/* Success header */}
                <div className={`flex items-center gap-2 ${result.partialSuccess ? 'text-yellow-700' : 'text-green-700'}`}>
                  {result.partialSuccess ? (
                    <Info className="h-5 w-5" />
                  ) : (
                    <Check className="h-5 w-5" />
                  )}
                  <span className="font-medium">
                    {result.partialSuccess
                      ? 'Content Generated with Some Limitations'
                      : 'Content Generated Successfully!'}
                  </span>
                </div>

                {/* Show combined summary if content was reused + generated */}
                {showCombinedResult && smartResult && (
                  <p className="text-sm text-green-700">
                    Some content was reused from &quot;{smartResult.contentCopiedFromTitle}&quot; &mdash; saving AI costs!
                  </p>
                )}

                {result.partialSuccess && !showCombinedResult && (
                  <p className="text-sm text-yellow-700">
                    Some content sources were unavailable, but content was successfully generated from available sources.
                  </p>
                )}

                {/* Smart summary: copied vs generated */}
                {showCombinedResult && smartResult ? (
                  <ul className="text-sm text-green-700 space-y-1">
                    {(smartResult.sectionsCopied > 0 || result.sectionsGenerated > 0) && (
                      <li>
                        &bull; {smartResult.sectionsCopied + result.sectionsGenerated} sections
                        {smartResult.sectionsCopied > 0 && result.sectionsGenerated > 0 && (
                          <span className="text-green-600">
                            {' '}({smartResult.sectionsCopied} reused, {result.sectionsGenerated} new)
                          </span>
                        )}
                        {smartResult.sectionsCopied > 0 && result.sectionsGenerated === 0 && (
                          <span className="text-green-600"> (reused)</span>
                        )}
                      </li>
                    )}
                    {(smartResult.questionsCopied > 0 || result.questionsGenerated > 0) && (
                      <li>
                        &bull; {smartResult.questionsCopied + result.questionsGenerated} quiz questions
                        {smartResult.questionsCopied > 0 && result.questionsGenerated > 0 && (
                          <span className="text-green-600">
                            {' '}({smartResult.questionsCopied} reused, {result.questionsGenerated} new)
                          </span>
                        )}
                        {smartResult.questionsCopied > 0 && result.questionsGenerated === 0 && (
                          <span className="text-green-600"> (reused)</span>
                        )}
                      </li>
                    )}
                    {(smartResult.slideshowCopied || smartResult.slideshowGenerated) && (
                      <li>
                        &bull; Animated slideshow
                        {smartResult.slideshowCopied && <span className="text-green-600"> (reused)</span>}
                      </li>
                    )}
                  </ul>
                ) : (
                  /* Standard stats (no reuse) */
                  <div className="grid grid-cols-2 gap-4">
                    <div className="p-4 bg-white rounded-lg border">
                      <p className="text-3xl font-bold text-primary">{result.sectionsGenerated}</p>
                      <p className="text-sm text-muted-foreground">Sections created</p>
                    </div>
                    <div className="p-4 bg-white rounded-lg border">
                      <p className="text-3xl font-bold text-primary">{result.questionsGenerated}</p>
                      <p className="text-sm text-muted-foreground">Quiz questions</p>
                    </div>
                  </div>
                )}

                {/* Final portion question badge */}
                {result.hasFinalPortionQuestion && (
                  <Alert className="bg-green-100 border-green-200">
                    <Check className="h-4 w-4 text-green-700" />
                    <AlertDescription className="text-green-700">
                      Includes a question from the video&apos;s final portion (80-100%) to ensure full viewing
                    </AlertDescription>
                  </Alert>
                )}

                {/* Warnings */}
                {result.warnings.length > 0 && (
                  <Alert className="bg-yellow-50 border-yellow-200">
                    <Info className="h-4 w-4 text-yellow-700" />
                    <AlertTitle className="text-yellow-800">
                      {result.partialSuccess ? 'Source Limitations' : 'Notes'}
                    </AlertTitle>
                    <AlertDescription className="text-yellow-700">
                      <ul className="list-disc list-inside space-y-1 mt-2">
                        {result.warnings.map((warning, i) => (
                          <li key={i}>{warning}</li>
                        ))}
                      </ul>
                    </AlertDescription>
                  </Alert>
                )}
              </div>
            ) : (
              <div className="space-y-4">
                {/* Failure header */}
                <div className="flex items-center gap-2 text-red-700">
                  <AlertCircle className="h-5 w-5" />
                  <span className="font-medium">
                    {result.message || 'Content Generation Failed'}
                  </span>
                </div>

                {/* Errors */}
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    <ul className="list-disc list-inside space-y-1">
                      {result.errors.map((err, i) => (
                        <li key={i}>{err}</li>
                      ))}
                    </ul>
                  </AlertDescription>
                </Alert>

                {/* Show helpful message if content exists despite "failure" */}
                {hasGeneratedContent && (
                  <Alert className="bg-blue-50 border-blue-200">
                    <Info className="h-4 w-4 text-blue-600" />
                    <AlertTitle className="text-blue-800">Content was partially generated</AlertTitle>
                    <AlertDescription className="text-blue-700">
                      <p className="mt-1">
                        Some content sources weren&apos;t available, but {data.sections?.length || 0} sections
                        and {data.questions?.length || 0} quiz questions were created.
                        You can continue to review and edit the content.
                      </p>
                    </AlertDescription>
                  </Alert>
                )}

                {/* Retry button */}
                <Button onClick={handleRetry} variant="outline" className="gap-2">
                  <RefreshCw className="h-4 w-4" />
                  Try Again
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Error alert */}
      {error && !result && !showSmartResult && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Action buttons */}
      <div className="flex justify-between pt-4 border-t">
        <Button
          type="button"
          variant="outline"
          onClick={onBack}
          disabled={isGenerating || isSmartGenerating}
        >
          Back
        </Button>

        <div className="flex gap-2">
          {/* Show generate button when ready */}
          {!isGenerating && !isSmartGenerating && !result && !showSmartResult && hasContent && (
            <Button
              onClick={handleSmartGenerate}
              disabled={!includeVideo && !includePdf}
              className="gap-2"
            >
              <Rocket className="h-4 w-4" />
              Generate Sections &amp; Quiz
            </Button>
          )}

          {/* Show next button after successful generation OR if content exists */}
          {canProceed && (
            <Button onClick={handleNext} className="gap-2">
              {(result?.success || showSmartResult) ? 'Next: Review Content' : 'Continue to Review'}
              <ArrowRight className="h-4 w-4" />
            </Button>
          )}

          {/* Show smart generating state */}
          {isSmartGenerating && (
            <Button disabled className="gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              Analyzing content...
            </Button>
          )}

          {/* Show generating state */}
          {isGenerating && !isSmartGenerating && (
            <Button disabled className="gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              Generating...
            </Button>
          )}

          {/* Skip button when no content sources available */}
          {!hasContent && !hasGeneratedContent && (
            <Button onClick={onNext} variant="outline">
              Skip to Review
            </Button>
          )}

          {/* Skip & Add Manually button when generation failed and no content exists */}
          {result && !result.success && !hasGeneratedContent && (
            <Button onClick={onNext} variant="ghost" className="gap-2">
              Skip &amp; Add Manually
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
