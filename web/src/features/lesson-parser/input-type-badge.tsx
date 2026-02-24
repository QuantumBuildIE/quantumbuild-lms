'use client';

import { Badge } from '@/components/ui/badge';
import type { ParseInputType } from '@/types/lesson-parser';

interface InputTypeBadgeProps {
  type: ParseInputType;
}

const TYPE_LABELS: Record<ParseInputType, string> = {
  Pdf: 'PDF',
  Docx: 'Word',
  Url: 'URL',
  Text: 'Text',
};

export function InputTypeBadge({ type }: InputTypeBadgeProps) {
  return (
    <Badge variant="outline" className="font-normal">
      {TYPE_LABELS[type] ?? type}
    </Badge>
  );
}
