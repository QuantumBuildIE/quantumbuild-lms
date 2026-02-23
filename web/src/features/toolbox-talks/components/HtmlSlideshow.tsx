'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import {
  Expand,
  Minimize,
  ChevronLeft,
  ChevronRight,
  Play,
  Pause,
} from 'lucide-react';
import { cn } from '@/lib/utils';

interface HtmlSlideshowProps {
  html: string;
  className?: string;
}

export function HtmlSlideshow({ html, className }: HtmlSlideshowProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const autoplayRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const [isFullscreen, setIsFullscreen] = useState(false);
  const [iframeReady, setIframeReady] = useState(false);
  const [currentSlide, setCurrentSlide] = useState(0);
  const [totalSlides, setTotalSlides] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);

  // Send a message to the iframe
  const postToIframe = useCallback((message: Record<string, unknown>) => {
    iframeRef.current?.contentWindow?.postMessage(message, '*');
  }, []);

  // Listen for messages from the iframe
  useEffect(() => {
    const handleMessage = (e: MessageEvent) => {
      const data = e.data;
      if (!data || data.type !== 'slideChanged') return;
      setCurrentSlide(data.current ?? 0);
      setTotalSlides(data.total ?? 0);
      if (!iframeReady) setIframeReady(true);
    };

    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [iframeReady]);

  // Request slide count once iframe loads
  const handleIframeLoad = useCallback(() => {
    // Give the iframe's JS time to initialise, then request state
    setTimeout(() => postToIframe({ type: 'getSlideCount' }), 600);
  }, [postToIframe]);

  // Navigation handlers
  const goNext = useCallback(() => postToIframe({ type: 'nextSlide' }), [postToIframe]);
  const goPrev = useCallback(() => postToIframe({ type: 'prevSlide' }), [postToIframe]);

  // Auto-play logic
  const stopAutoplay = useCallback(() => {
    if (autoplayRef.current) {
      clearInterval(autoplayRef.current);
      autoplayRef.current = null;
    }
    setIsPlaying(false);
  }, []);

  const startAutoplay = useCallback(() => {
    stopAutoplay();
    setIsPlaying(true);
    autoplayRef.current = setInterval(() => {
      postToIframe({ type: 'nextSlide' });
    }, 6000);
  }, [postToIframe, stopAutoplay]);

  // Stop auto-play when reaching the last slide
  useEffect(() => {
    if (isPlaying && totalSlides > 0 && currentSlide >= totalSlides - 1) {
      stopAutoplay();
    }
  }, [currentSlide, totalSlides, isPlaying, stopAutoplay]);

  // Cleanup interval on unmount
  useEffect(() => {
    return () => {
      if (autoplayRef.current) clearInterval(autoplayRef.current);
    };
  }, []);

  const toggleAutoplay = useCallback(() => {
    if (isPlaying) {
      stopAutoplay();
    } else {
      startAutoplay();
    }
  }, [isPlaying, startAutoplay, stopAutoplay]);

  // Fullscreen on the container so nav controls remain visible
  const toggleFullscreen = useCallback(() => {
    if (!document.fullscreenElement) {
      containerRef.current?.requestFullscreen();
    } else {
      document.exitFullscreen();
    }
  }, []);

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () =>
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  const isFirstSlide = currentSlide <= 0;
  const isLastSlide = totalSlides > 0 && currentSlide >= totalSlides - 1;
  const showNavigation = totalSlides > 1;

  return (
    <div
      ref={containerRef}
      className={cn(
        'relative flex flex-col',
        isFullscreen && 'bg-black',
        className
      )}
    >
      {/* Fullscreen toggle */}
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

      {/* Iframe */}
      <iframe
        ref={iframeRef}
        srcDoc={html}
        onLoad={handleIframeLoad}
        className={cn(
          'w-full border-0',
          showNavigation ? 'rounded-t-lg' : 'rounded-lg',
          isFullscreen ? 'flex-1' : 'h-[600px] md:h-[700px]'
        )}
        sandbox="allow-scripts allow-same-origin"
        title="Safety Training Slideshow"
      />

      {/* Navigation controls */}
      {showNavigation && (
        <div
          className={cn(
            'flex items-center justify-between gap-2 px-3 py-2',
            'bg-zinc-900 border-t border-zinc-800 rounded-b-lg',
            isFullscreen && 'rounded-b-none'
          )}
        >
          {/* Back */}
          <Button
            variant="ghost"
            size="sm"
            onClick={goPrev}
            disabled={!iframeReady || isFirstSlide}
            className="text-zinc-300 hover:text-white hover:bg-zinc-800 disabled:opacity-30"
          >
            <ChevronLeft className="h-4 w-4 mr-1" />
            Back
          </Button>

          {/* Center: auto-play + counter */}
          <div className="flex items-center gap-3">
            <Button
              variant="ghost"
              size="sm"
              onClick={toggleAutoplay}
              disabled={!iframeReady || (isLastSlide && !isPlaying)}
              className={cn(
                'text-zinc-300 hover:text-white hover:bg-zinc-800 disabled:opacity-30',
                isPlaying && 'bg-red-950/50 text-red-300 hover:bg-red-950/70 hover:text-red-200'
              )}
            >
              {isPlaying ? (
                <Pause className="h-4 w-4 mr-1" />
              ) : (
                <Play className="h-4 w-4 mr-1" />
              )}
              {isPlaying ? 'Pause' : 'Auto-play'}
            </Button>

            <span className="text-xs text-zinc-500 tabular-nums min-w-[70px] text-center">
              Slide {currentSlide + 1} of {totalSlides}
            </span>
          </div>

          {/* Next */}
          <Button
            variant="ghost"
            size="sm"
            onClick={goNext}
            disabled={!iframeReady || isLastSlide}
            className="text-zinc-300 hover:text-white hover:bg-zinc-800 disabled:opacity-30"
          >
            Next
            <ChevronRight className="h-4 w-4 ml-1" />
          </Button>
        </div>
      )}

      {/* Single slide indicator */}
      {totalSlides === 1 && (
        <div className="flex items-center justify-center px-3 py-2 bg-zinc-900 border-t border-zinc-800 rounded-b-lg">
          <span className="text-xs text-zinc-500">1 of 1</span>
        </div>
      )}
    </div>
  );
}
