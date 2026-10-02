"use client";

import { useRouter } from "next/navigation";
import { useAuth, useHasAnyPermission } from "@/lib/auth/use-auth";
import { useMyTrainingSummary } from "@/lib/api/toolbox-talks/use-my-toolbox-talks";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { LogOut, User, KeyRound, ClipboardList, Shield, HelpCircle } from "lucide-react";
import Link from "next/link";
import { TenantSwitcher } from "@/components/layout/tenant-switcher";
import { CONTENT_CONTAINER } from "@/components/layout/content-container";
import { BrandLockup } from "@/components/branding/brand-lockup";
import { useCurrentBranding } from "@/lib/api/branding/use-branding";

export function TopNav() {
  const router = useRouter();
  const { user, logout } = useAuth();
  const isSuperUser = user?.isSuperUser ?? false;
  const { logoUrl, tenantName, isLoading: brandingLoading } = useCurrentBranding();
  const { data: trainingSummary } = useMyTrainingSummary(!!user?.employeeId);
  const hasAdminAccess = useHasAnyPermission([
    "Core.ManageEmployees",
    "Core.ManageUsers",
    "Learnings.Manage",
    "Learnings.Schedule",
    "Learnings.Admin",
    "LessonParser.Use",
  ]);

  const isSupervisorOnly =
    !isSuperUser &&
    (user?.roles?.includes("Supervisor") ?? false) &&
    !user?.roles?.some((r) => r === "Admin");

  const initials = user
    ? `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase()
    : "??";

  // Calculate total pending training (pending + in-progress + overdue)
  const pendingTrainingCount = trainingSummary?.totalCount ?? 0;
  const hasOverdue = (trainingSummary?.overdueCount ?? 0) > 0;

  const handleLogout = () => {
    logout();
    router.push("/login");
  };

  return (
    <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className={`${CONTENT_CONTAINER} flex h-14 items-center justify-between`}>
        <Link href={isSuperUser ? "/admin/tenants" : user?.employeeId ? "/toolbox-talks" : "/admin/toolbox-talks"} className="flex items-center gap-2 hover:opacity-80 transition-opacity">
          <BrandLockup logoUrl={logoUrl} alt={tenantName ?? "Tenant logo"} isLoading={brandingLoading} />
        </Link>

        <div className="flex items-center gap-4">
          {user?.isSuperUser && <TenantSwitcher />}
        </div>

        <div className="flex items-center gap-2">
          <Link
            href="/help"
            className="flex h-8 w-8 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
            title="Help"
          >
            <HelpCircle className="h-5 w-5" />
          </Link>

          <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className="flex items-center gap-2 rounded-full focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2">
              <Avatar className="h-8 w-8 cursor-pointer">
                <AvatarFallback className="bg-primary text-primary-foreground text-sm">
                  {initials}
                </AvatarFallback>
              </Avatar>
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel className="font-normal">
              <div className="flex flex-col space-y-1">
                <p className="text-sm font-medium leading-none">
                  {user?.firstName} {user?.lastName}
                </p>
                <p className="text-xs leading-none text-muted-foreground">
                  {user?.email}
                </p>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link href="/profile">
                <User className="mr-2 h-4 w-4" />
                <span>Profile</span>
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link href="/profile">
                <KeyRound className="mr-2 h-4 w-4" />
                <span>Change Password</span>
              </Link>
            </DropdownMenuItem>
            {!isSuperUser && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link href={user?.employeeId ? "/toolbox-talks" : "/admin/toolbox-talks"} className="flex items-center justify-between w-full">
                    <span className="flex items-center">
                      <ClipboardList className="mr-2 h-4 w-4" />
                      <span>{user?.employeeId ? "My Learnings" : "Learnings Admin"}</span>
                    </span>
                    {pendingTrainingCount > 0 && (
                      <Badge
                        variant={hasOverdue ? "destructive" : "secondary"}
                        className="ml-2 h-5 min-w-[20px] px-1.5 text-xs"
                      >
                        {pendingTrainingCount}
                      </Badge>
                    )}
                  </Link>
                </DropdownMenuItem>
              </>
            )}
            {hasAdminAccess && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link href={isSupervisorOnly ? "/admin/toolbox-talks" : "/admin"}>
                    <Shield className="mr-2 h-4 w-4" />
                    <span>{isSupervisorOnly ? "Learning Management" : "Administration"}</span>
                  </Link>
                </DropdownMenuItem>
              </>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleLogout} className="text-destructive focus:text-destructive">
              <LogOut className="mr-2 h-4 w-4" />
              <span>Sign out</span>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <div className="px-2 py-1.5 text-center space-y-0.5">
              <span className="text-xs text-muted-foreground">A CertifiedIQ Product</span>
              <div>
                <a href="/ai-system-card" className="text-xs text-muted-foreground hover:text-primary hover:underline transition-colors">
                  AI System Card
                </a>
              </div>
            </div>
          </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  );
}
