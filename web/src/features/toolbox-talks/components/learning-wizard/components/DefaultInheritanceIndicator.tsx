'use client';

import { RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface DefaultInheritanceIndicatorProps {
  /** True when the field's current value differs from the tenant default. */
  isOverridden: boolean;
  /** Reverts the field to the tenant default. Only called when isOverridden is true. */
  onReset: () => void;
  className?: string;
}

/**
 * Subtle indicator shown next to a wizard toggle whose initial value came from a
 * tenant-level default (see Learning Defaults, admin/toolbox-talks/settings). Kept muted
 * in both states so it doesn't compete visually with the toggle itself.
 */
export function DefaultInheritanceIndicator({
  isOverridden,
  onReset,
  className,
}: DefaultInheritanceIndicatorProps) {
  if (!isOverridden) {
    return (
      <p className={cn('text-xs text-muted-foreground/70 mt-1', className)}>
        Using tenant default
      </p>
    );
  }

  return (
    <div className={cn('flex items-center gap-1.5 mt-1', className)}>
      <p className="text-xs text-amber-600 dark:text-amber-500">
        Overridden from tenant default
      </p>
      <Button
        type="button"
        variant="link"
        size="sm"
        className="h-auto p-0 text-xs"
        onClick={onReset}
      >
        <RotateCcw className="mr-1 h-3 w-3" aria-hidden="true" />
        Reset
      </Button>
    </div>
  );
}
