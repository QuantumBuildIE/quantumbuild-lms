"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { useCreateSite, useUpdateSite } from "@/lib/api/admin/use-sites";
import { getApiErrorMessage } from "@/lib/utils";
import type { Site } from "@/types/admin";

const locationFormSchema = z.object({
  siteName: z.string().min(1, "Location name is required").max(200),
  siteCode: z.string().max(50).optional(),
  address: z.string().max(500).optional(),
  city: z.string().max(100).optional(),
  postalCode: z.string().max(20).optional(),
  phone: z.string().max(50).optional(),
  email: z.string().email("Invalid email").max(255).optional().or(z.literal("")),
  notes: z.string().max(2000).optional(),
  isActive: z.boolean(),
});

type LocationFormValues = z.infer<typeof locationFormSchema>;

interface LocationFormProps {
  site?: Site;
  onSuccess?: () => void;
  onCancel?: () => void;
}

export function LocationForm({ site, onSuccess, onCancel }: LocationFormProps) {
  const isEditing = !!site;

  const createSite = useCreateSite();
  const updateSite = useUpdateSite();

  const form = useForm<LocationFormValues>({
    resolver: zodResolver(locationFormSchema),
    defaultValues: {
      siteName: site?.siteName ?? "",
      siteCode: site?.siteCode ?? "",
      address: site?.address ?? "",
      city: site?.city ?? "",
      postalCode: site?.postalCode ?? "",
      phone: site?.phone ?? "",
      email: site?.email ?? "",
      notes: site?.notes ?? "",
      isActive: site?.isActive ?? true,
    },
  });

  const isSubmitting = createSite.isPending || updateSite.isPending;

  async function onSubmit(values: LocationFormValues) {
    const siteCode = values.siteCode?.trim() || undefined;

    try {
      if (isEditing) {
        await updateSite.mutateAsync({
          id: site.id,
          data: {
            siteCode,
            siteName: values.siteName,
            address: values.address || undefined,
            city: values.city || undefined,
            postalCode: values.postalCode || undefined,
            phone: values.phone || undefined,
            email: values.email || undefined,
            notes: values.notes || undefined,
            isActive: values.isActive,
            // Construction/Float fields aren't part of UpdateSiteDto - the
            // backend leaves them untouched, so this form doesn't need to know
            // about them at all.
            siteManagerId: site.siteManagerId ?? null,
            companyId: site.companyId ?? null,
          },
        });
        toast.success("Location updated");
      } else {
        await createSite.mutateAsync({
          siteCode,
          siteName: values.siteName,
          address: values.address || undefined,
          city: values.city || undefined,
          postalCode: values.postalCode || undefined,
          phone: values.phone || undefined,
          email: values.email || undefined,
          notes: values.notes || undefined,
          isActive: values.isActive,
        });
        toast.success("Location created");
      }
      onSuccess?.();
    } catch (error) {
      toast.error(isEditing ? "Failed to update location" : "Failed to create location", {
        description: getApiErrorMessage(error),
      });
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <div className="grid gap-6 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="siteName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Location Name *</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., Main Warehouse" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="siteCode"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Location Code</FormLabel>
                <FormControl>
                  <Input placeholder="Optional" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        <FormField
          control={form.control}
          name="address"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Address</FormLabel>
              <FormControl>
                <Input placeholder="e.g., 12 Industrial Way" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="grid gap-6 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="city"
            render={({ field }) => (
              <FormItem>
                <FormLabel>City</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., Dublin" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="postalCode"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Postal Code</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., D01 F5P2" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        <div className="grid gap-6 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="phone"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Phone</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., +353 1 234 5678" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Email</FormLabel>
                <FormControl>
                  <Input type="email" placeholder="e.g., site@example.com" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        <FormField
          control={form.control}
          name="notes"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Notes</FormLabel>
              <FormControl>
                <Textarea
                  placeholder="Additional notes about this location"
                  className="resize-none"
                  rows={4}
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="isActive"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center justify-between rounded-md border p-4">
              <div className="space-y-1 leading-none">
                <FormLabel>Active</FormLabel>
                <FormDescription>
                  Inactive locations won&apos;t appear in selection dropdowns, but existing
                  employee assignments are unaffected.
                </FormDescription>
              </div>
              <FormControl>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
            </FormItem>
          )}
        />

        <div className="flex justify-end gap-4">
          {onCancel && (
            <Button type="button" variant="outline" onClick={onCancel}>
              Cancel
            </Button>
          )}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting
              ? isEditing
                ? "Updating..."
                : "Creating..."
              : isEditing
                ? "Update Location"
                : "Create Location"}
          </Button>
        </div>
      </form>
    </Form>
  );
}
