"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Skeleton } from "@/components/ui/skeleton";
import { useCreateRegulatoryBody } from "@/lib/api/admin/use-regulatory-ingestion";
import { useAvailableSectors } from "@/lib/api/admin/use-tenant-sectors";
import { toast } from "sonner";
import type { RegulatoryBody } from "@/types/regulatory";

const formSchema = z
  .object({
    name: z.string().trim().min(1, "Name is required").max(100),
    code: z.string().trim().min(1, "Code is required").max(20),
    country: z.string().trim().min(1, "Country is required").max(100),
    website: z.string().trim().max(500).optional(),
    kind: z.enum(["Regulation", "Standard"]),
    sectorId: z.string().optional(),
  })
  .refine((data) => data.kind !== "Standard" || !!data.sectorId, {
    message: "Sector is required for Standards",
    path: ["sectorId"],
  });

type FormValues = z.infer<typeof formSchema>;

interface CreateRegulatoryBodyDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (body: RegulatoryBody) => void;
}

export function CreateRegulatoryBodyDialog({
  open,
  onOpenChange,
  onCreated,
}: CreateRegulatoryBodyDialogProps) {
  const { data: sectors, isLoading: loadingSectors } = useAvailableSectors();
  const createBody = useCreateRegulatoryBody();

  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: "",
      code: "",
      country: "",
      website: "",
      kind: "Regulation",
      sectorId: "",
    },
  });

  const kind = form.watch("kind");

  React.useEffect(() => {
    if (!open) {
      form.reset();
    }
  }, [open, form]);

  React.useEffect(() => {
    if (kind === "Regulation") {
      form.setValue("sectorId", "");
      form.clearErrors("sectorId");
    }
  }, [kind, form]);

  async function onSubmit(values: FormValues) {
    try {
      const created = await createBody.mutateAsync({
        name: values.name,
        code: values.code,
        country: values.country,
        website: values.website || undefined,
        kind: values.kind,
        sectorId: values.kind === "Standard" ? values.sectorId : undefined,
      });
      toast.success(`${values.kind} added`, {
        description: `"${created.name}" has been added to the catalog.`,
      });
      onOpenChange(false);
      onCreated(created);
    } catch (error: unknown) {
      let message = "Failed to create regulatory body";
      if (error && typeof error === "object" && "response" in error) {
        const axiosError = error as { response?: { data?: { message?: string } } };
        if (axiosError.response?.data?.message) {
          message = axiosError.response.data.message;
        }
      } else if (error instanceof Error) {
        message = error.message;
      }
      toast.error("Error", { description: message });
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Add Regulatory Body</DialogTitle>
          <DialogDescription>
            Add a Regulation (legally mandated, applies to tenants automatically
            by sector) or a Standard (voluntary, tenants subscribe explicitly) to
            the catalog. Documents are attached separately once the body exists.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="kind"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Type *</FormLabel>
                  <FormControl>
                    <RadioGroup
                      onValueChange={field.onChange}
                      value={field.value}
                      className="flex gap-4"
                    >
                      <div className="flex items-center space-x-2">
                        <RadioGroupItem value="Regulation" id="kind-regulation" />
                        <label
                          htmlFor="kind-regulation"
                          className="text-sm font-medium cursor-pointer"
                        >
                          Regulation
                        </label>
                      </div>
                      <div className="flex items-center space-x-2">
                        <RadioGroupItem value="Standard" id="kind-standard" />
                        <label
                          htmlFor="kind-standard"
                          className="text-sm font-medium cursor-pointer"
                        >
                          Standard
                        </label>
                      </div>
                    </RadioGroup>
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {kind === "Standard" && (
              <FormField
                control={form.control}
                name="sectorId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Sector *</FormLabel>
                    {loadingSectors ? (
                      <Skeleton className="h-9 w-full" />
                    ) : (
                      <Select onValueChange={field.onChange} value={field.value}>
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue placeholder="Select a sector" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {(sectors ?? []).map((sector) => (
                            <SelectItem key={sector.id} value={sector.id}>
                              {sector.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Name *</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g., International Organization for Standardization" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="code"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Code *</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., ISO" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="country"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Country *</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., IE, International" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="website"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Website</FormLabel>
                  <FormControl>
                    <Input placeholder="https://example.org" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter className="pt-4">
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
                disabled={createBody.isPending}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={createBody.isPending}>
                {createBody.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Adding...
                  </>
                ) : (
                  "Add to Catalog"
                )}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
