'use client';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Download, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { useSkillsMatrix, useExportSkillsMatrix } from '@/lib/api/toolbox-talks';
import { useLookupValues } from '@/hooks/use-lookups';
import { SkillsMatrixGrid } from '@/features/toolbox-talks/components/SkillsMatrixGrid';

export default function SupervisorSkillsMatrixPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const category = searchParams.get('category') || undefined;

  const { data: categories = [], isLoading: categoriesLoading } =
    useLookupValues('TrainingCategory');
  const { data, isLoading } = useSkillsMatrix({ category });

  const exportMutation = useExportSkillsMatrix();

  const handleExport = () => {
    exportMutation.mutate({ category }, {
      onSuccess: () => toast.success('Skills matrix exported successfully'),
      onError: () => toast.error('Failed to export skills matrix'),
    });
  };

  const updateUrlParams = (updates: Record<string, string | null | undefined>) => {
    const params = new URLSearchParams(searchParams.toString());
    Object.entries(updates).forEach(([key, value]) => {
      if (value === null || value === undefined || value === 'all') {
        params.delete(key);
      } else {
        params.set(key, String(value));
      }
    });
    const queryString = params.toString();
    router.push(queryString ? `${pathname}?${queryString}` : pathname);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Skills Matrix</h1>
          <p className="text-muted-foreground">
            Overview of your team&apos;s learning progress
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            size="sm"
            onClick={handleExport}
            disabled={exportMutation.isPending || isLoading || !data}
          >
            {exportMutation.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Download className="mr-2 h-4 w-4" />
            )}
            Export to Excel
          </Button>
          <Select
            value={category || 'all'}
            onValueChange={(value) =>
              updateUrlParams({ category: value === 'all' ? null : value })
            }
            disabled={categoriesLoading}
          >
            <SelectTrigger className="w-[220px]">
              <SelectValue placeholder="All Categories" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Categories</SelectItem>
              {categories
                .filter((c) => c.isActive)
                .map((cat) => (
                  <SelectItem key={cat.id} value={cat.name}>
                    {cat.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <SkillsMatrixGrid data={data} isLoading={isLoading} />
    </div>
  );
}
