"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { useCreateEmployeeForUser } from "@/lib/api/admin/use-users";
import { useSites } from "@/lib/api/admin/use-sites";
import { useAllDepartments } from "@/lib/api/admin/use-departments";
import { toast } from "sonner";
import type { User } from "@/lib/api/admin/users";

const formSchema = z.object({
  employeeCode: z.string().max(50).optional(),
  phone: z.string().max(50).optional(),
  mobile: z.string().max(50).optional(),
  jobTitle: z.string().max(100).optional(),
  departmentId: z.string().optional(),
  primarySiteId: z.string().optional(),
});

type FormValues = z.infer<typeof formSchema>;

interface CreateEmployeeForUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  user: User;
}

export function CreateEmployeeForUserDialog({
  open,
  onOpenChange,
  user,
}: CreateEmployeeForUserDialogProps) {
  const createEmployeeForUser = useCreateEmployeeForUser();
  const { data: sitesData, isLoading: loadingSites } = useSites();
  const sites = sitesData?.items ?? [];
  const { data: departments, isLoading: loadingDepartments } = useAllDepartments();

  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      employeeCode: "",
      phone: "",
      mobile: "",
      jobTitle: "",
      departmentId: "",
      primarySiteId: "",
    },
  });

  // Reset form when dialog closes
  React.useEffect(() => {
    if (!open) {
      form.reset();
    }
  }, [open, form]);

  async function onSubmit(values: FormValues) {
    try {
      await createEmployeeForUser.mutateAsync({
        userId: user.id,
        data: {
          employeeCode: values.employeeCode?.trim() || undefined,
          phone: values.phone || undefined,
          mobile: values.mobile || undefined,
          jobTitle: values.jobTitle || undefined,
          departmentId: values.departmentId || undefined,
          primarySiteId: values.primarySiteId || undefined,
        },
      });
      toast.success("Employee record created", {
        description: `Employee record has been created for ${user.fullName}`,
      });
      onOpenChange(false);
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error("Failed to create employee", {
        description: message,
      });
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Create Employee Record</DialogTitle>
          <DialogDescription>
            Create an employee record for {user.fullName}. Name and email will be
            copied from the user account.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form
            onSubmit={(e) => {
              // Radix Dialog portals this form outside the parent page's <form> in the
              // DOM, but React still bubbles the synthetic submit event through the
              // component tree. Without stopping it here, a validation failure (or
              // success) in this dialog also submits the parent user-edit form.
              e.stopPropagation();
              void form.handleSubmit(onSubmit)(e);
            }}
            className="space-y-4"
          >
            {/* User Info */}
            <div className="rounded-lg border p-4 bg-muted/50">
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div className="text-muted-foreground">Name:</div>
                <div className="font-medium">{user.fullName}</div>
                <div className="text-muted-foreground">Email:</div>
                <div className="font-medium">{user.email}</div>
              </div>
            </div>

            <FormField
              control={form.control}
              name="employeeCode"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Employee Code</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g., EMP001 (auto-generated if left blank)" {...field} />
                  </FormControl>
                  <FormDescription>
                    Leave blank to auto-generate (e.g., EMP001). Enter a value to use a code from a legacy system.
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="jobTitle"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Job Title</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., Site Manager" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="departmentId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Department</FormLabel>
                    <Select
                      onValueChange={(value) => field.onChange(value === "__none__" ? "" : value)}
                      value={field.value || "__none__"}
                      disabled={loadingDepartments}
                    >
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder={loadingDepartments ? "Loading departments..." : "Select a department"} />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="__none__">None</SelectItem>
                        {departments?.filter((d) => d.isActive).map((department) => (
                          <SelectItem key={department.id} value={department.id}>
                            {department.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="phone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Phone</FormLabel>
                    <FormControl>
                      <Input placeholder="Phone number" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="mobile"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Mobile</FormLabel>
                    <FormControl>
                      <Input placeholder="Mobile number" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="primarySiteId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Primary Site</FormLabel>
                  <Select
                    onValueChange={(value) => field.onChange(value === "__none__" ? "" : value)}
                    value={field.value || "__none__"}
                    disabled={loadingSites}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select a site (optional)" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      <SelectItem value="__none__">None</SelectItem>
                      {sites.map((site) => (
                        <SelectItem key={site.id} value={site.id}>
                          {site.siteName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter className="pt-4">
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
                disabled={createEmployeeForUser.isPending}
              >
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={createEmployeeForUser.isPending}
              >
                {createEmployeeForUser.isPending ? (
                  <>
                    <LoadingSpinner className="mr-2 h-4 w-4" />
                    Creating...
                  </>
                ) : (
                  "Create Employee"
                )}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
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
