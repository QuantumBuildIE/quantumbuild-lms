'use client';

import {
  AlertTriangle,
  CheckCircle,
  Circle,
  Clock,
  Loader2,
  ShieldCheck,
  Sparkles,
  UserCheck,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import type { TranslationWorkflowState } from '@/types/workflows';

interface WorkflowStateBadgeProps {
  state: TranslationWorkflowState;
}

interface StateConfig {
  icon: React.ReactNode;
  label: string;
  tooltip: string;
  className: string;
}

const stateConfig: Record<TranslationWorkflowState, StateConfig> = {
  Initial: {
    icon: <Circle className="h-3 w-3" />,
    label: 'Initial',
    tooltip: 'Not yet translated',
    className: 'bg-gray-100 text-gray-800 hover:bg-gray-100',
  },
  Translating: {
    icon: <Loader2 className="h-3 w-3 animate-spin" />,
    label: 'Translating',
    tooltip: 'Translation in progress',
    className: 'bg-blue-100 text-blue-800 hover:bg-blue-100',
  },
  Validating: {
    icon: <Loader2 className="h-3 w-3 animate-spin" />,
    label: 'Validating',
    tooltip: 'Back-translation validation in progress',
    className: 'bg-blue-100 text-blue-800 hover:bg-blue-100',
  },
  AIGenerated: {
    icon: <Sparkles className="h-3 w-3" />,
    label: 'Translated · awaiting validation',
    tooltip: 'AI translation complete; awaiting validation',
    className: 'bg-blue-100 text-blue-800 hover:bg-blue-100',
  },
  Validated: {
    icon: <ShieldCheck className="h-3 w-3" />,
    label: 'Validated',
    tooltip: 'Validation complete; awaiting reviewer',
    className: 'bg-amber-100 text-amber-800 hover:bg-amber-100',
  },
  ReviewerAccepted: {
    icon: <UserCheck className="h-3 w-3" />,
    label: 'Reviewer accepted',
    tooltip: 'Internal reviewer accepted; not yet final',
    className: 'bg-amber-100 text-amber-800 hover:bg-amber-100',
  },
  AwaitingThirdParty: {
    icon: <Clock className="h-3 w-3" />,
    label: 'Awaiting external',
    tooltip: 'External reviewer invitation pending',
    className: 'bg-blue-100 text-blue-800 hover:bg-blue-100',
  },
  ThirdPartyReviewed: {
    icon: <UserCheck className="h-3 w-3" />,
    label: 'External reviewed',
    tooltip: 'External reviewer submitted; awaiting final accept',
    className: 'bg-amber-100 text-amber-800 hover:bg-amber-100',
  },
  Accepted: {
    icon: <CheckCircle className="h-3 w-3" />,
    label: 'Accepted',
    tooltip: 'Final accepted translation',
    className: 'bg-green-100 text-green-800 hover:bg-green-100',
  },
  Stale: {
    icon: <AlertTriangle className="h-3 w-3" />,
    label: 'Stale',
    tooltip: 'Translation needs refresh',
    className: 'bg-amber-100 text-amber-800 hover:bg-amber-100',
  },
};

export function WorkflowStateBadge({ state }: WorkflowStateBadgeProps) {
  const config = stateConfig[state];

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Badge variant="secondary" className={`gap-1 ${config.className}`}>
            {config.icon}
            {config.label}
          </Badge>
        </TooltipTrigger>
        <TooltipContent>
          <p>{config.tooltip}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
