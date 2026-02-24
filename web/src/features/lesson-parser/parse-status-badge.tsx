'use client';

import { Badge } from '@/components/ui/badge';
import { Loader2, CheckCircle, XCircle } from 'lucide-react';
import type { ParseJobStatus } from '@/types/lesson-parser';

interface ParseStatusBadgeProps {
  status: ParseJobStatus;
}

export function ParseStatusBadge({ status }: ParseStatusBadgeProps) {
  switch (status) {
    case 'Processing':
      return (
        <Badge variant="secondary" className="gap-1 bg-blue-100 text-blue-700 hover:bg-blue-100">
          <Loader2 className="h-3 w-3 animate-spin" />
          Processing
        </Badge>
      );
    case 'Completed':
      return (
        <Badge variant="secondary" className="gap-1 bg-green-100 text-green-700 hover:bg-green-100">
          <CheckCircle className="h-3 w-3" />
          Completed
        </Badge>
      );
    case 'Failed':
      return (
        <Badge variant="secondary" className="gap-1 bg-red-100 text-red-700 hover:bg-red-100">
          <XCircle className="h-3 w-3" />
          Failed
        </Badge>
      );
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}
