"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
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
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Separator } from "@/components/ui/separator";
import { Combobox } from "@/components/ui/combobox";
import { useCreateUser, useUpdateUser, type User } from "@/lib/api/admin/use-users";
import type { CreateUserDto, EmployeeLinkOption } from "@/lib/api/admin/users";
import { useRoles, type Role } from "@/lib/api/admin/use-roles";
import { useUnlinkedEmployees } from "@/lib/api/admin/use-employees";
import { useAllSites } from "@/lib/api/admin/use-sites";
import { toast } from "sonner";
import { cn, getApiErrorMessage } from "@/lib/utils";
import { LookupField } from "./lookup-field";
import { UserEmployeeRecordSection } from "./user-employee-record-section";

// Password strength calculation
function calculatePasswordStrength(password: string): {
  score: number;
  label: string;
  color: string;
} {
  let score = 0;
  if (password.length >= 8) score++;
  if (password.length >= 12) score++;
  if (/[A-Z]/.test(password)) score++;
  if (/[a-z]/.test(password)) score++;
  if (/[0-9]/.test(password)) score++;
  if (/[^A-Za-z0-9]/.test(password)) score++;

  if (score <= 2) return { score, label: "Weak", color: "bg-red-500" };
  if (score <= 4) return { score, label: "Medium", color: "bg-yellow-500" };
  return { score, label: "Strong", color: "bg-green-500" };
}

