'use client';

import * as React from 'react';
import { Eraser, Check, Loader2, PenLine } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';

interface SignatureCaptureProps {
  onComplete: (signatureData: string, signedByName: string) => Promise<void>;
  defaultName?: string;
  className?: string;
}

export function SignatureCapture({ onComplete, defaultName = '', className }: SignatureCaptureProps) {
  const canvasRef = React.useRef<HTMLCanvasElement>(null);
  const [isDrawing, setIsDrawing] = React.useState(false);
  const [hasSignature, setHasSignature] = React.useState(false);
  const [signedByName, setSignedByName] = React.useState(defaultName);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  // Initialize canvas
  React.useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Set canvas size
    const rect = canvas.getBoundingClientRect();
    canvas.width = rect.width * window.devicePixelRatio;
    canvas.height = rect.height * window.devicePixelRatio;
    ctx.scale(window.devicePixelRatio, window.devicePixelRatio);

    // Set drawing styles
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    // Fill background
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, rect.width, rect.height);
  }, []);

  // Get coordinates from event
  const getCoordinates = (
    e: React.MouseEvent<HTMLCanvasElement> | React.TouchEvent<HTMLCanvasElement>
  ): { x: number; y: number } | null => {
    const canvas = canvasRef.current;
    if (!canvas) return null;

    const rect = canvas.getBoundingClientRect();

    if ('touches' in e) {
      const touch = e.touches[0];
      return {
        x: touch.clientX - rect.left,
        y: touch.clientY - rect.top,
      };
    }

    return {
      x: e.clientX - rect.left,
      y: e.clientY - rect.top,
    };
  };

  const startDrawing = (
    e: React.MouseEvent<HTMLCanvasElement> | React.TouchEvent<HTMLCanvasElement>
  ) => {
    e.preventDefault();
    const coords = getCoordinates(e);
    if (!coords) return;

    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!ctx) return;

    ctx.beginPath();
    ctx.moveTo(coords.x, coords.y);
    setIsDrawing(true);
    setHasSignature(true);
  };

  const draw = (e: React.MouseEvent<HTMLCanvasElement> | React.TouchEvent<HTMLCanvasElement>) => {
    e.preventDefault();
    if (!isDrawing) return;

    const coords = getCoordinates(e);
    if (!coords) return;

    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!ctx) return;

    ctx.lineTo(coords.x, coords.y);
    ctx.stroke();
  };

  const stopDrawing = () => {
    setIsDrawing(false);
  };

  const clearSignature = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const rect = canvas.getBoundingClientRect();
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, rect.width, rect.height);
    setHasSignature(false);
  };

  const handleSubmit = async () => {
    setError(null);

    if (!hasSignature) {
      setError('Please provide your signature');
      return;
    }

    if (!signedByName.trim()) {
      setError('Please enter your name');
      return;
    }

    const canvas = canvasRef.current;
    if (!canvas) {
      setError('Signature canvas not available');
      return;
    }

    setIsSubmitting(true);
    try {
      // Get signature as base64 PNG
      const signatureData = canvas.toDataURL('image/png');
      await onComplete(signatureData, signedByName.trim());
    } catch {
      setError('Failed to submit signature. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const isValid = hasSignature && signedByName.trim().length > 0;

  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-center gap-2">
          <PenLine className="h-5 w-5 text-muted-foreground" />
          <CardTitle className="text-lg">Sign to Complete</CardTitle>
        </div>
        <p className="text-sm text-muted-foreground">
          Please sign below to confirm you have read and understood all the content.
        </p>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Name input */}
        <div className="space-y-2">
          <Label htmlFor="signedByName">Your Full Name</Label>
          <Input
            id="signedByName"
            value={signedByName}
            onChange={(e) => setSignedByName(e.target.value)}
            placeholder="Enter your full name"
            disabled={isSubmitting}
          />
        </div>

        {/* Signature canvas */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <Label>Your Signature</Label>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={clearSignature}
              disabled={!hasSignature || isSubmitting}
              className="h-8 gap-1 text-xs"
            >
              <Eraser className="h-3 w-3" />
              Clear
            </Button>
          </div>
          <div className="relative">
            <canvas
              ref={canvasRef}
              className={cn(
                'w-full h-40 border rounded-lg cursor-crosshair touch-none',
                isSubmitting && 'opacity-50 pointer-events-none'
              )}
              onMouseDown={startDrawing}
              onMouseMove={draw}
              onMouseUp={stopDrawing}
              onMouseLeave={stopDrawing}
              onTouchStart={startDrawing}
              onTouchMove={draw}
              onTouchEnd={stopDrawing}
            />
            {!hasSignature && (
              <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                <p className="text-sm text-muted-foreground">
                  Draw your signature here
                </p>
              </div>
            )}
          </div>
          <p className="text-xs text-muted-foreground text-center">
            Use your mouse or finger to sign
          </p>
        </div>

        {/* Error message */}
        {error && (
          <div className="p-3 rounded-lg bg-destructive/10 text-destructive text-sm">
            {error}
          </div>
        )}

        {/* Preview section */}
        {hasSignature && signedByName && (
          <div className="p-4 rounded-lg bg-muted/50 space-y-2">
            <p className="text-sm font-medium">Preview</p>
            <div className="flex items-center gap-4">
              <div className="flex-1">
                <p className="text-xs text-muted-foreground">Signed by</p>
                <p className="font-medium">{signedByName}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Date</p>
                <p className="font-medium">
                  {new Date().toLocaleDateString('en-GB', {
                    day: '2-digit',
                    month: 'short',
                    year: 'numeric',
                  })}
                </p>
              </div>
            </div>
          </div>
        )}
      </CardContent>

      <CardFooter className="border-t pt-4">
        <Button
          onClick={handleSubmit}
          disabled={!isValid || isSubmitting}
          className="w-full gap-2"
        >
          {isSubmitting ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin" />
              Completing...
            </>
          ) : (
            <>
              <Check className="h-4 w-4" />
              Complete Learning
            </>
          )}
        </Button>
      </CardFooter>
    </Card>
  );
}

// Skeleton for loading state
export function SignatureCaptureSkeleton() {
  return (
    <Card>
      <CardHeader>
        <div className="h-6 w-40 bg-muted rounded animate-pulse" />
        <div className="h-4 w-64 bg-muted rounded animate-pulse" />
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="space-y-2">
          <div className="h-4 w-24 bg-muted rounded animate-pulse" />
          <div className="h-10 w-full bg-muted rounded animate-pulse" />
        </div>
        <div className="space-y-2">
          <div className="h-4 w-24 bg-muted rounded animate-pulse" />
          <div className="h-40 w-full bg-muted rounded-lg animate-pulse" />
        </div>
      </CardContent>
      <CardFooter className="border-t pt-4">
        <div className="h-10 w-full bg-muted rounded animate-pulse" />
      </CardFooter>
    </Card>
  );
}
