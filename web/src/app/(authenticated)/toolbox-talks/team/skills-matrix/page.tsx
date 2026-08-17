'use client';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Download, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { useSkillsMatrix, useExportSkillsMatrix } from '@/lib/api/toolbox-talks';
import { useLookupValues } from '@/hooks/use-lookups';
import { useAllDepartments } from '@/lib/api/admin/use-departments';
import { useAllSites } from '@/lib/api/admin/use-sites';
import { SkillsMatrixGrid } from '@/features/toolbox-talks/components/SkillsMatrixGrid';

export default function SupervisorSkillsMatrixPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const category = searchParams.get('category') || undefined;
  const departmentId = searchParams.get('departmentId') || undefined;
  const departmentUnassigned = searchParams.get('departmentUnassigned') === 'true';
  const siteId = searchParams.get('siteId') || undefined;
  const siteUnassigned = searchParams.get('siteUnassigned') === 'true';

  const { data: categories = [], isLoading: categoriesLoading } =
    useLookupValues('TrainingCategory');
  const { data: departments = [], isLoading: departmentsLoading } = useAllDepartments();
  const { data: sites = [], isLoading: sitesLoading } = useAllSites();
  const { data, isLoading } = useSkillsMatrix({
    category,
    departmentId,
    departmentUnassigned,
    siteId,
    siteUnassigned,
  });

  const exportMutation = useExportSkillsMatrix();

  const handleExport = () => {
    exportMutation.mutate(
      { category, departmentId, departmentUnassigned, siteId, siteUnassigned },
      {
        onSuccess: () => toast.success('Skills matrix exported successfully'),
        onError: () => toast.error('Failed to export skills matrix'),
      }
    );
  };

  const updateParams = (updates: Record<string, string | undefined>) => {
    const params = new URLSearchParams(searchParams.toString());
    Object.entries(updates).forEach(([key, value]) => {
      if (!value) params.delete(key);
      else params.set(key, value);
    });
    const queryString = params.toString();
    router.push(queryString ? `${pathname}?${queryString}` : pathname);
  };

  const updateCategory = (newCategory: string | undefined) => {
    updateParams({ category: newCategory });
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
        departmentFilter={{
          label: 'Department',
          allLabel: 'All Departments',
          unassignedLabel: 'No Department',
          entities: departments.map((d) => ({ id: d.id, name: d.name, isActive: d.isActive })),
          selectedId: departmentId,
          unassigned: departmentUnassigned,
          isLoading: departmentsLoading,
          onChange: ({ id, unassigned }) =>
            updateParams({
              departmentId: unassigned ? undefined : id,
              departmentUnassigned: unassigned ? 'true' : undefined,
            }),
        }}
        locationFilter={{
          label: 'Location',
          allLabel: 'All Locations',
          unassignedLabel: 'No Location',
          entities: sites.map((s) => ({
            id: s.id,
            name: s.siteName,
            isActive: s.isActive,
          })),
          selectedId: siteId,
          unassigned: siteUnassigned,
          isLoading: sitesLoading,
          onChange: ({ id, unassigned }) =>
            updateParams({
              siteId: unassigned ? undefined : id,
              siteUnassigned: unassigned ? 'true' : undefined,
            }),
        }}
      />
    </div>
  );
}
