'use client';

import { useState, useEffect, useCallback } from 'react';
import type { SlideDto } from '@/types/toolbox-talks';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  Play,
  Pause,
  ChevronLeft,
  ChevronRight,
  Maximize2,
  X,
} from 'lucide-react';

interface SlideshowProps {
  slides: SlideDto[];
  autoPlayInterval?: number; // ms, default 10000 (10 seconds)
  className?: string;
}

export function Slideshow({
  slides,
  autoPlayInterval = 10000,
  className,
}: SlideshowProps) {
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(true);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const currentSlide = slides[currentIndex];
  const totalSlides = slides.length;

  // Auto-advance slides
  useEffect(() => {
    if (!isPlaying || totalSlides <= 1) return;

    const timer = setInterval(() => {
      setCurrentIndex((prev) => (prev + 1) % totalSlides);
    }, autoPlayInterval);

    return () => clearInterval(timer);
  }, [isPlaying, totalSlides, autoPlayInterval, currentIndex]);

  const goToSlide = useCallback(
    (index: number) => {
      setCurrentIndex(index);
    },
    []
  );

  const goToPrevious = useCallback(() => {
    setCurrentIndex((prev) => (prev - 1 + totalSlides) % totalSlides);
  }, [totalSlides]);

  const goToNext = useCallback(() => {
    setCurrentIndex((prev) => (prev + 1) % totalSlides);
  }, [totalSlides]);

  const togglePlayPause = useCallback(() => {
    setIsPlaying((prev) => !prev);
  }, []);

  const toggleFullscreen = useCallback(() => {
    setIsFullscreen((prev) => !prev);
  }, []);

  // Keyboard navigation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft') goToPrevious();
      if (e.key === 'ArrowRight') goToNext();
      if (e.key === ' ') {
        e.preventDefault();
        togglePlayPause();
      }
      if (e.key === 'Escape' && isFullscreen) {
        setIsFullscreen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [goToPrevious, goToNext, togglePlayPause, isFullscreen]);

  if (slides.length === 0) return null;

  const slideshowContent = (
    <div
      className={cn(
        'relative bg-black rounded-lg overflow-hidden group',
        isFullscreen ? 'fixed inset-0 z-50 rounded-none' : 'aspect-[16/9]',
        className
      )}
    >
      {/* Slide Images with Ken Burns Effect */}
      <div className="absolute inset-0 overflow-hidden">
        {slides.map((slide, index) => (
          <div
            key={slide.id}
            className={cn(
              'absolute inset-0 transition-opacity duration-1000',
              index === currentIndex
                ? 'opacity-100'
                : 'opacity-0 pointer-events-none'
            )}
          >
            <img
              src={slide.imageUrl}
              alt={`Slide ${slide.pageNumber}`}
              className={cn(
                'w-full h-full object-contain',
                index === currentIndex && 'animate-ken-burns'
              )}
            />
          </div>
        ))}
      </div>

      {/* Caption Overlay */}
      {currentSlide?.text && (
        <div
          className={cn(
            'absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/90 via-black/70 to-transparent',
            isFullscreen ? 'p-8 pt-16' : 'p-4 pt-10'
          )}
        >
          <p
            className={cn(
              'text-white leading-relaxed',
              isFullscreen ? 'text-lg max-w-4xl mx-auto' : 'text-sm',
              'line-clamp-4'
            )}
          >
            {currentSlide.text}
          </p>
        </div>
      )}

      {/* Navigation Arrow Controls */}
      <div
        className={cn(
          'absolute inset-0 flex items-center justify-between px-2',
          'opacity-0 group-hover:opacity-100 transition-opacity',
          'pointer-events-none [&>*]:pointer-events-auto'
        )}
      >
        <Button
          variant="ghost"
          size="icon"
          onClick={goToPrevious}
          className="h-10 w-10 rounded-full bg-black/50 text-white hover:bg-black/70"
        >
          <ChevronLeft className="h-6 w-6" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          onClick={goToNext}
          className="h-10 w-10 rounded-full bg-black/50 text-white hover:bg-black/70"
        >
          <ChevronRight className="h-6 w-6" />
        </Button>
      </div>

      {/* Bottom Controls Bar */}
      <div
        className={cn(
          'absolute left-0 right-0 flex items-center justify-between p-3',
          'opacity-0 group-hover:opacity-100 transition-opacity',
          currentSlide?.text
            ? isFullscreen
              ? 'bottom-28'
              : 'bottom-20'
            : 'bottom-0'
        )}
      >
        {/* Play/Pause */}
        <Button
          variant="ghost"
          size="icon"
          onClick={togglePlayPause}
          className="h-8 w-8 rounded-full bg-black/50 text-white hover:bg-black/70"
        >
          {isPlaying ? (
            <Pause className="h-4 w-4" />
          ) : (
            <Play className="h-4 w-4" />
          )}
        </Button>

        {/* Dot Navigation */}
        <div className="flex items-center gap-1.5">
          {slides.map((_, index) => (
            <button
              key={index}
              onClick={() => goToSlide(index)}
              className={cn(
                'w-2 h-2 rounded-full transition-all',
                index === currentIndex
                  ? 'bg-white w-4'
                  : 'bg-white/50 hover:bg-white/75'
              )}
            />
          ))}
        </div>

        {/* Slide Counter & Fullscreen */}
        <div className="flex items-center gap-2">
          <span className="text-white/75 text-sm">
            {currentIndex + 1} / {totalSlides}
          </span>
          <Button
            variant="ghost"
            size="icon"
            onClick={toggleFullscreen}
            className="h-8 w-8 rounded-full bg-black/50 text-white hover:bg-black/70"
          >
            {isFullscreen ? (
              <X className="h-4 w-4" />
            ) : (
              <Maximize2 className="h-4 w-4" />
            )}
          </Button>
        </div>
      </div>

      {/* Progress Bar */}
      {isPlaying && totalSlides > 1 && (
        <div className="absolute top-0 left-0 right-0 h-1 bg-white/20">
          <div
            key={currentIndex}
            className="h-full bg-white/75 slideshow-progress"
            style={
              {
                '--slideshow-duration': `${autoPlayInterval}ms`,
              } as React.CSSProperties
            }
          />
        </div>
      )}
    </div>
  );

  return slideshowContent;
}
