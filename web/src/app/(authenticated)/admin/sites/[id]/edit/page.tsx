"use client";

import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { SiteForm } from "@/components/admin/site-form";
import { useSite } from "@/lib/api/admin/use-sites";
import { ChevronLeft } from "lucide-react";

export default function EditSitePage() {
  const params = useParams();
  const router = useRouter();
  const siteId = params.id as string;

  const { data: site, isLoading, error } = useSite(siteId);

  const handleSuccess = () => {
    router.push("/admin/sites");
  };

  const handleCancel = () => {
    router.back();
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/sites">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Edit Site</h1>
            <p className="text-muted-foreground">Loading site details...</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="flex items-center justify-center">
              <div className="animate-pulse text-muted-foreground">
                Loading...
              </div>
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
            <Link href="/admin/sites">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Edit Site</h1>
            <p className="text-muted-foreground">Site not found</p>
          </div>
        </div>
        <Card className="max-w-2xl">
          <CardContent className="py-8">
            <div className="text-center">
              <p className="text-destructive">
                Failed to load site. The site may have been deleted.
              </p>
              <Button className="mt-4" asChild>
                <Link href="/admin/sites">Back to Sites</Link>
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
          <Link href="/admin/sites">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Edit Site</h1>
          <p className="text-muted-foreground">
            Editing: {site.siteName} ({site.siteCode})
          </p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Site Details</CardTitle>
          <CardDescription>
            Update the site information
          </CardDescription>
        </CardHeader>
        <CardContent>
          <SiteForm site={site} onSuccess={handleSuccess} onCancel={handleCancel} />
        </CardContent>
      </Card>
    </div>
  );
}
