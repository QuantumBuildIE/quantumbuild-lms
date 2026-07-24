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
import {
  useRegulatoryBodies,
  useCreateRegulatoryDocument,
} from "@/lib/api/admin/use-regulatory-ingestion";
import { toast } from "sonner";

// Mirrors the client-side guidance in the document detail page's checkSourceUrlInput —
// backend SourceUrlValidator is authoritative, this only gives an earlier, friendlier signal.
function isPlausibleHttpUrl(value: string): boolean {
  if (/^[a-zA-Z]:[\\/]/.test(value) || value.startsWith("/")) return false;
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

const formSchema = z.object({
  regulatoryBodyId: z.string().min(1, "Regulatory body is required"),
  title: z.string().trim().min(1, "Title is required").max(500),
  version: z.string().trim().min(1, "Version is required").max(50),
  sourceUrl: z
    .string()
    .trim()
    .optional()
    .refine((value) => !value || isPlausibleHttpUrl(value), {
      message: "Enter a valid public https:// URL, or leave this blank.",
    }),
});

type FormValues = z.infer<typeof formSchema>;

interface CreateRegulatoryDocumentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (documentId: string) => void;
}

export function CreateRegulatoryDocumentDialog({
  open,
  onOpenChange,
  onCreated,
}: CreateRegulatoryDocumentDialogProps) {
  const { data: bodies, isLoading: loadingBodies } = useRegulatoryBodies();
  const createDocument = useCreateRegulatoryDocument();

  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      regulatoryBodyId: "",
      title: "",
      version: "",
      sourceUrl: "",
    },
  });

  React.useEffect(() => {
    if (!open) {
      form.reset();
    }
  }, [open, form]);

  async function onSubmit(values: FormValues) {
    try {
      const created = await createDocument.mutateAsync({
        regulatoryBodyId: values.regulatoryBodyId,
        title: values.title,
        version: values.version,
        sourceUrl: values.sourceUrl || undefined,
      });
      toast.success("Regulation created", {
        description: `"${created.title}" has been created.`,
      });
      onOpenChange(false);
      onCreated(created.id);
    } catch (error: unknown) {
      let message = "Failed to create regulation";
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
          <DialogTitle>Add Document</DialogTitle>
          <DialogDescription>
            Create a new regulatory document. You can upload or paste a source
            document and trigger ingestion afterwards from its detail page.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="regulatoryBodyId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Regulatory Body *</FormLabel>
                  {loadingBodies ? (
                    <Skeleton className="h-9 w-full" />
                  ) : (
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select a regulatory body" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {(bodies ?? []).map((body) => (
                          <SelectItem key={body.id} value={body.id}>
                            {body.code} — {body.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="title"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Title *</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g., Draft National Standards for Home Support Services" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="version"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Version *</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g., 1.0" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="sourceUrl"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Source URL</FormLabel>
                  <FormControl>
                    <Input placeholder="https://example.com/regulation.pdf" {...field} />
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
                disabled={createDocument.isPending}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={createDocument.isPending}>
                {createDocument.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Creating...
                  </>
                ) : (
                  "Create Regulation"
                )}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
