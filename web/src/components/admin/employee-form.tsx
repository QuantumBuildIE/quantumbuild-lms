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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useCreateEmployee, useUpdateEmployee } from "@/lib/api/admin/use-employees";
import { useAllSites } from "@/lib/api/admin/use-sites";
import type { Employee } from "@/types/admin";
import { toast } from "sonner";
import { Info, User as UserIcon } from "lucide-react";
import { EmployeeUserAccountSection } from "./employee-user-account-section";

const AVAILABLE_ROLES = [
  { value: "SiteManager", label: "Site Manager" },
  { value: "WarehouseStaff", label: "Warehouse Staff" },
  { value: "OfficeStaff", label: "Office Staff" },
  { value: "Finance", label: "Finance" },
  { value: "Admin", label: "Admin" },
] as const;

// Supported languages for Toolbox Talk subtitles
// Most common construction worker languages in Ireland/UK listed first
const SUPPORTED_LANGUAGES: Array<{ code: string; name: string; nativeName?: string }> = [
  { code: "en", name: "English" },
  { code: "pl", name: "Polish", nativeName: "Polski" },
  { code: "ro", name: "Romanian", nativeName: "Română" },
  { code: "lt", name: "Lithuanian", nativeName: "Lietuvių" },
  { code: "pt", name: "Portuguese", nativeName: "Português" },
  { code: "es", name: "Spanish", nativeName: "Español" },
  { code: "lv", name: "Latvian", nativeName: "Latviešu" },
  { code: "ru", name: "Russian", nativeName: "Русский" },
  { code: "uk", name: "Ukrainian", nativeName: "Українська" },
  { code: "bg", name: "Bulgarian", nativeName: "Български" },
  { code: "cs", name: "Czech", nativeName: "Čeština" },
  { code: "hr", name: "Croatian", nativeName: "Hrvatski" },
  { code: "hu", name: "Hungarian", nativeName: "Magyar" },
  { code: "sk", name: "Slovak", nativeName: "Slovenčina" },
  { code: "ar", name: "Arabic", nativeName: "العربية" },
  { code: "zh", name: "Chinese", nativeName: "中文" },
  { code: "da", name: "Danish", nativeName: "Dansk" },
  { code: "nl", name: "Dutch", nativeName: "Nederlands" },
  { code: "fi", name: "Finnish", nativeName: "Suomi" },
  { code: "fr", name: "French", nativeName: "Français" },
  { code: "de", name: "German", nativeName: "Deutsch" },
  { code: "el", name: "Greek", nativeName: "Ελληνικά" },
  { code: "hi", name: "Hindi", nativeName: "हिन्दी" },
  { code: "it", name: "Italian", nativeName: "Italiano" },
  { code: "ja", name: "Japanese", nativeName: "日本語" },
  { code: "ko", name: "Korean", nativeName: "한국어" },
  { code: "no", name: "Norwegian", nativeName: "Norsk" },
  { code: "sv", name: "Swedish", nativeName: "Svenska" },
  { code: "tr", name: "Turkish", nativeName: "Türkçe" },
  { code: "vi", name: "Vietnamese", nativeName: "Tiếng Việt" },
  { code: "af", name: "Afrikaans", nativeName: "Afrikaans" },
];

const employeeFormSchema = z.object({
  // Employee code is auto-generated on create, read-only on edit
  employeeCode: z.string().max(50).optional().nullable(),
  firstName: z.string().min(1, "First name is required").max(100),
  lastName: z.string().min(1, "Last name is required").max(100),
  email: z.string().email("Invalid email").max(200).optional().nullable().or(z.literal("")),
  phone: z.string().max(50).optional().nullable(),
  mobile: z.string().max(50).optional().nullable(),
  jobTitle: z.string().max(100).optional().nullable(),
  department: z.string().max(100).optional().nullable(),
  primarySiteId: z.string().optional().nullable(),
  startDate: z.string().optional().nullable(),
  endDate: z.string().optional().nullable(),
  notes: z.string().max(2000).optional().nullable(),
  isActive: z.boolean(),
  createUserAccount: z.boolean(),
  userRole: z.string().optional().nullable(),
  preferredLanguage: z.string().default("en"),
}).refine((data) => {
  if (data.startDate && data.endDate) {
    return new Date(data.endDate) > new Date(data.startDate);
  }
  return true;
}, {
  message: "End date must be after start date",
  path: ["endDate"],
});

type EmployeeFormValues = z.infer<typeof employeeFormSchema>;

interface EmployeeFormProps {
  employee?: Employee;
  onSuccess?: () => void;
  onCancel?: () => void;
}

