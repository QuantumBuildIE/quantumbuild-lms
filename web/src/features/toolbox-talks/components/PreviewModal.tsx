'use client';

import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import { Slideshow } from './Slideshow';
import { HtmlSlideshow } from './HtmlSlideshow';
import {
  useToolboxTalkPreview,
  useToolboxTalkPreviewSlides,
  useAdminSlideshowHtml,
} from '@/lib/api/toolbox-talks/use-toolbox-talks';
import { SOURCE_LANGUAGE_OPTIONS } from '../constants';
import { cn } from '@/lib/utils';
import {
  Eye,
  Globe,
  Presentation,
  BookOpen,
  HelpCircle,
  CheckCircle2,
} from 'lucide-react';
import type { ToolboxTalk } from '@/types/toolbox-talks';

interface PreviewModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  talk: ToolboxTalk;
}

export function PreviewModal({ open, onOpenChange, talk }: PreviewModalProps) {
  const [previewLanguage, setPreviewLanguage] = useState(
    talk.sourceLanguageCode || 'en'
  );

  const { data: preview, isLoading } = useToolboxTalkPreview(
    talk.id,
    previewLanguage
  );

  const { data: slides } = useToolboxTalkPreviewSlides(
    talk.id,
    previewLanguage,
    open && talk.slidesGenerated && talk.slideCount > 0
  );

  const { data: slideshowHtmlData } = useAdminSlideshowHtml(
    talk.id,
    previewLanguage,
    open && (talk.hasSlideshow || !!preview?.hasSlideshow)
  );

  // Build language options: source language + available translations
  const availableLanguages = [
    {
      code: talk.sourceLanguageCode || 'en',
      name:
        SOURCE_LANGUAGE_OPTIONS.find(
          (l) => l.value === (talk.sourceLanguageCode || 'en')
        )?.label ?? 'English',
      isSource: true,
    },
    ...talk.translations.map((t) => ({
      code: t.languageCode,
      name: t.language,
      isSource: false,
    })),
  ];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center justify-between gap-4">
            <DialogTitle className="flex items-center gap-2">
              <Eye className="h-5 w-5" />
              Preview as Employee
            </DialogTitle>
            <div className="flex items-center gap-2">
              <Globe className="h-4 w-4 text-muted-foreground" />
              <Select
                value={previewLanguage}
                onValueChange={setPreviewLanguage}
              >
                <SelectTrigger className="w-48">
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
          </div>
        </DialogHeader>

        {isLoading ? (
          <PreviewSkeleton />
        ) : preview ? (
          <div className="space-y-6 py-4">
            {/* Talk Header */}
            <div>
              <h2 className="text-2xl font-bold">{preview.title}</h2>
              {preview.description && (
                <p className="text-muted-foreground mt-2">
                  {preview.description}
                </p>
              )}
              <div className="flex gap-2 mt-3">
                {preview.category && (
                  <Badge variant="secondary">{preview.category}</Badge>
                )}
                {preview.requiresQuiz && (
                  <Badge variant="outline">Quiz Required</Badge>
                )}
              </div>
            </div>

            {/* HTML Slideshow (preferred) */}
            {slideshowHtmlData?.html && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-lg">
                    <Presentation className="h-5 w-5" />
                    Presentation
                    {slideshowHtmlData.isTranslated && (
                      <Badge variant="secondary" className="text-xs gap-1">
                        <Globe className="h-3 w-3" />
                        {slideshowHtmlData.languageCode.toUpperCase()}
                      </Badge>
                    )}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <HtmlSlideshow html={slideshowHtmlData.html} />
                </CardContent>
              </Card>
            )}

            {/* Image-based slideshow fallback */}
            {!slideshowHtmlData?.html && slides && slides.length > 0 && (
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
                  <Accordion type="multiple" className="w-full">
                    {preview.sections.map((section) => (
                      <AccordionItem
                        key={section.id}
                        value={section.id}
                      >
                        <AccordionTrigger className="hover:no-underline">
                          <div className="flex items-center gap-3 text-left">
                            <Badge variant="outline" className="shrink-0">
                              {section.sectionNumber}
                            </Badge>
                            <span className="font-medium">
                              {section.title}
                            </span>
                            {section.requiresAcknowledgment && (
                              <Badge
                                variant="secondary"
                                className="text-xs"
                              >
                                <CheckCircle2 className="h-3 w-3 mr-1" />
                                Acknowledgment Required
                              </Badge>
                            )}
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
                                '[&>blockquote]:border-l-4 [&>blockquote]:border-muted [&>blockquote]:pl-4 [&>blockquote]:italic',
                                '[&_img]:max-w-full [&_img]:rounded-lg',
                                '[&_table]:border-collapse [&_td]:border [&_td]:p-2 [&_th]:border [&_th]:p-2 [&_th]:bg-muted'
                              )}
                              dangerouslySetInnerHTML={{
                                __html: section.content,
                              }}
                            />
                          </div>
                        </AccordionContent>
                      </AccordionItem>
                    ))}
                  </Accordion>
                </CardContent>
              </Card>
            )}

            {/* Quiz Questions (without correct answers) */}
            {preview.requiresQuiz && preview.questions.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-lg">
                    <HelpCircle className="h-5 w-5" />
                    Quiz Preview ({preview.questions.length} questions)
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
                      <div
                        key={question.id}
                        className="rounded-lg border p-4"
                      >
                        <div className="flex items-start gap-3">
                          <span className="flex-shrink-0 w-6 h-6 rounded-full bg-primary text-primary-foreground text-sm font-medium flex items-center justify-center">
                            {question.questionNumber}
                          </span>
                          <div className="flex-1 space-y-2">
                            <p className="font-medium">
                              {question.questionText}
                            </p>
                            <div className="flex items-center gap-4 text-sm text-muted-foreground">
                              <span>{question.questionTypeDisplay}</span>
                              <span>
                                {question.points} point
                                {question.points !== 1 ? 's' : ''}
                              </span>
                            </div>

                            {/* Multiple Choice Options */}
                            {question.questionType === 'MultipleChoice' &&
                              question.options && (
                                <div className="mt-2 space-y-2">
                                  {question.options.map((option, idx) => (
                                    <div
                                      key={idx}
                                      className="flex items-center gap-3 p-3 rounded-md border bg-card"
                                    >
                                      <div className="h-4 w-4 rounded-full border-2 border-muted-foreground/30" />
                                      <span>{option}</span>
                                    </div>
                                  ))}
                                </div>
                              )}

                            {/* True/False Options */}
                            {question.questionType === 'TrueFalse' && (
                              <div className="mt-2 flex gap-4">
                                {['True', 'False'].map((option) => (
                                  <div
                                    key={option}
                                    className="flex items-center gap-2 px-4 py-2 rounded-md border bg-card"
                                  >
                                    <div className="h-4 w-4 rounded-full border-2 border-muted-foreground/30" />
                                    <span>{option}</span>
                                  </div>
                                ))}
                              </div>
                            )}

                            {/* Short Answer */}
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
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

function PreviewSkeleton() {
  return (
    <div className="space-y-6 py-4">
      <div>
        <Skeleton className="h-8 w-2/3" />
        <Skeleton className="h-4 w-full mt-2" />
        <div className="flex gap-2 mt-3">
          <Skeleton className="h-5 w-20" />
          <Skeleton className="h-5 w-24" />
        </div>
      </div>
      <Card>
        <CardHeader>
          <Skeleton className="h-6 w-32" />
        </CardHeader>
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
