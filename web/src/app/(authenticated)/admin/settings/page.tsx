'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export default function AdminSettingsGeneralPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">General Settings</h1>
        <p className="text-muted-foreground">
          Manage company name, contact details, and general configuration
        </p>
      </div>

      <div className="grid gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Company Information</CardTitle>
            <CardDescription>
              Organisation name, contact email, and other details
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Company configuration coming soon.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
