'use client';

import { useState, useEffect } from 'react';
import { Textarea } from '@/components/ui/textarea';

export interface SectionBodyEditorProps {
  value: string;
  onChange: (value: string) => void;
  sectionId?: string;
  ariaLabel?: string;
  disabled?: boolean;
}

export function SectionBodyEditor({
  value,
  onChange,
  sectionId,
  ariaLabel,
  disabled,
}: SectionBodyEditorProps) {
  const [localValue, setLocalValue] = useState(value);

  useEffect(() => {
    setLocalValue(value);
  }, [value]);

  return (
    <Textarea
      id={sectionId ? `section-body-${sectionId}` : undefined}
      aria-label={ariaLabel ?? 'Section body'}
      value={localValue}
      onChange={(e) => setLocalValue(e.target.value)}
      onBlur={() => onChange(localValue)}
      disabled={disabled}
      className="min-h-[120px] resize-y text-sm"
    />
  );
}
