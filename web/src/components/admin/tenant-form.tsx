"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { useCreateTenant, useUpdateTenant } from "@/lib/api/admin/use-tenants";
import type { TenantDetail } from "@/types/admin";
import { toast } from "sonner";

const tenantFormSchema = z.object({
  name: z.string().min(1, "Tenant name is required").max(200),
  code: z.string().max(50).optional().nullable(),
  companyName: z.string().max(200).optional().nullable(),
  contactName: z.string().max(200).optional().nullable(),
  contactEmail: z
    .string()
    .email("Invalid email")
    .max(200)
    .optional()
    .nullable()
    .or(z.literal("")),
});

type TenantFormData = z.infer<typeof tenantFormSchema>;

interface TenantFormProps {
  tenant?: TenantDetail;
  onSuccess: () => void;
  onCancel: () => void;
}

export function TenantForm({ tenant, onSuccess, onCancel }: TenantFormProps) {
  const isEditing = !!tenant;
  const createTenant = useCreateTenant();
  const updateTenant = useUpdateTenant();

  const form = useForm<TenantFormData>({
    resolver: zodResolver(tenantFormSchema),
    defaultValues: {
      name: tenant?.name ?? "",
      code: tenant?.code ?? "",
      companyName: tenant?.companyName ?? "",
      contactName: tenant?.contactName ?? "",
      contactEmail: tenant?.contactEmail ?? "",
    },
  });

  const onSubmit = async (data: TenantFormData) => {
    const payload = {
      name: data.name,
      code: data.code || undefined,
      companyName: data.companyName || undefined,
      contactName: data.contactName || undefined,
      contactEmail: data.contactEmail || undefined,
    };

    try {
      if (isEditing) {
        await updateTenant.mutateAsync({ id: tenant.id, data: payload });
        toast.success("Tenant updated successfully");
      } else {
        await createTenant.mutateAsync(payload);
        toast.success("Tenant created successfully");
      }
      onSuccess();
    } catch {
      toast.error(isEditing ? "Failed to update tenant" : "Failed to create tenant");
    }
  };

  const isPending = createTenant.isPending || updateTenant.isPending;

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Tenant Name *</FormLabel>
              <FormControl>
                <Input placeholder="e.g. Acme Construction" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="code"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Code</FormLabel>
              <FormControl>
                <Input
                  placeholder="e.g. ACME"
                  {...field}
                  value={field.value ?? ""}
                />
              </FormControl>
              <FormDescription>
                Optional unique identifier code for the tenant
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="companyName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Company Name</FormLabel>
              <FormControl>
                <Input
                  placeholder="e.g. Acme Construction Ltd."
                  {...field}
                  value={field.value ?? ""}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="contactName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Contact Name</FormLabel>
              <FormControl>
                <Input
                  placeholder="e.g. John Smith"
                  {...field}
                  value={field.value ?? ""}
                />
              </FormControl>
              <FormDescription>
                {!isEditing &&
                  "If provided with an email, an admin user account will be created and a welcome email sent"}
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="contactEmail"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Contact Email</FormLabel>
              <FormControl>
                <Input
                  type="email"
                  placeholder="e.g. john@acme.com"
                  {...field}
                  value={field.value ?? ""}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="flex items-center gap-4 pt-4">
          <Button type="submit" disabled={isPending}>
            {isPending
              ? isEditing
                ? "Saving..."
                : "Creating..."
              : isEditing
                ? "Save Changes"
                : "Create Tenant"}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </form>
    </Form>
  );
}
