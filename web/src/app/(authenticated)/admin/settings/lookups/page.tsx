'use client';

import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useLookupCategories } from '@/hooks/use-lookups';
import { LookupCategorySection } from '@/components/admin/lookup-category-section';

export default function AdminSettingsLookupsPage() {
  const { data: categories, isLoading } = useLookupCategories();
  const lookupCategories = categories?.filter((c) => c.name !== 'Language');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Lookups</h1>
        <p className="text-muted-foreground">
          Manage training categories, departments, and job titles. Drag to reorder.
        </p>
      </div>

      {isLoading ? (
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <Card key={i}>
              <CardHeader>
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-4 w-64" />
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  <Skeleton className="h-10 w-full" />
                  <Skeleton className="h-10 w-full" />
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : lookupCategories && lookupCategories.length > 0 ? (
        <div className="space-y-4">
          {lookupCategories.map((category) => (
            <LookupCategorySection key={category.id} category={category} />
          ))}
        </div>
      ) : (
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            No lookup categories found.
          </p>
        </Card>
      )}
    </div>
  );
}
