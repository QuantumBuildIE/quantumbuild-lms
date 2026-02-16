"use client";

import * as React from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
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
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useCreateCompany, useUpdateCompany, type CompanyWithContacts } from "@/lib/api/admin/use-companies";
import { useCreateCompanyContact, useUpdateCompanyContact, useDeleteCompanyContact } from "@/lib/api/admin/use-contacts";
import { toast } from "sonner";
import { Plus, Trash2, Star } from "lucide-react";

const contactSchema = z.object({
  id: z.string().optional(),
  firstName: z.string().min(1, "First name is required").max(100),
  lastName: z.string().min(1, "Last name is required").max(100),
  jobTitle: z.string().max(100).optional().nullable(),
  email: z.string().email("Invalid email").max(200).optional().nullable().or(z.literal("")),
  phone: z.string().max(50).optional().nullable(),
  mobile: z.string().max(50).optional().nullable(),
  isPrimaryContact: z.boolean(),
  isActive: z.boolean(),
  notes: z.string().max(2000).optional().nullable(),
  _isNew: z.boolean().optional(),
  _isDeleted: z.boolean().optional(),
});

const companyFormSchema = z.object({
  companyCode: z.string().min(1, "Company code is required").max(50),
  companyName: z.string().min(1, "Company name is required").max(200),
  tradingName: z.string().max(200).optional().nullable(),
  registrationNumber: z.string().max(50).optional().nullable(),
  vatNumber: z.string().max(50).optional().nullable(),
  addressLine1: z.string().max(200).optional().nullable(),
  addressLine2: z.string().max(200).optional().nullable(),
  city: z.string().max(100).optional().nullable(),
  county: z.string().max(100).optional().nullable(),
  postalCode: z.string().max(20).optional().nullable(),
  country: z.string().max(100).optional().nullable(),
  phone: z.string().max(50).optional().nullable(),
  email: z.string().email("Invalid email").max(200).optional().nullable().or(z.literal("")),
  website: z.string().max(200).optional().nullable(),
  companyType: z.string().max(50).optional().nullable(),
  notes: z.string().max(2000).optional().nullable(),
  isActive: z.boolean(),
  contacts: z.array(contactSchema),
});

type CompanyFormValues = z.infer<typeof companyFormSchema>;

const COMPANY_TYPES = [
  "Client",
  "Supplier",
  "Subcontractor",
  "Partner",
  "Other",
];

interface CompanyFormProps {
  company?: CompanyWithContacts;
  onSuccess?: () => void;
  onCancel?: () => void;
}

