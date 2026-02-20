'use client';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';
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

  const updateCategory = (newCategory: string | undefined) => {
    const params = new URLSearchParams(searchParams.toString());
    if (!newCategory) {
      params.delete('category');
    } else {
      params.set('category', newCategory);
    }
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
      </div>

      <SkillsMatrixGrid
        data={data}
        isLoading={isLoading}
        categoryFilter={{
          categories,
          selectedCategory: category,
          onCategoryChange: updateCategory,
          isLoading: categoriesLoading,
        }}
      />
    </div>
  );
}
