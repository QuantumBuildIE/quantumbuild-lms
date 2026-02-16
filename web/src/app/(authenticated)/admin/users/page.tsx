"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  DataTable,
  type Column,
  type SortDirection,
} from "@/components/shared/data-table";
import {
  useUsers,
  useDeleteUser,
  useUpdateUser,
  type User,
} from "@/lib/api/admin/use-users";
import { useRoles } from "@/lib/api/admin/use-roles";
import { ResetPasswordDialog } from "@/components/admin/reset-password-dialog";
import { toast } from "sonner";

function useDebounce<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = React.useState(value);

  React.useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(timer);
    };
  }, [value, delay]);

  return debouncedValue;
}

export default function UsersPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // Parse URL params
  const pageNumber = Number(searchParams.get("page")) || 1;
  const pageSize = Number(searchParams.get("size")) || 20;
  const sortColumn = searchParams.get("sortColumn") || undefined;
  const sortDirection = (searchParams.get("sortDirection") as SortDirection) || undefined;
  const searchParam = searchParams.get("search") || "";
  const roleFilter = searchParams.get("role") || "";
  const statusFilter = searchParams.get("status") || "";

  const [searchInput, setSearchInput] = React.useState(searchParam);
  const debouncedSearch = useDebounce(searchInput, 300);

  // Dialog states
  const [resetPasswordUser, setResetPasswordUser] = React.useState<User | null>(null);
  const [toggleActiveUser, setToggleActiveUser] = React.useState<User | null>(null);

  // Update URL when search changes (debounced)
  React.useEffect(() => {
    if (debouncedSearch !== searchParam) {
      updateUrlParams({ search: debouncedSearch || null, page: 1 });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch]);

  const { data: rolesData } = useRoles();

  const { data, isLoading, error } = useUsers({
    pageNumber,
    pageSize,
    sortColumn,
    sortDirection,
    search: searchParam || undefined,
    roleId: roleFilter || undefined,
    isActive: statusFilter === "" ? undefined : statusFilter === "active",
  });

  const deleteUser = useDeleteUser();
  const updateUser = useUpdateUser();

  const updateUrlParams = (
    updates: Record<string, string | number | null | undefined>
  ) => {
    const params = new URLSearchParams(searchParams.toString());

    Object.entries(updates).forEach(([key, value]) => {
      if (value === null || value === undefined || value === "") {
        params.delete(key);
      } else {
        params.set(key, String(value));
      }
    });

    // Remove page param if it's 1
    if (params.get("page") === "1") {
      params.delete("page");
    }

    const queryString = params.toString();
    router.push(queryString ? `${pathname}?${queryString}` : pathname);
  };

  const handlePageChange = (page: number) => {
    updateUrlParams({ page });
  };

  const handlePageSizeChange = (size: number) => {
    updateUrlParams({ size, page: 1 });
  };

  const handleSort = (column: string, direction: SortDirection) => {
    updateUrlParams({ sortColumn: column, sortDirection: direction, page: 1 });
  };

  const handleRoleFilter = (value: string) => {
    updateUrlParams({ role: value === "all" ? null : value, page: 1 });
  };

  const handleStatusFilter = (value: string) => {
    updateUrlParams({ status: value === "all" ? null : value, page: 1 });
  };

  const handleToggleActiveConfirm = async () => {
    if (!toggleActiveUser) return;

    const action = toggleActiveUser.isActive ? "deactivated" : "activated";
    try {
      await updateUser.mutateAsync({
        id: toggleActiveUser.id,
        data: {
          firstName: toggleActiveUser.firstName,
          lastName: toggleActiveUser.lastName,
          isActive: !toggleActiveUser.isActive,
          roleIds: toggleActiveUser.roles.map((r) => r.id),
        },
      });
      toast.success(`User ${action} successfully`, {
        description: `${toggleActiveUser.fullName} has been ${action}`,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error(`Failed to ${toggleActiveUser.isActive ? "deactivate" : "activate"} user`, {
        description: message,
      });
    } finally {
      setToggleActiveUser(null);
    }
  };

  const columns: Column<User>[] = [
    {
      key: "fullName",
      header: "Name",
      sortable: true,
      render: (user) => (
        <span className="font-medium">{user.fullName}</span>
      ),
    },
    {
      key: "email",
      header: "Email",
      sortable: true,
    },
    {
      key: "roles",
      header: "Roles",
      render: (user) =>
        user.roles.length > 0 ? (
          <div className="flex flex-wrap gap-1">
            {user.roles.map((role) => (
              <Badge key={role.id} variant="outline">
                {role.name}
              </Badge>
            ))}
          </div>
        ) : (
          <span className="text-muted-foreground">-</span>
        ),
    },
    {
      key: "isActive",
      header: "Status",
      sortable: true,
      render: (user) =>
        user.isActive ? (
          <Badge variant="default">Active</Badge>
        ) : (
          <Badge variant="secondary">Inactive</Badge>
        ),
    },
    {
      key: "createdAt",
      header: "Created",
      sortable: true,
      render: (user) => {
        const date = new Date(user.createdAt);
        return (
          <span className="text-muted-foreground">
            {date.toLocaleDateString()}
          </span>
        );
      },
    },
    {
      key: "actions",
      header: "Actions",
      headerClassName: "text-right",
      className: "text-right",
      render: (user) => (
        <div className="flex items-center justify-end gap-2">
          <Button variant="ghost" size="sm" asChild>
            <Link href={`/admin/users/${user.id}/edit`}>Edit</Link>
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              setResetPasswordUser(user);
            }}
          >
            Reset Password
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              setToggleActiveUser(user);
            }}
            disabled={updateUser.isPending}
          >
            {user.isActive ? "Deactivate" : "Activate"}
          </Button>
        </div>
      ),
    },
  ];

  if (error) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Users</h1>
            <p className="text-muted-foreground">Manage user accounts and roles</p>
          </div>
        </div>
        <div className="rounded-lg border bg-card p-8 text-center">
          <p className="text-destructive">
            Failed to load users. Please try again.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Users</h1>
          <p className="text-muted-foreground">Manage user accounts and roles</p>
        </div>
        <Button asChild>
          <Link href="/admin/users/new">Add User</Link>
        </Button>
      </div>

      <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
        <div className="relative flex-1 max-w-sm">
          <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by name or email..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="pl-9"
          />
        </div>

        <div className="flex items-center gap-2">
          <Select
            value={roleFilter || "all"}
            onValueChange={handleRoleFilter}
          >
            <SelectTrigger className="w-[150px]">
              <SelectValue placeholder="Filter by role" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Roles</SelectItem>
              {rolesData?.map((role) => (
                <SelectItem key={role.id} value={role.id}>
                  {role.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={statusFilter || "all"}
            onValueChange={handleStatusFilter}
          >
            <SelectTrigger className="w-[130px]">
              <SelectValue placeholder="Filter by status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Status</SelectItem>
              <SelectItem value="active">Active</SelectItem>
              <SelectItem value="inactive">Inactive</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyMessage="No users found"
        keyExtractor={(user) => user.id}
        skeletonRows={pageSize}
        pagination={
          data
            ? {
                pageNumber: data.pageNumber,
                pageSize: data.pageSize,
                totalCount: data.totalCount,
                totalPages: data.totalPages,
              }
            : undefined
        }
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
        sortColumn={sortColumn}
        sortDirection={sortDirection}
        onSort={handleSort}
      />

      {/* Reset Password Dialog */}
      {resetPasswordUser && (
        <ResetPasswordDialog
          open={!!resetPasswordUser}
          onOpenChange={(open) => !open && setResetPasswordUser(null)}
          userId={resetPasswordUser.id}
          userName={resetPasswordUser.fullName}
        />
      )}

      {/* Activate/Deactivate Confirmation Dialog */}
      <AlertDialog
        open={!!toggleActiveUser}
        onOpenChange={(open) => !open && setToggleActiveUser(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {toggleActiveUser?.isActive ? "Deactivate" : "Activate"} User
            </AlertDialogTitle>
            <AlertDialogDescription>
              {toggleActiveUser?.isActive
                ? `Are you sure you want to deactivate ${toggleActiveUser?.fullName}? They will no longer be able to log in to the system.`
                : `Are you sure you want to activate ${toggleActiveUser?.fullName}? They will be able to log in to the system.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleToggleActiveConfirm}>
              {toggleActiveUser?.isActive ? "Deactivate" : "Activate"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function SearchIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      fill="none"
      stroke="currentColor"
      viewBox="0 0 24 24"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth={2}
        d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
      />
    </svg>
  );
}
