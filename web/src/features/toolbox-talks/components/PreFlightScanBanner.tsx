'use client';

import { useState, useMemo, useCallback } from 'react';
import { AlertTriangle, ChevronDown, ChevronRight, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type {
  PreFlightScanResult,
  PreFlightFinding,
  PreFlightFindingType,
} from '@/types/content-creation';

interface PreFlightScanBannerProps {
  preFlightScanJson: string;
  /** localStorage key suffix — e.g. the runId */
  storageKey: string;
}

const STORAGE_PREFIX = 'preflight-banner-dismissed-';

const groupConfig: Record<
  PreFlightFindingType,
  { label: string; description: string }
> = {
  HighRiskTerm: {
    label: 'High Risk Terms',
    description: 'with suggested translations',
  },
  ProperNoun: {
    label: 'Proper Nouns',
    description: 'should not be translated',
  },
  RoleConstruct: {
    label: 'Role Constructs',
    description: 'need consistent translation',
  },
  SlashConstruct: {
    label: 'Slash Constructs',
    description: 'risk meaning change',
  },
};

function parsePreFlightScan(json: string): PreFlightScanResult | null {
  try {
    return JSON.parse(json) as PreFlightScanResult;
  } catch {
    return null;
  }
}

export function PreFlightScanBanner({
  preFlightScanJson,
  storageKey,
}: PreFlightScanBannerProps) {
  const fullKey = STORAGE_PREFIX + storageKey;

  const [dismissed, setDismissed] = useState(() => {
    if (typeof window === 'undefined') return false;
    return localStorage.getItem(fullKey) === '1';
  });
  const [expanded, setExpanded] = useState(false);

  const scan = useMemo(
    () => parsePreFlightScan(preFlightScanJson),
    [preFlightScanJson]
  );

  const grouped = useMemo(() => {
    if (!scan?.findings?.length) return new Map<PreFlightFindingType, PreFlightFinding[]>();
    const map = new Map<PreFlightFindingType, PreFlightFinding[]>();
    for (const f of scan.findings) {
      const list = map.get(f.type) ?? [];
      list.push(f);
      map.set(f.type, list);
    }
    return map;
  }, [scan]);

  const dismiss = useCallback(() => {
    setDismissed(true);
    localStorage.setItem(fullKey, '1');
  }, [fullKey]);

  if (dismissed || !scan?.hasFindings) return null;

  const totalFindings = scan.findings.length;

  return (
    <div className="rounded-lg border border-amber-300 bg-amber-50">
      {/* Summary row */}
      <div className="flex items-center gap-3 px-4 py-3">
        <AlertTriangle className="h-5 w-5 shrink-0 text-amber-600" />
        <button
          type="button"
          className="flex flex-1 items-center gap-2 text-left text-sm font-medium text-amber-800"
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? (
            <ChevronDown className="h-4 w-4 shrink-0" />
          ) : (
            <ChevronRight className="h-4 w-4 shrink-0" />
          )}
          Pre-flight scan found {totalFindings} suggestion{totalFindings !== 1 ? 's' : ''} before translation
        </button>
        <Button
          variant="ghost"
          size="icon"
          className="h-6 w-6 shrink-0 text-amber-600 hover:text-amber-800"
          onClick={dismiss}
        >
          <X className="h-4 w-4" />
        </Button>
      </div>

      {/* Expanded detail */}
      {expanded && (
        <div className="space-y-3 border-t border-amber-200 px-4 py-3">
          {(
            ['HighRiskTerm', 'ProperNoun', 'RoleConstruct', 'SlashConstruct'] as PreFlightFindingType[]
          ).map((type) => {
            const items = grouped.get(type);
            if (!items?.length) return null;
            const cfg = groupConfig[type];
            return (
              <div key={type}>
                <div className="mb-1.5 flex items-center gap-2">
                  <span className="text-sm font-semibold text-amber-900">
                    {cfg.label}
                  </span>
                  <span className="text-xs text-amber-600">
                    ({cfg.description})
                  </span>
                </div>
                <div className="space-y-1 pl-2">
                  {items.map((f, i) => (
                    <div
                      key={i}
                      className="flex flex-wrap items-baseline gap-x-2 text-sm text-amber-800"
                    >
                      <Badge
                        variant="outline"
                        className="border-amber-300 text-xs text-amber-700"
                      >
                        {f.term}
                      </Badge>
                      <span className="text-amber-600">{f.risk}</span>
                      {f.suggestedTranslation && (
                        <span className={cn('text-xs text-amber-700')}>
                          &rarr; &quot;{f.suggestedTranslation}&quot;
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
