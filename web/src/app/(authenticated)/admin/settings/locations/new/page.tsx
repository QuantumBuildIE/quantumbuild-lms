"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { LocationForm } from "@/components/admin/location-form";
import { ChevronLeft } from "lucide-react";

export default function NewLocationPage() {
  const router = useRouter();

  const handleSuccess = () => {
    router.push("/admin/settings/locations");
  };

  const handleCancel = () => {
    router.back();
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/admin/settings/locations">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Add Location</h1>
          <p className="text-muted-foreground">Create a new location</p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Location Details</CardTitle>
          <CardDescription>Enter the details for the new location</CardDescription>
        </CardHeader>
        <CardContent>
          <LocationForm onSuccess={handleSuccess} onCancel={handleCancel} />
        </CardContent>
      </Card>
    </div>
  );
}
