'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { usePermission } from '@/lib/auth/use-auth';
import { SafetyGlossarySection } from '@/features/toolbox-talks/components/settings/safety-glossary-section';
import { PassThresholdSection } from '@/features/toolbox-talks/components/settings/pass-threshold-section';
import { AuditPurposeSection } from '@/features/toolbox-talks/components/settings/audit-purpose-section';
import { SkipValidationSection } from '@/features/toolbox-talks/components/settings/skip-validation-section';

export default function AdminToolboxTalksSettingsPage() {
  const hasAdminPermission = usePermission('Learnings.Admin');

  if (!hasAdminPermission) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Learnings Settings</h1>
          <p className="text-muted-foreground">
            Learnings module configuration
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
        <h1 className="text-2xl font-semibold tracking-tight">Learnings Settings</h1>
        <p className="text-muted-foreground">
          Configure Learnings module settings
        </p>
      </div>

      <Tabs defaultValue="general">
        <TabsList>
          <TabsTrigger value="general">General</TabsTrigger>
          <TabsTrigger value="notifications">Notifications</TabsTrigger>
          <TabsTrigger value="quiz">Quiz</TabsTrigger>
          <TabsTrigger value="validation">Validation</TabsTrigger>
        </TabsList>

        <TabsContent value="general" className="space-y-6 pt-4">
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
        </TabsContent>

        <TabsContent value="notifications" className="space-y-6 pt-4">
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
        </TabsContent>

        <TabsContent value="quiz" className="space-y-6 pt-4">
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
        </TabsContent>

        <TabsContent value="validation" className="space-y-6 pt-4">
          <SafetyGlossarySection />
          <SkipValidationSection />
          <PassThresholdSection />
          <AuditPurposeSection />
        </TabsContent>
      </Tabs>
    </div>
  );
}
