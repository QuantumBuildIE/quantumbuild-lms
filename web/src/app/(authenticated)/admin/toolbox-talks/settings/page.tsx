'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { usePermission } from '@/lib/auth/use-auth';
import { useLookupCategories } from '@/hooks/use-lookups';
import { LookupCategorySection } from '@/components/admin/lookup-category-section';

export default function AdminToolboxTalksSettingsPage() {
  const hasAdminPermission = usePermission('Learnings.Admin');
  const { data: categories, isLoading: categoriesLoading } = useLookupCategories();

  if (!hasAdminPermission) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
          <p className="text-muted-foreground">
            Learnings configuration
          </p>
        </div>
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            You do not have permission to access settings.
          </p>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
        <p className="text-muted-foreground">
          Configure Learnings module settings
        </p>
      </div>

      {/* Lookup Values Management */}
      <div className="space-y-4">
        <div>
          <h2 className="text-lg font-semibold tracking-tight">Lookup Values</h2>
          <p className="text-sm text-muted-foreground">
            Manage the categories, departments, and job titles available across the system. Drag to reorder.
          </p>
        </div>

        {categoriesLoading ? (
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
        ) : categories && categories.length > 0 ? (
          <div className="space-y-4">
            {categories.map((category) => (
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

      {/* Other settings (placeholders) */}
      <div className="grid gap-6">
        <Card>
          <CardHeader>
            <CardTitle>General Settings</CardTitle>
            <CardDescription>
              Configure general learnings settings
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Settings configuration coming soon.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Notification Settings</CardTitle>
            <CardDescription>
              Configure reminder and notification preferences
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Notification settings coming soon.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Quiz Settings</CardTitle>
            <CardDescription>
              Configure default quiz and assessment settings
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Quiz settings coming soon.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
