"use client";

import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { useCompany, useDeleteCompany } from "@/lib/api/admin/use-companies";
import { useDeleteCompanyContact } from "@/lib/api/admin/use-contacts";
import { ChevronLeft, Edit, Trash2, Plus, Star, Mail, Phone, Globe, MapPin, Building2 } from "lucide-react";
import { toast } from "sonner";

export default function ViewCompanyPage() {
  const params = useParams();
  const router = useRouter();
  const companyId = params.id as string;

  const { data: company, isLoading, error } = useCompany(companyId);
  const deleteCompany = useDeleteCompany();
  const deleteContact = useDeleteCompanyContact(companyId);

  const handleDeleteCompany = async () => {
    try {
      await deleteCompany.mutateAsync(companyId);
      toast.success("Company deleted successfully");
      router.push("/admin/companies");
    } catch {
      toast.error("Failed to delete company");
    }
  };

  const handleDeleteContact = async (contactId: string, contactName: string) => {
    try {
      await deleteContact.mutateAsync(contactId);
      toast.success(`Contact "${contactName}" deleted successfully`);
    } catch {
      toast.error("Failed to delete contact");
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/companies">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Company Details</h1>
            <p className="text-muted-foreground">Loading company details...</p>
          </div>
        </div>
        <Card>
          <CardContent className="py-8">
            <div className="flex items-center justify-center">
              <div className="animate-pulse text-muted-foreground">
                Loading...
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !company) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/companies">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Company Details</h1>
            <p className="text-muted-foreground">Company not found</p>
          </div>
        </div>
        <Card>
          <CardContent className="py-8">
            <div className="text-center">
              <p className="text-destructive">
                Failed to load company. The company may have been deleted.
              </p>
              <Button className="mt-4" asChild>
                <Link href="/admin/companies">Back to Companies</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const formatAddress = () => {
    const parts = [
      company.addressLine1,
      company.addressLine2,
      company.city,
      company.county,
      company.postalCode,
      company.country,
    ].filter(Boolean);
    return parts.length > 0 ? parts.join(", ") : null;
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/companies">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-semibold tracking-tight">{company.companyName}</h1>
              {company.isActive ? (
                <Badge variant="default">Active</Badge>
              ) : (
                <Badge variant="secondary">Inactive</Badge>
              )}
            </div>
            <p className="text-muted-foreground">
              {company.companyCode}
              {company.companyType && ` - ${company.companyType}`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" asChild>
            <Link href={`/admin/companies/${companyId}/edit`}>
              <Edit className="h-4 w-4 mr-2" />
              Edit
            </Link>
          </Button>
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="destructive">
                <Trash2 className="h-4 w-4 mr-2" />
                Delete
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete Company</AlertDialogTitle>
                <AlertDialogDescription>
                  Are you sure you want to delete &quot;{company.companyName}&quot;? This action cannot be undone.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction
                  onClick={handleDeleteCompany}
                  className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                >
                  Delete
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Building2 className="h-5 w-5" />
              Company Information
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {company.tradingName && (
              <div>
                <p className="text-sm font-medium text-muted-foreground">Trading Name</p>
                <p>{company.tradingName}</p>
              </div>
            )}
            {company.registrationNumber && (
              <div>
                <p className="text-sm font-medium text-muted-foreground">Registration Number</p>
                <p>{company.registrationNumber}</p>
              </div>
            )}
            {company.vatNumber && (
              <div>
                <p className="text-sm font-medium text-muted-foreground">VAT Number</p>
                <p>{company.vatNumber}</p>
              </div>
            )}
            {company.companyType && (
              <div>
                <p className="text-sm font-medium text-muted-foreground">Company Type</p>
                <Badge variant="outline">{company.companyType}</Badge>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Mail className="h-5 w-5" />
              Contact Details
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {company.phone && (
              <div className="flex items-center gap-2">
                <Phone className="h-4 w-4 text-muted-foreground" />
                <a href={`tel:${company.phone}`} className="text-primary hover:underline">
                  {company.phone}
                </a>
              </div>
            )}
            {company.email && (
              <div className="flex items-center gap-2">
                <Mail className="h-4 w-4 text-muted-foreground" />
                <a href={`mailto:${company.email}`} className="text-primary hover:underline">
                  {company.email}
                </a>
              </div>
            )}
            {company.website && (
              <div className="flex items-center gap-2">
                <Globe className="h-4 w-4 text-muted-foreground" />
                <a
                  href={company.website.startsWith("http") ? company.website : `https://${company.website}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-primary hover:underline"
                >
                  {company.website}
                </a>
              </div>
            )}
            {formatAddress() && (
              <div className="flex items-start gap-2">
                <MapPin className="h-4 w-4 text-muted-foreground mt-0.5" />
                <p>{formatAddress()}</p>
              </div>
            )}
            {!company.phone && !company.email && !company.website && !formatAddress() && (
              <p className="text-muted-foreground">No contact details available</p>
            )}
          </CardContent>
        </Card>
      </div>

      {company.notes && (
        <Card>
          <CardHeader>
            <CardTitle>Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap">{company.notes}</p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Contacts ({company.contacts?.length || 0})</CardTitle>
          <Button variant="outline" size="sm" asChild>
            <Link href={`/admin/companies/${companyId}/edit`}>
              <Plus className="h-4 w-4 mr-2" />
              Add Contact
            </Link>
          </Button>
        </CardHeader>
        <CardContent>
          {company.contacts && company.contacts.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Job Title</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Phone</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {company.contacts.map((contact) => (
                  <TableRow key={contact.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{contact.fullName}</span>
                        {contact.isPrimaryContact && (
                          <Badge variant="secondary" className="flex items-center gap-1">
                            <Star className="h-3 w-3" />
                            Primary
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      {contact.jobTitle || <span className="text-muted-foreground">-</span>}
                    </TableCell>
                    <TableCell>
                      {contact.email ? (
                        <a href={`mailto:${contact.email}`} className="text-primary hover:underline">
                          {contact.email}
                        </a>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )}
                    </TableCell>
                    <TableCell>
                      {contact.phone || contact.mobile ? (
                        <a
                          href={`tel:${contact.phone || contact.mobile}`}
                          className="text-primary hover:underline"
                        >
                          {contact.phone || contact.mobile}
                        </a>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )}
                    </TableCell>
                    <TableCell>
                      {contact.isActive ? (
                        <Badge variant="default">Active</Badge>
                      ) : (
                        <Badge variant="secondary">Inactive</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <AlertDialog>
                        <AlertDialogTrigger asChild>
                          <Button variant="ghost" size="sm" className="text-destructive hover:text-destructive">
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </AlertDialogTrigger>
                        <AlertDialogContent>
                          <AlertDialogHeader>
                            <AlertDialogTitle>Delete Contact</AlertDialogTitle>
                            <AlertDialogDescription>
                              Are you sure you want to delete &quot;{contact.fullName}&quot;? This action cannot be undone.
                            </AlertDialogDescription>
                          </AlertDialogHeader>
                          <AlertDialogFooter>
                            <AlertDialogCancel>Cancel</AlertDialogCancel>
                            <AlertDialogAction
                              onClick={() => handleDeleteContact(contact.id, contact.fullName)}
                              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                            >
                              Delete
                            </AlertDialogAction>
                          </AlertDialogFooter>
                        </AlertDialogContent>
                      </AlertDialog>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <p className="text-muted-foreground text-center py-4">
              No contacts for this company yet.{" "}
              <Link href={`/admin/companies/${companyId}/edit`} className="text-primary hover:underline">
                Add a contact
              </Link>
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
