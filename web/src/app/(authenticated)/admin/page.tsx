"use client";

import Link from "next/link";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useSites } from "@/lib/api/admin/use-sites";
import { useEmployees } from "@/lib/api/admin/use-employees";
import { useCompanies } from "@/lib/api/admin/use-companies";
import { MapPin, Users, Building2, UserCog, ArrowRight } from "lucide-react";

export default function AdminDashboardPage() {
  const { data: sitesData, isLoading: sitesLoading } = useSites({ pageSize: 1 });
  const { data: employeesData, isLoading: employeesLoading } = useEmployees({ pageSize: 1 });
  const { data: companiesData, isLoading: companiesLoading } = useCompanies({ pageSize: 1 });

  const isLoading = sitesLoading || employeesLoading || companiesLoading;

  const totalSites = sitesData?.totalCount ?? 0;
  const totalEmployees = employeesData?.totalCount ?? 0;
  const totalCompanies = companiesData?.totalCount ?? 0;

  const quickLinks = [
    {
      title: "Sites",
      description: "Manage construction sites and locations",
      href: "/admin/sites",
      icon: MapPin,
      count: totalSites,
      addHref: "/admin/sites/new",
      addLabel: "Add Site",
    },
    {
      title: "Employees",
      description: "Manage employee records",
      href: "/admin/employees",
      icon: Users,
      count: totalEmployees,
      addHref: "/admin/employees/new",
      addLabel: "Add Employee",
    },
    {
      title: "Companies",
      description: "Manage companies and contacts",
      href: "/admin/companies",
      icon: Building2,
      count: totalCompanies,
      addHref: "/admin/companies/new",
      addLabel: "Add Company",
    },
    {
      title: "Users",
      description: "Manage user accounts and access",
      href: "/admin/users",
      icon: UserCog,
      count: null,
      addHref: null,
      addLabel: null,
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Administration</h1>
        <p className="text-muted-foreground">
          Manage sites, employees, companies, and users
        </p>
      </div>

      {/* Summary Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardDescription>Total Sites</CardDescription>
            <MapPin className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">
              {isLoading ? (
                <span className="animate-pulse text-muted-foreground">...</span>
              ) : (
                totalSites
              )}
            </div>
            <Link
              href="/admin/sites"
              className="text-sm text-muted-foreground hover:text-foreground"
            >
              View all sites
            </Link>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardDescription>Total Employees</CardDescription>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">
              {isLoading ? (
                <span className="animate-pulse text-muted-foreground">...</span>
              ) : (
                totalEmployees
              )}
            </div>
            <Link
              href="/admin/employees"
              className="text-sm text-muted-foreground hover:text-foreground"
            >
              View all employees
            </Link>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardDescription>Total Companies</CardDescription>
            <Building2 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">
              {isLoading ? (
                <span className="animate-pulse text-muted-foreground">...</span>
              ) : (
                totalCompanies
              )}
            </div>
            <Link
              href="/admin/companies"
              className="text-sm text-muted-foreground hover:text-foreground"
            >
              View all companies
            </Link>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardDescription>Total Users</CardDescription>
            <UserCog className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold text-muted-foreground">
              -
            </div>
            <Link
              href="/admin/users"
              className="text-sm text-muted-foreground hover:text-foreground"
            >
              View all users
            </Link>
          </CardContent>
        </Card>
      </div>

      {/* Quick Links */}
      <div className="grid gap-4 md:grid-cols-2">
        {quickLinks.map((link) => {
          const Icon = link.icon;
          return (
            <Card key={link.href} className="hover:shadow-md transition-shadow">
              <CardHeader>
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
                    <Icon className="h-5 w-5" />
                  </div>
                  <div>
                    <CardTitle className="text-lg">{link.title}</CardTitle>
                    <CardDescription>{link.description}</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="flex items-center justify-between">
                <Button asChild variant="ghost" className="p-0 h-auto hover:bg-transparent">
                  <Link href={link.href} className="flex items-center gap-2 text-primary">
                    View all
                    <ArrowRight className="h-4 w-4" />
                  </Link>
                </Button>
                {link.addHref && (
                  <Button asChild size="sm">
                    <Link href={link.addHref}>{link.addLabel}</Link>
                  </Button>
                )}
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
