"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useEmployees } from "@/lib/api/admin/use-employees";
import { usePermission } from "@/lib/auth/use-auth";
import { Users, UserCog, ArrowRight } from "lucide-react";

export default function AdminDashboardPage() {
  const router = useRouter();
  const { data: employeesData, isLoading } = useEmployees({ pageSize: 1 });
  const canManageUsers = usePermission("Core.ManageUsers");
  const canManageEmployees = usePermission("Core.ManageEmployees");

  useEffect(() => {
    if (!canManageEmployees && !canManageUsers) {
      router.replace("/admin/toolbox-talks");
    }
  }, [canManageEmployees, canManageUsers, router]);

  const totalEmployees = employeesData?.totalCount ?? 0;

  const quickLinks = [
    {
      title: "Employees",
      description: "Manage employee records",
      href: "/admin/employees",
      icon: Users,
      count: totalEmployees,
      addHref: "/admin/employees/new",
      addLabel: "Add Employee",
    },
    ...(canManageUsers ? [{
      title: "Users",
      description: "Manage user accounts and access",
      href: "/admin/users",
      icon: UserCog,
      count: null as number | null,
      addHref: null as string | null,
      addLabel: null as string | null,
    }] : []),
  ];

  return (
    <div className="space-y-6">
      {/* Summary Cards */}
      <div className="grid gap-4 md:grid-cols-2">
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

        {canManageUsers && (
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
        )}
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