export function EmployeeForm({ employee, onSuccess, onCancel }: EmployeeFormProps) {
  const isEditing = !!employee;

  const createEmployee = useCreateEmployee();
  const updateEmployee = useUpdateEmployee();
  const { data: sites, isLoading: sitesLoading } = useAllSites();

  const form = useForm<EmployeeFormValues>({
    resolver: zodResolver(employeeFormSchema) as any,
    defaultValues: {
      employeeCode: employee?.employeeCode ?? "",
      firstName: employee?.firstName ?? "",
      lastName: employee?.lastName ?? "",
      email: employee?.email ?? "",
      phone: employee?.phone ?? "",
      mobile: employee?.mobile ?? "",
      jobTitle: employee?.jobTitle ?? "",
      department: employee?.department ?? "",
      primarySiteId: employee?.primarySiteId ?? "",
      startDate: employee?.startDate ? employee.startDate.split("T")[0] : "",
      endDate: employee?.endDate ? employee.endDate.split("T")[0] : "",
      notes: employee?.notes ?? "",
      isActive: employee?.isActive ?? true,
      createUserAccount: !isEditing, // Only default to true for new employees
      userRole: "SiteManager", // Default role
      preferredLanguage: employee?.preferredLanguage ?? "en",
    },
  });

  // Watch the createUserAccount and email fields for conditional rendering
  const watchCreateUserAccount = form.watch("createUserAccount");
  const watchEmail = form.watch("email");

  const isSubmitting = createEmployee.isPending || updateEmployee.isPending;

  async function onSubmit(values: EmployeeFormValues) {
    try {
      // Clean up empty strings to null/undefined for optional fields
      const cleanedValues = {
        ...values,
        email: values.email || undefined,
        phone: values.phone || undefined,
        mobile: values.mobile || undefined,
        jobTitle: values.jobTitle || undefined,
        department: values.department || undefined,
        primarySiteId: values.primarySiteId || undefined,
        startDate: values.startDate || undefined,
        endDate: values.endDate || undefined,
        notes: values.notes || undefined,
      };

      if (isEditing) {
        await updateEmployee.mutateAsync({
          id: employee.id,
          data: {
            employeeCode: cleanedValues.employeeCode ?? '',
            firstName: cleanedValues.firstName,
            lastName: cleanedValues.lastName,
            email: cleanedValues.email,
            phone: cleanedValues.phone,
            mobile: cleanedValues.mobile,
            jobTitle: cleanedValues.jobTitle,
            department: cleanedValues.department,
            primarySiteId: cleanedValues.primarySiteId || null,
            startDate: cleanedValues.startDate || null,
            endDate: cleanedValues.endDate || null,
            notes: cleanedValues.notes,
            isActive: cleanedValues.isActive,
            preferredLanguage: values.preferredLanguage,
          },
        });
        toast.success("Employee updated successfully");
      } else {
        const hasEmail = !!cleanedValues.email;
        const willCreateUser = values.createUserAccount && hasEmail;

        // Note: employeeCode is auto-generated on the backend, so we don't send it
        await createEmployee.mutateAsync({
          firstName: cleanedValues.firstName,
          lastName: cleanedValues.lastName,
          email: cleanedValues.email,
          phone: cleanedValues.phone,
          mobile: cleanedValues.mobile,
          jobTitle: cleanedValues.jobTitle,
          department: cleanedValues.department,
          primarySiteId: cleanedValues.primarySiteId,
          startDate: cleanedValues.startDate,
          endDate: cleanedValues.endDate,
          notes: cleanedValues.notes,
          isActive: cleanedValues.isActive,
          createUserAccount: values.createUserAccount,
          userRole: values.userRole || undefined,
          preferredLanguage: values.preferredLanguage,
        });

        if (willCreateUser) {
          toast.success("Employee created successfully", {
            description: "A password setup email has been sent to the employee.",
          });
        } else {
          toast.success("Employee created successfully");
        }
      }
      onSuccess?.();
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error(isEditing ? "Failed to update employee" : "Failed to create employee", {
        description: message,
      });
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        {/* Employee Code - only show in edit mode, read-only */}
        {isEditing && (
          <FormField
            control={form.control}
            name="employeeCode"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Employee Code</FormLabel>
                <FormControl>
                  <Input
                    {...field}
                    value={field.value ?? ""}
                    disabled
                    className="bg-muted"
                  />
                </FormControl>
                <FormDescription>
                  Auto-generated employee identifier (read-only)
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        )}

        <div className="grid gap-6 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="jobTitle"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Job Title</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., Site Manager" {...field} value={field.value ?? ""} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="department"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Department</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., Operations" {...field} value={field.value ?? ""} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        <div className="grid gap-6 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="firstName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>First Name *</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., John" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="lastName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Last Name *</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., Smith" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Email</FormLabel>
              <FormControl>
                <Input
                  type="email"
                  placeholder="e.g., john.smith@example.com"
                  {...field}
                  value={field.value ?? ""}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        {/* User Account Section - Only show for new employees without existing user account */}
        {!isEditing && (
          <div className="rounded-lg border bg-muted/50 p-4 space-y-4">
            <div className="flex items-center gap-2">
              <UserIcon className="h-5 w-5 text-muted-foreground" />
              <h3 className="font-medium">User Account</h3>
            </div>

            <FormField
              control={form.control}
              name="createUserAccount"
              render={({ field }) => (
                <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                  <FormControl>
                    <Checkbox
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      disabled={!watchEmail}
                    />
                  </FormControl>
                  <div className="space-y-1 leading-none">
                    <FormLabel>Create login account</FormLabel>
                    <FormDescription>
                      {watchEmail
                        ? "Create a user account so this employee can log in to the system"
                        : "Enter an email address to enable this option"}
                    </FormDescription>
                  </div>
                </FormItem>
              )}
            />

            {watchCreateUserAccount && watchEmail && (
              <>
                <FormField
                  control={form.control}
                  name="userRole"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>User Role</FormLabel>
                      <Select
                        onValueChange={field.onChange}
                        value={field.value || "SiteManager"}
                      >
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue placeholder="Select a role" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {AVAILABLE_ROLES.map((role) => (
                            <SelectItem key={role.value} value={role.value}>
                              {role.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormDescription>
                        The role determines what permissions the user will have
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <div className="flex items-start gap-2 rounded-md bg-blue-50 border border-blue-200 p-3 text-sm text-blue-800 dark:bg-blue-950 dark:border-blue-900 dark:text-blue-300">
                  <Info className="h-4 w-4 mt-0.5 shrink-0" />
                  <div>
                    <p className="font-medium">A login account will be created</p>
                    <p className="text-blue-700 dark:text-blue-400">
                      The employee will receive an email at <strong>{watchEmail}</strong> with instructions to set up their password.
                    </p>
                  </div>
                </div>
              </>
            )}
          </div>
        )}

        {/* User Account Section - Show for existing employees to manage linkage */}
        {isEditing && employee && (
          <EmployeeUserAccountSection employee={employee} />
        )}

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
            name="mobile"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Mobile</FormLabel>
                <FormControl>
                  <Input placeholder="e.g., +353 87 123 4567" {...field} value={field.value ?? ""} />
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
              <FormLabel>Default Site</FormLabel>
              <Select
                onValueChange={(value) => field.onChange(value === "__none__" ? "" : value)}
                value={field.value || "__none__"}
                disabled={sitesLoading}
              >
                <FormControl>
                  <SelectTrigger>
                    <SelectValue placeholder={sitesLoading ? "Loading sites..." : "Select a site"} />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  <SelectItem value="__none__">None</SelectItem>
                  {sites?.filter(s => s.isActive).map((site) => (
                    <SelectItem key={site.id} value={site.id}>
                      {site.siteName} ({site.siteCode})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FormDescription>
                The primary site where this employee is based
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="preferredLanguage"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Preferred Language</FormLabel>
              <Select
                onValueChange={field.onChange}
                value={field.value || "en"}
              >
                <FormControl>
                  <SelectTrigger>
                    <SelectValue placeholder="Select a language" />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  {SUPPORTED_LANGUAGES.map((lang) => (
                    <SelectItem key={lang.code} value={lang.code}>
                      {lang.name}
                      {lang.nativeName && ` (${lang.nativeName})`}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FormDescription>
                Used for Toolbox Talk video subtitles and notifications
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="grid gap-6 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="startDate"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Start Date</FormLabel>
                <FormControl>
                  <Input type="date" {...field} value={field.value ?? ""} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="endDate"
            render={({ field }) => (
              <FormItem>
                <FormLabel>End Date</FormLabel>
                <FormControl>
                  <Input type="date" {...field} value={field.value ?? ""} />
                </FormControl>
                <FormDescription>Leave blank if still employed</FormDescription>
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
                  placeholder="Additional notes about this employee"
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
                  Inactive employees won&apos;t appear in selection dropdowns
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
              "Update Employee"
            ) : (
              "Create Employee"
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

