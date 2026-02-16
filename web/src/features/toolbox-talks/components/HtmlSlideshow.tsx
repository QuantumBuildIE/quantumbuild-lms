'use client';

import { useState, useEffect, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Expand, Minimize } from 'lucide-react';
import { cn } from '@/lib/utils';

interface HtmlSlideshowProps {
  html: string;
  className?: string;
}

export function HtmlSlideshow({ html, className }: HtmlSlideshowProps) {
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      iframeRef.current?.requestFullscreen();
      setIsFullscreen(true);
    } else {
      document.exitFullscreen();
      setIsFullscreen(false);
    }
  };

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () =>
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  return (
    <div className={cn('relative', className)}>
      <div className="absolute top-2 right-2 z-10">
        <Button
          variant="secondary"
          size="icon"
          onClick={toggleFullscreen}
          className="bg-black/50 hover:bg-black/70 text-white"
        >
          {isFullscreen ? (
            <Minimize className="h-4 w-4" />
          ) : (
            <Expand className="h-4 w-4" />
          )}
        </Button>
      </div>

      <iframe
        ref={iframeRef}
        srcDoc={html}
        className={cn(
          'w-full rounded-lg border-0',
          isFullscreen ? 'h-screen' : 'h-[600px] md:h-[700px]'
        )}
        sandbox="allow-scripts allow-same-origin"
        title="Safety Training Slideshow"
      />
    </div>
  );
}