export function CompanyForm({ company, onSuccess, onCancel }: CompanyFormProps) {
  const isEditing = !!company;

  const createCompany = useCreateCompany();
  const updateCompany = useUpdateCompany();
  const createContact = useCreateCompanyContact(company?.id ?? "");
  const updateContact = useUpdateCompanyContact(company?.id ?? "");
  const deleteContact = useDeleteCompanyContact(company?.id ?? "");

  const form = useForm<CompanyFormValues>({
    resolver: zodResolver(companyFormSchema) as any,
    defaultValues: {
      companyCode: company?.companyCode ?? "",
      companyName: company?.companyName ?? "",
      tradingName: company?.tradingName ?? "",
      registrationNumber: company?.registrationNumber ?? "",
      vatNumber: company?.vatNumber ?? "",
      addressLine1: company?.addressLine1 ?? "",
      addressLine2: company?.addressLine2 ?? "",
      city: company?.city ?? "",
      county: company?.county ?? "",
      postalCode: company?.postalCode ?? "",
      country: company?.country ?? "Ireland",
      phone: company?.phone ?? "",
      email: company?.email ?? "",
      website: company?.website ?? "",
      companyType: company?.companyType ?? "",
      notes: company?.notes ?? "",
      isActive: company?.isActive ?? true,
      contacts: company?.contacts?.map(c => ({
        id: c.id,
        firstName: c.firstName,
        lastName: c.lastName,
        jobTitle: c.jobTitle ?? "",
        email: c.email ?? "",
        phone: c.phone ?? "",
        mobile: c.mobile ?? "",
        isPrimaryContact: c.isPrimaryContact,
        isActive: c.isActive,
        notes: "",
        _isNew: false,
        _isDeleted: false,
      })) ?? [],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: "contacts",
  });

  const isSubmitting = createCompany.isPending || updateCompany.isPending;

  const addContact = () => {
    append({
      firstName: "",
      lastName: "",
      jobTitle: "",
      email: "",
      phone: "",
      mobile: "",
      isPrimaryContact: fields.length === 0,
      isActive: true,
      notes: "",
      _isNew: true,
      _isDeleted: false,
    });
  };

  const setPrimaryContact = (index: number) => {
    const contacts = form.getValues("contacts");
    contacts.forEach((_, i) => {
      form.setValue(`contacts.${i}.isPrimaryContact`, i === index);
    });
  };

  async function onSubmit(values: CompanyFormValues) {
    try {
      // Clean up empty strings to null/undefined for optional fields
      const cleanedCompanyValues = {
        companyCode: values.companyCode,
        companyName: values.companyName,
        tradingName: values.tradingName || undefined,
        registrationNumber: values.registrationNumber || undefined,
        vatNumber: values.vatNumber || undefined,
        addressLine1: values.addressLine1 || undefined,
        addressLine2: values.addressLine2 || undefined,
        city: values.city || undefined,
        county: values.county || undefined,
        postalCode: values.postalCode || undefined,
        country: values.country || undefined,
        phone: values.phone || undefined,
        email: values.email || undefined,
        website: values.website || undefined,
        companyType: values.companyType || undefined,
        notes: values.notes || undefined,
        isActive: values.isActive,
      };

      let companyId = company?.id;

      if (isEditing) {
        await updateCompany.mutateAsync({
          id: company.id,
          data: {
            ...cleanedCompanyValues,
            tradingName: cleanedCompanyValues.tradingName ?? null,
            registrationNumber: cleanedCompanyValues.registrationNumber ?? null,
            vatNumber: cleanedCompanyValues.vatNumber ?? null,
            addressLine1: cleanedCompanyValues.addressLine1 ?? null,
            addressLine2: cleanedCompanyValues.addressLine2 ?? null,
            city: cleanedCompanyValues.city ?? null,
            county: cleanedCompanyValues.county ?? null,
            postalCode: cleanedCompanyValues.postalCode ?? null,
            country: cleanedCompanyValues.country ?? null,
            phone: cleanedCompanyValues.phone ?? null,
            email: cleanedCompanyValues.email ?? null,
            website: cleanedCompanyValues.website ?? null,
            companyType: cleanedCompanyValues.companyType ?? null,
            notes: cleanedCompanyValues.notes ?? null,
          },
        });

        // Handle contacts - create new, update existing, delete removed
        for (const contact of values.contacts) {
          const cleanedContact = {
            firstName: contact.firstName,
            lastName: contact.lastName,
            jobTitle: contact.jobTitle || undefined,
            email: contact.email || undefined,
            phone: contact.phone || undefined,
            mobile: contact.mobile || undefined,
            isPrimaryContact: contact.isPrimaryContact,
            isActive: contact.isActive,
            notes: contact.notes || undefined,
          };

          if (contact._isDeleted && contact.id) {
            await deleteContact.mutateAsync(contact.id);
          } else if (contact._isNew) {
            await createContact.mutateAsync(cleanedContact);
          } else if (contact.id) {
            await updateContact.mutateAsync({
              id: contact.id,
              data: {
                ...cleanedContact,
                jobTitle: cleanedContact.jobTitle ?? null,
                email: cleanedContact.email ?? null,
                phone: cleanedContact.phone ?? null,
                mobile: cleanedContact.mobile ?? null,
                notes: cleanedContact.notes ?? null,
              },
            });
          }
        }

        toast.success("Company updated successfully");
      } else {
        const createdCompany = await createCompany.mutateAsync(cleanedCompanyValues);
        companyId = createdCompany.id;
        toast.success("Company created successfully");
      }
      onSuccess?.();
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error(isEditing ? "Failed to update company" : "Failed to create company", {
        description: message,
      });
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8">
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="grid gap-6 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="companyCode"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Company Code *</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., COMP-001" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="companyType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Company Type</FormLabel>
                    <Select
                      onValueChange={(value) => field.onChange(value === "_none" ? "" : value)}
                      value={field.value || "_none"}
                    >
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select type" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="_none">None</SelectItem>
                        {COMPANY_TYPES.map((type) => (
                          <SelectItem key={type} value={type}>
                            {type}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid gap-6 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="companyName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Company Name *</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., Acme Corporation Ltd" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="tradingName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Trading Name</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., Acme Corp" {...field} value={field.value ?? ""} />
                    </FormControl>
                    <FormDescription>If different from company name</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid gap-6 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="registrationNumber"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Registration Number</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., 12345678" {...field} value={field.value ?? ""} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="vatNumber"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>VAT Number</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., IE1234567AB" {...field} value={field.value ?? ""} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Contact Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
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
                        placeholder="e.g., info@company.com"
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
              name="website"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Website</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g., https://www.company.com" {...field} value={field.value ?? ""} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Address</CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            <FormField
              control={form.control}
              name="addressLine1"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Address Line 1</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g., 123 Main Street" {...field} value={field.value ?? ""} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="addressLine2"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Address Line 2</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g., Suite 100" {...field} value={field.value ?? ""} />
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
                name="county"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>County</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., Dublin" {...field} value={field.value ?? ""} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="grid gap-6 sm:grid-cols-2">
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

              <FormField
                control={form.control}
                name="country"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Country</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., Ireland" {...field} value={field.value ?? ""} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
          </CardContent>
        </Card>

        {isEditing && (
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Contacts</CardTitle>
              <Button type="button" variant="outline" size="sm" onClick={addContact}>
                <Plus className="h-4 w-4 mr-2" />
                Add Contact
              </Button>
            </CardHeader>
            <CardContent>
              {fields.length === 0 ? (
                <p className="text-muted-foreground text-center py-4">
                  No contacts added yet. Click &quot;Add Contact&quot; to add one.
                </p>
              ) : (
                <Accordion type="multiple" className="w-full">
                  {fields.map((field, index) => {
                    const contact = form.watch(`contacts.${index}`);
                    if (contact._isDeleted) return null;

                    return (
                      <AccordionItem key={field.id} value={`contact-${index}`}>
                        <AccordionTrigger className="hover:no-underline">
                          <div className="flex items-center gap-2">
                            <span>
                              {contact.firstName || contact.lastName
                                ? `${contact.firstName} ${contact.lastName}`.trim()
                                : "New Contact"}
                            </span>
                            {contact.isPrimaryContact && (
                              <Badge variant="secondary" className="ml-2">
                                <Star className="h-3 w-3 mr-1" />
                                Primary
                              </Badge>
                            )}
                            {!contact.isActive && (
                              <Badge variant="outline" className="ml-2">Inactive</Badge>
                            )}
                          </div>
                        </AccordionTrigger>
                        <AccordionContent>
                          <div className="space-y-4 pt-4">
                            <div className="grid gap-4 sm:grid-cols-2">
                              <FormField
                                control={form.control}
                                name={`contacts.${index}.firstName`}
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
                                name={`contacts.${index}.lastName`}
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
                              name={`contacts.${index}.jobTitle`}
                              render={({ field }) => (
                                <FormItem>
                                  <FormLabel>Job Title</FormLabel>
                                  <FormControl>
                                    <Input placeholder="e.g., Sales Manager" {...field} value={field.value ?? ""} />
                                  </FormControl>
                                  <FormMessage />
                                </FormItem>
                              )}
                            />

                            <div className="grid gap-4 sm:grid-cols-2">
                              <FormField
                                control={form.control}
                                name={`contacts.${index}.email`}
                                render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>Email</FormLabel>
                                    <FormControl>
                                      <Input type="email" placeholder="e.g., john@company.com" {...field} value={field.value ?? ""} />
                                    </FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )}
                              />

                              <FormField
                                control={form.control}
                                name={`contacts.${index}.phone`}
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
                            </div>

                            <FormField
                              control={form.control}
                              name={`contacts.${index}.mobile`}
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

                            <div className="flex items-center gap-6">
                              <FormField
                                control={form.control}
                                name={`contacts.${index}.isPrimaryContact`}
                                render={({ field }) => (
                                  <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                                    <FormControl>
                                      <Checkbox
                                        checked={field.value}
                                        onCheckedChange={(checked) => {
                                          if (checked) {
                                            setPrimaryContact(index);
                                          } else {
                                            field.onChange(false);
                                          }
                                        }}
                                      />
                                    </FormControl>
                                    <div className="space-y-1 leading-none">
                                      <FormLabel>Primary Contact</FormLabel>
                                    </div>
                                  </FormItem>
                                )}
                              />

                              <FormField
                                control={form.control}
                                name={`contacts.${index}.isActive`}
                                render={({ field }) => (
                                  <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                                    <FormControl>
                                      <Checkbox
                                        checked={field.value}
                                        onCheckedChange={field.onChange}
                                      />
                                    </FormControl>
                                    <div className="space-y-1 leading-none">
                                      <FormLabel>Active</FormLabel>
                                    </div>
                                  </FormItem>
                                )}
                              />
                            </div>

                            <div className="flex justify-end">
                              <Button
                                type="button"
                                variant="destructive"
                                size="sm"
                                onClick={() => {
                                  if (contact.id && !contact._isNew) {
                                    // Mark existing contact for deletion
                                    form.setValue(`contacts.${index}._isDeleted`, true);
                                  } else {
                                    // Remove new contact from array
                                    remove(index);
                                  }
                                }}
                              >
                                <Trash2 className="h-4 w-4 mr-2" />
                                Remove Contact
                              </Button>
                            </div>
                          </div>
                        </AccordionContent>
                      </AccordionItem>
                    );
                  })}
                </Accordion>
              )}
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader>
            <CardTitle>Additional Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            <FormField
              control={form.control}
              name="notes"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Notes</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder="Additional notes about this company"
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
                      Inactive companies won&apos;t appear in selection dropdowns
                    </FormDescription>
                  </div>
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

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
              "Update Company"
            ) : (
              "Create Company"
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

