"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Textarea } from "@/components/ui/textarea";
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
import type { Site } from "@/types/admin";
import { toast } from "sonner";

const siteFormSchema = z.object({
  siteCode: z.string().min(1, "Site code is required").max(50),
  siteName: z.string().min(1, "Site name is required").max(200),
  address: z.string().max(500).optional().nullable(),
  city: z.string().max(100).optional().nullable(),
  postalCode: z.string().max(20).optional().nullable(),
  phone: z.string().max(50).optional().nullable(),
  email: z.string().email("Invalid email").max(200).optional().nullable().or(z.literal("")),
  notes: z.string().max(2000).optional().nullable(),
  isActive: z.boolean(),
});

type SiteFormValues = z.infer<typeof siteFormSchema>;

interface SiteFormProps {
  site?: Site;
  onSuccess?: () => void;
  onCancel?: () => void;
}

export function SiteForm({ site, onSuccess, onCancel }: SiteFormProps) {
  const isEditing = !!site;

  const createSite = useCreateSite();
  const updateSite = useUpdateSite();

  const form = useForm<SiteFormValues>({
    resolver: zodResolver(siteFormSchema) as any,
    defaultValues: {
      siteCode: site?.siteCode ?? "",
      siteName: site?.siteName ?? "",
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

  async function onSubmit(values: SiteFormValues) {
    try {
      // Clean up empty strings to null for optional fields
      const cleanedValues = {
        ...values,
        address: values.address || undefined,
        city: values.city || undefined,
        postalCode: values.postalCode || undefined,
        phone: values.phone || undefined,
        email: values.email || undefined,
        notes: values.notes || undefined,
      };

      if (isEditing) {
        await updateSite.mutateAsync({
          id: site.id,
          data: {
            siteCode: cleanedValues.siteCode,
            siteName: cleanedValues.siteName,
            address: cleanedValues.address,
            city: cleanedValues.city,
            postalCode: cleanedValues.postalCode,
            phone: cleanedValues.phone,
            email: cleanedValues.email,
            notes: cleanedValues.notes,
            isActive: cleanedValues.isActive,
          },
        });
        toast.success("Site updated successfully");
      } else {
        await createSite.mutateAsync({
          siteCode: cleanedValues.siteCode,
          siteName: cleanedValues.siteName,
          address: cleanedValues.address,
          city: cleanedValues.city,
          postalCode: cleanedValues.postalCode,
          phone: cleanedValues.phone,
          email: cleanedValues.email,
          notes: cleanedValues.notes,
          isActive: cleanedValues.isActive,
        });
        toast.success("Site created successfully");
      }
      onSuccess?.();
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error(isEditing ? "Failed to update site" : "Failed to create site", {
        description: message,
      });
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <div className="grid gap-6 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="siteCode"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Site Code *</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., SITE-001" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="siteName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Site Name *</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., Dublin City Centre" {...field} />
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
                <Textarea
                  placeholder="Enter full address"
                  className="resize-none"
                  {...field}
                  value={field.value ?? ""}
                />
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
                  <Input placeholder="e.g., Dublin" {...field} value={field.value ?? ""} />
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
                  <Input placeholder="e.g., D01 AB12" {...field} value={field.value ?? ""} />
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
                  <Input placeholder="e.g., +353 1 234 5678" {...field} value={field.value ?? ""} />
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
                  <Input
                    type="email"
                    placeholder="e.g., site@example.com"
                    {...field}
                    value={field.value ?? ""}
                  />
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
                  placeholder="Additional notes about this site"
                  className="resize-none"
                  rows={4}
                  {...field}
                  value={field.value ?? ""}
                />
              </FormControl>
              <FormDescription>Optional notes or additional information</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="isActive"
          render={({ field }) => (
            <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
              <FormControl>
                <Checkbox
                  checked={field.value}
                  onCheckedChange={field.onChange}
                />
              </FormControl>
              <div className="space-y-1 leading-none">
                <FormLabel>Active</FormLabel>
                <FormDescription>
                  Inactive sites won&apos;t appear in selection dropdowns
                </FormDescription>
              </div>
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
            {isSubmitting ? (
              <>
                <LoadingSpinner className="mr-2 h-4 w-4" />
                {isEditing ? "Updating..." : "Creating..."}
              </>
            ) : isEditing ? (
              "Update Site"
            ) : (
              "Create Site"
            )}
          </Button>
        </div>
      </form>
    </Form>
  );
}

function LoadingSpinner({ className }: { className?: string }) {
  return (
    <svg
      className={`animate-spin ${className}`}
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
}

