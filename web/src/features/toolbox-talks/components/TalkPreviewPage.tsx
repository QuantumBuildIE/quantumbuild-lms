'use client';

import { useState, useCallback } from 'react';
import {
  AlertTriangle,
  Globe,
  BookOpen,
  HelpCircle,
  Presentation,
  PenLine,
  X,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import {
  useToolboxTalkPreview,
  useToolboxTalkPreviewSlides,
  useAdminSlideshowHtml,
} from '@/lib/api/toolbox-talks/use-toolbox-talks';
import { useLookupValues } from '@/hooks/use-lookups';
import { VideoPlayer } from './VideoPlayer';
import { HtmlSlideshow } from './HtmlSlideshow';
import { Slideshow } from './Slideshow';

interface TalkPreviewPageProps {
  talkId: string;
}

export function TalkPreviewPage({ talkId }: TalkPreviewPageProps) {
  const { data: languages = [] } = useLookupValues('Language');
  const [previewLanguage, setPreviewLanguage] = useState('en');

  const { data: preview, isLoading } = useToolboxTalkPreview(talkId, previewLanguage);
  const { data: slides } = useToolboxTalkPreviewSlides(
    talkId,
    previewLanguage,
    !!(preview?.slidesGenerated && preview.slideCount > 0)
  );
  const { data: slideshowHtml } = useAdminSlideshowHtml(
    talkId,
    previewLanguage,
    !!(preview?.hasSlideshow)
  );

  const handlePreviewProgress = useCallback(async () => {}, []);

  const sourceCode = preview?.sourceLanguageCode ?? 'en';
  const availableLanguages = [
    {
      code: sourceCode,
      name: languages.find((l) => l.code === sourceCode)?.name ?? 'English',
      isSource: true,
    },
    ...(preview?.availableTranslations ?? []).map((t) => ({
      code: t.languageCode,
      name: t.language,
      isSource: false,
    })),
  ];
  const showLanguageSelector = availableLanguages.length > 1;

  return (
    <div className="max-w-4xl mx-auto space-y-6 py-6 px-4">
      {/* Header */}
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold">
            {isLoading ? (
              <Skeleton className="h-6 w-64 inline-block" />
            ) : (
              preview?.title ?? 'Preview'
            )}
          </h1>
          {preview?.category && (
            <Badge variant="secondary">{preview.category}</Badge>
          )}
        </div>
        <div className="flex items-center gap-3">
          {showLanguageSelector && (
            <div className="flex items-center gap-2">
              <Globe className="h-4 w-4 text-muted-foreground" />
              <Select value={previewLanguage} onValueChange={setPreviewLanguage}>
                <SelectTrigger className="w-44">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {availableLanguages.map((lang) => (
                    <SelectItem key={lang.code} value={lang.code}>
                      {lang.name}
                      {lang.isSource ? ' (Source)' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
          <Button variant="ghost" size="icon" onClick={() => window.close()} title="Close preview">
            <X className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Preview mode banner */}
      <Alert className="border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30">
        <AlertTriangle className="h-4 w-4 text-amber-600" />
        <AlertDescription className="text-amber-700 dark:text-amber-400">
          Preview mode — no completion records will be saved
        </AlertDescription>
      </Alert>

      {isLoading ? (
        <PreviewSkeleton />
      ) : preview ? (
        <div className="space-y-6">
          {preview.description && (
            <p className="text-muted-foreground">{preview.description}</p>
          )}

          {/* Video */}
          {preview.videoUrl && (
            <VideoPlayer
              videoUrl={preview.videoUrl}
              videoSource={preview.videoSource}
              minimumWatchPercent={0}
              currentWatchPercent={0}
              onProgressUpdate={handlePreviewProgress}
              toolboxTalkId={talkId}
              preferredLanguageCode={previewLanguage}
            />
          )}

          {/* HTML slideshow (preferred) */}
          {slideshowHtml?.html && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Presentation className="h-5 w-5" />
                  Presentation
                  {slideshowHtml.isTranslated && (
                    <Badge variant="secondary" className="text-xs gap-1">
                      <Globe className="h-3 w-3" />
                      {slideshowHtml.languageCode.toUpperCase()}
                    </Badge>
                  )}
                </CardTitle>
              </CardHeader>
              <CardContent>
                <HtmlSlideshow html={slideshowHtml.html} />
              </CardContent>
            </Card>
          )}

          {/* Image-based slideshow fallback */}
          {!slideshowHtml?.html && slides && slides.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Presentation className="h-5 w-5" />
                  Presentation
                  <span className="text-sm font-normal text-muted-foreground">
                    ({slides.length} slides)
                  </span>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <Slideshow slides={slides} />
              </CardContent>
            </Card>
          )}

          {/* Sections */}
          {preview.sections.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <BookOpen className="h-5 w-5" />
                  Sections ({preview.sections.length})
                </CardTitle>
              </CardHeader>
              <CardContent>
                <Accordion type="multiple" defaultValue={preview.sections.map((s) => s.id)} className="w-full">
                  {preview.sections.map((section) => (
                    <AccordionItem key={section.id} value={section.id}>
                      <AccordionTrigger className="hover:no-underline">
                        <div className="flex items-center gap-3 text-left">
                          <Badge variant="outline" className="shrink-0">
                            {section.sectionNumber}
                          </Badge>
                          <span className="font-medium">{section.title}</span>
                        </div>
                      </AccordionTrigger>
                      <AccordionContent>
                        <div className="rounded-lg bg-muted/50 p-4 mt-2">
                          <div
                            className={cn(
                              'prose prose-sm max-w-none dark:prose-invert',
                              '[&>p]:mb-4 [&>ul]:mb-4 [&>ol]:mb-4',
                              '[&>h1]:text-xl [&>h2]:text-lg [&>h3]:text-base',
                              '[&>ul]:list-disc [&>ul]:pl-6',
                              '[&>ol]:list-decimal [&>ol]:pl-6',
                              '[&_table]:border-collapse [&_td]:border [&_td]:p-2 [&_th]:border [&_th]:p-2 [&_th]:bg-muted'
                            )}
                            dangerouslySetInnerHTML={{ __html: section.content }}
                          />
                        </div>
                      </AccordionContent>
                    </AccordionItem>
                  ))}
                </Accordion>
              </CardContent>
            </Card>
          )}

          {/* Quiz preview */}
          {preview.requiresQuiz && preview.questions.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <HelpCircle className="h-5 w-5" />
                  Quiz ({preview.questions.length} questions)
                </CardTitle>
                {preview.passingScore && (
                  <p className="text-sm text-muted-foreground">
                    Passing score: {preview.passingScore}%
                  </p>
                )}
              </CardHeader>
              <CardContent>
                <div className="space-y-4">
                  {preview.questions.map((question) => (
                    <div key={question.id} className="rounded-lg border p-4">
                      <div className="flex items-start gap-3">
                        <span className="flex-shrink-0 w-6 h-6 rounded-full bg-primary text-primary-foreground text-sm font-medium flex items-center justify-center">
                          {question.questionNumber}
                        </span>
                        <div className="flex-1 space-y-2">
                          <p className="font-medium">{question.questionText}</p>
                          <div className="flex items-center gap-4 text-sm text-muted-foreground">
                            <span>{question.questionTypeDisplay}</span>
                            <span>{question.points} point{question.points !== 1 ? 's' : ''}</span>
                          </div>
                          {question.questionType === 'MultipleChoice' && question.options && (
                            <div className="mt-2 space-y-2">
                              {question.options.map((option, idx) => (
                                <div key={idx} className="flex items-center gap-3 p-3 rounded-md border bg-card">
                                  <div className="h-4 w-4 rounded-full border-2 border-muted-foreground/30" />
                                  <span>{option}</span>
                                </div>
                              ))}
                            </div>
                          )}
                          {question.questionType === 'TrueFalse' && (
                            <div className="mt-2 flex gap-4">
                              {['True', 'False'].map((option) => (
                                <div key={option} className="flex items-center gap-2 px-4 py-2 rounded-md border bg-card">
                                  <div className="h-4 w-4 rounded-full border-2 border-muted-foreground/30" />
                                  <span>{option}</span>
                                </div>
                              ))}
                            </div>
                          )}
                          {question.questionType === 'ShortAnswer' && (
                            <div className="mt-2 p-3 rounded-md border bg-muted/50 text-sm text-muted-foreground italic">
                              Employee types their answer here...
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}

          {/* Disabled sign-off footer */}
          <div className="flex justify-end pt-4 border-t">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span>
                    <Button disabled>
                      <PenLine className="h-4 w-4 mr-2" />
                      Sign & Complete
                    </Button>
                  </span>
                </TooltipTrigger>
                <TooltipContent>Not available in preview</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function PreviewSkeleton() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-8 w-2/3" />
      <Skeleton className="h-4 w-full" />
      <Card>
        <CardHeader><Skeleton className="h-6 w-32" /></CardHeader>
        <CardContent>
          <div className="space-y-3">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
