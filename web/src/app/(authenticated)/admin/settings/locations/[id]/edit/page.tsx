"use client";

import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { LocationForm } from "@/components/admin/location-form";
import { useSite } from "@/lib/api/admin/use-sites";
import { ChevronLeft } from "lucide-react";

export default function EditLocationPage() {
  const params = useParams();
  const router = useRouter();
  const siteId = params.id as string;

  const { data: site, isLoading, error } = useSite(siteId);

  const handleSuccess = () => {
    router.push("/admin/settings/locations");
  };

  const handleCancel = () => {
    router.push("/admin/settings/locations");
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/settings/locations">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Edit Location</h1>
            <p className="text-muted-foreground">Loading location details...</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="flex items-center justify-center">
              <div className="animate-pulse text-muted-foreground">Loading...</div>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !site) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/settings/locations">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Edit Location</h1>
            <p className="text-muted-foreground">Location not found</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="text-center">
              <p className="text-destructive">
                Failed to load location. It may have been deleted.
              </p>
              <Button className="mt-4" asChild>
                <Link href="/admin/settings/locations">Back to Locations</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/admin/settings/locations">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Edit Location</h1>
          <p className="text-muted-foreground">
            Editing: {site.siteName}
            {site.siteCode ? ` (${site.siteCode})` : ""}
          </p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Location Details</CardTitle>
          <CardDescription>Update the location information</CardDescription>
        </CardHeader>
        <CardContent>
          <LocationForm site={site} onSuccess={handleSuccess} onCancel={handleCancel} />
        </CardContent>
      </Card>
    </div>
  );
}