const createUserSchema = z
  .object({
    email: z.string().min(1, "Email is required").email("Invalid email address"),
    firstName: z.string().min(1, "First name is required").max(100),
    lastName: z.string().min(1, "Last name is required").max(100),
    password: z
      .string()
      .min(8, "Password must be at least 8 characters")
      .regex(/[A-Z]/, "Password must contain at least one uppercase letter")
      .regex(/[^A-Za-z0-9]/, "Password must contain at least one special character"),
    confirmPassword: z.string().min(1, "Please confirm your password"),
    roleIds: z.array(z.string()).min(1, "At least one role is required"),
    isActive: z.boolean(),
    // Employee linking
    employeeLinkOption: z.enum(["None", "LinkExisting", "CreateNew"]),
    existingEmployeeId: z.string().optional(),
    newEmployeeCode: z.string().max(50).optional(),
    newEmployeePhone: z.string().max(50).optional(),
    newEmployeeMobile: z.string().max(50).optional(),
    newEmployeeJobTitle: z.string().max(100).optional(),
    newEmployeeDepartment: z.string().max(100).optional(),
    newEmployeePrimarySiteId: z.string().optional(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  })
  .refine(
    (data) => {
      if (data.employeeLinkOption === "LinkExisting") {
        return !!data.existingEmployeeId;
      }
      return true;
    },
    {
      message: "Please select an employee to link",
      path: ["existingEmployeeId"],
    }
  )
  .refine(
    (data) => {
      if (data.employeeLinkOption === "CreateNew") {
        return !!data.newEmployeeCode && data.newEmployeeCode.trim().length > 0;
      }
      return true;
    },
    {
      message: "Employee code is required",
      path: ["newEmployeeCode"],
    }
  );

const updateUserSchema = z.object({
  email: z.string().email("Invalid email address"),
  firstName: z.string().min(1, "First name is required").max(100),
  lastName: z.string().min(1, "Last name is required").max(100),
  roleIds: z.array(z.string()).min(1, "At least one role is required"),
  isActive: z.boolean(),
});

type CreateUserFormValues = z.infer<typeof createUserSchema>;
type UpdateUserFormValues = z.infer<typeof updateUserSchema>;

interface UserFormProps {
  user?: User;
  onSuccess?: () => void;
  onCancel?: () => void;
}

export function UserForm({ user, onSuccess, onCancel }: UserFormProps) {
  const isEditing = !!user;

  const createUser = useCreateUser();
  const updateUser = useUpdateUser();
  const { data: roles, isLoading: rolesLoading } = useRoles();
  const { data: unlinkedEmployees, isLoading: employeesLoading } = useUnlinkedEmployees();
  const { data: sites, isLoading: sitesLoading } = useAllSites();

  const form = useForm<CreateUserFormValues | UpdateUserFormValues>({
    resolver: zodResolver(isEditing ? updateUserSchema : createUserSchema) as any,
    defaultValues: {
      email: user?.email ?? "",
      firstName: user?.firstName ?? "",
      lastName: user?.lastName ?? "",
      ...(isEditing ? {} : {
        password: "",
        confirmPassword: "",
        employeeLinkOption: "None" as const,
        existingEmployeeId: "",
        newEmployeeCode: "",
        newEmployeePhone: "",
        newEmployeeMobile: "",
        newEmployeeJobTitle: "",
        newEmployeeDepartment: "",
        newEmployeePrimarySiteId: "",
      }),
      roleIds: user?.roles.map((r) => r.id) ?? [],
      isActive: user?.isActive ?? true,
    },
  });

  // Only watch password for create mode
  const watchedPassword = !isEditing ? form.watch("password") : undefined;
  const passwordStrength = watchedPassword && typeof watchedPassword === "string"
    ? calculatePasswordStrength(watchedPassword)
    : null;

  const watchEmployeeLinkOption = !isEditing
    ? (form.watch as any)("employeeLinkOption") as string
    : "None";

  const isSubmitting = createUser.isPending || updateUser.isPending;

  async function onSubmit(values: CreateUserFormValues | UpdateUserFormValues) {
    try {
      if (isEditing) {
        const updateValues = values as UpdateUserFormValues;
        await updateUser.mutateAsync({
          id: user.id,
          data: {
            firstName: updateValues.firstName,
            lastName: updateValues.lastName,
            isActive: updateValues.isActive,
            roleIds: updateValues.roleIds,
          },
        });
        toast.success("User updated successfully");
      } else {
        const createValues = values as CreateUserFormValues;
        const linkOption = createValues.employeeLinkOption as EmployeeLinkOption;

        const payload: CreateUserDto = {
          email: createValues.email,
          firstName: createValues.firstName,
          lastName: createValues.lastName,
          password: createValues.password,
          confirmPassword: createValues.confirmPassword,
          isActive: createValues.isActive,
          roleIds: createValues.roleIds,
          employeeLinkOption: linkOption,
        };

        if (linkOption === "LinkExisting" && createValues.existingEmployeeId) {
          payload.existingEmployeeId = createValues.existingEmployeeId;
        } else if (linkOption === "CreateNew") {
          payload.newEmployee = {
            employeeCode: createValues.newEmployeeCode!,
            phone: createValues.newEmployeePhone || undefined,
            mobile: createValues.newEmployeeMobile || undefined,
            jobTitle: createValues.newEmployeeJobTitle || undefined,
            department: createValues.newEmployeeDepartment || undefined,
            primarySiteId: createValues.newEmployeePrimarySiteId || undefined,
          };
        }

        await createUser.mutateAsync(payload);

        if (linkOption !== "None") {
          toast.success("User created and linked to employee");
        } else {
          toast.success("User created successfully");
        }
      }
      onSuccess?.();
    } catch (error: unknown) {
      toast.error(isEditing ? "Failed to update user" : "Failed to create user", {
        description: getApiErrorMessage(error),
      });
    }
  }

  const handleRoleToggle = (roleId: string, checked: boolean) => {
    const currentRoles = form.getValues("roleIds") as string[];
    if (checked) {
      form.setValue("roleIds", [...currentRoles, roleId], { shouldValidate: true });
    } else {
      form.setValue(
        "roleIds",
        currentRoles.filter((id) => id !== roleId),
        { shouldValidate: true }
      );
    }
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Email *</FormLabel>
              <FormControl>
                <Input
                  type="email"
                  placeholder="e.g., john.smith@example.com"
                  disabled={isEditing}
                  {...field}
                />
              </FormControl>
              {isEditing && (
                <FormDescription>Email cannot be changed after creation</FormDescription>
              )}
              <FormMessage />
            </FormItem>
          )}
        />

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

        {!isEditing && (
          <>
            <FormField
              control={form.control}
              name="password"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Password *</FormLabel>
                  <FormControl>
                    <Input type="password" placeholder="Enter password" {...field} />
                  </FormControl>
                  {passwordStrength && (
                    <div className="mt-2 space-y-1">
                      <div className="flex items-center gap-2">
                        <div className="h-2 flex-1 rounded-full bg-muted overflow-hidden">
                          <div
                            className={cn(
                              "h-full transition-all",
                              passwordStrength.color
                            )}
                            style={{ width: `${(passwordStrength.score / 6) * 100}%` }}
                          />
                        </div>
                        <span
                          className={cn(
                            "text-xs font-medium",
                            passwordStrength.label === "Weak" && "text-red-500",
                            passwordStrength.label === "Medium" && "text-yellow-500",
                            passwordStrength.label === "Strong" && "text-green-500"
                          )}
                        >
                          {passwordStrength.label}
                        </span>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        Required: 8+ characters, at least one uppercase letter (A-Z), and one special character (!@#$%^&* etc.)
                      </p>
                    </div>
                  )}
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="confirmPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Confirm Password *</FormLabel>
                  <FormControl>
                    <Input type="password" placeholder="Confirm password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </>
        )}

        <FormField
          control={form.control}
          name="roleIds"
          render={() => (
            <FormItem>
              <FormLabel>Roles *</FormLabel>
              <FormDescription>Select one or more roles for this user</FormDescription>
              <div className="mt-2 space-y-3 rounded-md border p-4">
                {rolesLoading ? (
                  <div className="animate-pulse text-sm text-muted-foreground">
                    Loading roles...
                  </div>
                ) : roles && roles.length > 0 ? (
                  roles.map((role: Role) => {
                    const currentRoles = form.watch("roleIds") as string[];
                    const isChecked = currentRoles.includes(role.id);
                    return (
                      <div
                        key={role.id}
                        className="flex items-start space-x-3 space-y-0"
                      >
                        <Checkbox
                          id={`role-${role.id}`}
                          checked={isChecked}
                          onCheckedChange={(checked) =>
                            handleRoleToggle(role.id, checked as boolean)
                          }
                        />
                        <div className="space-y-1 leading-none">
                          <label
                            htmlFor={`role-${role.id}`}
                            className="text-sm font-medium leading-none cursor-pointer"
                          >
                            {role.name}
                          </label>
                          {role.description && (
                            <p className="text-xs text-muted-foreground">
                              {role.description}
                            </p>
                          )}
                        </div>
                      </div>
                    );
                  })
                ) : (
                  <div className="text-sm text-muted-foreground">No roles available</div>
                )}
              </div>
              <FormMessage />
            </FormItem>
          )}
        />

        {!isEditing && (
          <>
            <Separator />

            <div className="space-y-4">
              <div>
                <h3 className="text-lg font-medium">Employee Record</h3>
                <p className="text-sm text-muted-foreground">
                  Optionally link this user to an employee record for attendance tracking and other employee features.
                </p>
              </div>

              <FormField
                control={form.control}
                name={"employeeLinkOption" as any}
                render={({ field }) => (
                  <FormItem>
                    <FormControl>
                      <RadioGroup
                        onValueChange={field.onChange}
                        value={field.value as string}
                        className="space-y-3"
                      >
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="None" id="emp-none" />
                          <label htmlFor="emp-none" className="text-sm font-medium cursor-pointer">
                            No employee record
                          </label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="LinkExisting" id="emp-link" />
                          <label htmlFor="emp-link" className="text-sm font-medium cursor-pointer">
                            Link to existing employee
                          </label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="CreateNew" id="emp-create" />
                          <label htmlFor="emp-create" className="text-sm font-medium cursor-pointer">
                            Create new employee
                          </label>
                        </div>
                      </RadioGroup>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {watchEmployeeLinkOption === "LinkExisting" && (
                <div className="ml-6 space-y-3">
                  <FormField
                    control={form.control}
                    name={"existingEmployeeId" as any}
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Select Employee *</FormLabel>
                        <Combobox
                          options={(unlinkedEmployees ?? []).map((emp) => ({
                            value: emp.id,
                            label: `${emp.firstName} ${emp.lastName}`,
                            description: `${emp.employeeCode}${emp.email ? ` - ${emp.email}` : ""}`,
                          }))}
                          value={field.value as string}
                          onValueChange={(val) => field.onChange(val)}
                          placeholder="Search employees..."
                          searchPlaceholder="Type name or code..."
                          emptyText="No unlinked employees found"
                          isLoading={employeesLoading}
                          allowCustomValue={false}
                        />
                        <FormDescription>
                          Only employees without a linked user account are shown
                        </FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
              )}

              {watchEmployeeLinkOption === "CreateNew" && (
                <div className="ml-6 rounded-lg border bg-muted/30 p-4 space-y-4">
                  <p className="text-sm text-muted-foreground">
                    Name and email will be copied from the user account details above.
                  </p>

                  <FormField
                    control={form.control}
                    name={"newEmployeeCode" as any}
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Employee Code *</FormLabel>
                        <FormControl>
                          <Input placeholder="e.g., EMP001" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <div className="grid gap-4 sm:grid-cols-2">
                    <FormField
                      control={form.control}
                      name={"newEmployeeJobTitle" as any}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Job Title</FormLabel>
                          <FormControl>
                            <LookupField
                              categoryName="JobTitle"
                              value={field.value ?? ""}
                              onChange={field.onChange}
                              placeholder="Select job title..."
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    <FormField
                      control={form.control}
                      name={"newEmployeeDepartment" as any}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Department</FormLabel>
                          <FormControl>
                            <LookupField
                              categoryName="Department"
                              value={field.value ?? ""}
                              onChange={field.onChange}
                              placeholder="Select department..."
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>

                  <div className="grid gap-4 sm:grid-cols-2">
                    <FormField
                      control={form.control}
                      name={"newEmployeePhone" as any}
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
                      name={"newEmployeeMobile" as any}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Mobile</FormLabel>
                          <FormControl>
                            <Input placeholder="e.g., +353 87 123 4567" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>

                  <FormField
                    control={form.control}
                    name={"newEmployeePrimarySiteId" as any}
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Default Site</FormLabel>
                        <Select
                          onValueChange={(value) => field.onChange(value === "__none__" ? "" : value)}
                          value={(field.value as string) || "__none__"}
                          disabled={sitesLoading}
                        >
                          <FormControl>
                            <SelectTrigger>
                              <SelectValue placeholder={sitesLoading ? "Loading sites..." : "Select a site"} />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            <SelectItem value="__none__">None</SelectItem>
                            {sites?.filter((s) => s.isActive).map((site) => (
                              <SelectItem key={site.id} value={site.id}>
                                {site.siteName} ({site.siteCode})
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                </div>
              )}
            </div>
          </>
        )}

        <FormField
          control={form.control}
          name="isActive"
          render={({ field }) => (
            <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
              <FormControl>
                <Checkbox checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
              <div className="space-y-1 leading-none">
                <FormLabel>Active</FormLabel>
                <FormDescription>
                  Inactive users cannot log in to the system
                </FormDescription>
              </div>
            </FormItem>
          )}
        />

        {/* Employee Record Section - Show for existing users to manage linkage */}
        {isEditing && user && (
          <UserEmployeeRecordSection user={user} />
        )}

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
              "Update User"
            ) : (
              "Create User"
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
