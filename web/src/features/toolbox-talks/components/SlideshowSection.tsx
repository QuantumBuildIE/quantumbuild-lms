'use client';

import { useToolboxTalkSlides, useSlideshowHtml } from '@/lib/api/toolbox-talks/use-my-toolbox-talks';
import { Slideshow } from './Slideshow';
import { HtmlSlideshow } from './HtmlSlideshow';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Presentation, Globe } from 'lucide-react';

interface SlideshowSectionProps {
  scheduledTalkId: string;
  languageCode?: string;
}

export function SlideshowSection({
  scheduledTalkId,
  languageCode,
}: SlideshowSectionProps) {
  // Try HTML slideshow first
  const {
    data: slideshowData,
    isLoading: isLoadingHtml,
    error: htmlError,
  } = useSlideshowHtml(scheduledTalkId, languageCode);

  // Fall back to image-based slides
  const {
    data: slidesData,
    isLoading: isLoadingSlides,
  } = useToolboxTalkSlides(
    scheduledTalkId,
    languageCode
  );
  const slides = slidesData ?? [];

  const hasHtmlSlideshow = !htmlError && slideshowData?.html;
  const isLoading = isLoadingHtml || (!hasHtmlSlideshow && isLoadingSlides);

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Presentation className="h-5 w-5" />
            Presentation
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Skeleton className="aspect-[16/9] w-full rounded-lg" />
        </CardContent>
      </Card>
    );
  }

  // Show HTML slideshow if available
  if (hasHtmlSlideshow) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Presentation className="h-5 w-5" />
            Presentation
            {slideshowData.isTranslated && (
              <Badge variant="secondary" className="text-xs gap-1">
                <Globe className="h-3 w-3" />
                {slideshowData.languageCode.toUpperCase()}
              </Badge>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <HtmlSlideshow html={slideshowData.html} />
        </CardContent>
      </Card>
    );
  }

  // Fall back to image-based slides
  if (slides.length === 0) {
    return null;
  }

  return (
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
  );
}
