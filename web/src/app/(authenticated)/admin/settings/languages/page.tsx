'use client';

import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useLookupCategories } from '@/hooks/use-lookups';
import { LookupCategorySection } from '@/components/admin/lookup-category-section';

export default function AdminSettingsLanguagesPage() {
  const { data: categories, isLoading } = useLookupCategories();
  const languageCategory = categories?.find((c) => c.name === 'Language');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Languages</h1>
        <p className="text-muted-foreground">
          Enable or disable languages for subtitles, translations, and employee preferences
        </p>
      </div>

      {isLoading ? (
        <Card>
          <CardHeader>
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-4 w-64" />
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          </CardContent>
        </Card>
      ) : languageCategory ? (
        <LookupCategorySection category={languageCategory} />
      ) : (
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            No language configuration found.
          </p>
        </Card>
      )}
    </div>
  );
}
