'use client';

import { useEffect, useRef } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Loader2, CheckCircle2, XCircle } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ParseLogEntry {
  timestamp: Date;
  message: string;
}

interface ParseLogPanelProps {
  entries: ParseLogEntry[];
  isActive: boolean;
  isFailed: boolean;
}

export function ParseLogPanel({ entries, isActive, isFailed }: ParseLogPanelProps) {
  const scrollRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom on new entries
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [entries.length]);

  return (
    <Card>
      <CardContent className="pt-4">
        <div className="mb-3 flex items-center gap-2">
          {isActive ? (
            <Loader2 className="h-4 w-4 animate-spin text-primary" />
          ) : isFailed ? (
            <XCircle className="h-4 w-4 text-destructive" />
          ) : (
            <CheckCircle2 className="h-4 w-4 text-green-600" />
          )}
          <h3 className="text-sm font-medium">
            {isActive ? 'Parsing content...' : isFailed ? 'Parse failed' : 'Parse complete'}
          </h3>
        </div>

        <div
          ref={scrollRef}
          className="max-h-48 overflow-y-auto rounded-md bg-muted/50 p-3 font-mono text-xs"
        >
          {entries.map((entry, i) => (
            <div
              key={i}
              className={cn(
                'flex gap-3 py-0.5',
                entry.message.startsWith('Error') && 'text-destructive'
              )}
            >
              <span className="shrink-0 text-muted-foreground">
                {entry.timestamp.toLocaleTimeString()}
              </span>
              <span>{entry.message}</span>
            </div>
          ))}

          {isActive && (
            <div className="flex gap-3 py-0.5 text-muted-foreground">
              <span className="shrink-0">
                {new Date().toLocaleTimeString()}
              </span>
              <span className="animate-pulse">Waiting for response...</span>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
