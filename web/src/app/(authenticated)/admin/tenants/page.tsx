"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { MoreHorizontal } from "lucide-react";
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
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  DataTable,
  type Column,
  type SortDirection,
} from "@/components/shared/data-table";
import { useTenants } from "@/lib/api/admin/use-tenants";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import type { TenantListItem, TenantStatus } from "@/types/admin";
import { format } from "date-fns";

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

const statusVariant: Record<TenantStatus, "default" | "secondary" | "destructive"> = {
  Active: "default",
  Suspended: "destructive",
  Inactive: "secondary",
};

// Backend sends status as numeric enum (1=Active, 2=Suspended, 3=Inactive)
function getStatusLabel(status: TenantStatus | number): TenantStatus {
  if (typeof status === "number") {
    switch (status) {
      case 1: return "Active";
      case 2: return "Suspended";
      case 3: return "Inactive";
      default: return "Active";
    }
  }
  return status;
}

export default function TenantsPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const isSuperUser = useIsSuperUser();

  const pageNumber = Number(searchParams.get("page")) || 1;
  const pageSize = Number(searchParams.get("size")) || 20;
  const sortColumn = searchParams.get("sortColumn") || undefined;
  const sortDirection =
    (searchParams.get("sortDirection") as SortDirection) || undefined;
  const searchParam = searchParams.get("search") || "";

  const [searchInput, setSearchInput] = React.useState(searchParam);
  const debouncedSearch = useDebounce(searchInput, 300);

  React.useEffect(() => {
    if (debouncedSearch !== searchParam) {
      updateUrlParams({ search: debouncedSearch || null, page: 1 });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch]);

  const { data, isLoading, error } = useTenants({
    pageNumber,
    pageSize,
    sortColumn,
    sortDirection,
    search: searchParam || undefined,
  });

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

  // Client-side status filter
  const [statusFilter, setStatusFilter] = React.useState<string>("all");

  const filteredItems = React.useMemo(() => {
    if (!data?.items) return [];
    if (statusFilter === "all") return data.items;
    return data.items.filter(
      (t) => getStatusLabel(t.status) === statusFilter
    );
  }, [data?.items, statusFilter]);

  if (!isSuperUser) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to access Tenant Management.
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Tenants</h1>
            <p className="text-muted-foreground">Manage tenants</p>
          </div>
        </div>
        <div className="rounded-lg border bg-card p-8 text-center">
          <p className="text-destructive">
            Failed to load tenants. Please try again.
          </p>
        </div>
      </div>
    );
  }

  const columns: Column<TenantListItem>[] = [
    {
      key: "name",
      header: "Name",
      sortable: true,
      className: "font-medium",
    },
    {
      key: "status",
      header: "Status",
      sortable: true,
      render: (tenant) => {
        const label = getStatusLabel(tenant.status);
        return <Badge variant={statusVariant[label]}>{label}</Badge>;
      },
    },
    {
      key: "contactName",
      header: "Contact",
      render: (tenant) =>
        tenant.contactName ? (
          <div>
            <div>{tenant.contactName}</div>
            {tenant.contactEmail && (
              <div className="text-xs text-muted-foreground">
                {tenant.contactEmail}
              </div>
            )}
          </div>
        ) : (
          <span className="text-muted-foreground">-</span>
        ),
    },
    {
      key: "createdAt",
      header: "Created",
      sortable: true,
      render: (tenant) => {
        try {
          return format(new Date(tenant.createdAt), "dd MMM yyyy");
        } catch {
          return <span className="text-muted-foreground">-</span>;
        }
      },
    },
    {
      key: "actions",
      header: "Actions",
      headerClassName: "text-right",
      className: "text-right",
      render: (tenant) => (
        <div className="flex items-center justify-end gap-2">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm">
                <MoreHorizontal className="h-4 w-4" />
                <span className="sr-only">Open menu</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem asChild>
                <Link href={`/admin/tenants/${tenant.id}`}>
                  View / Edit
                </Link>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Tenants</h1>
          <p className="text-muted-foreground">Manage tenant organizations</p>
        </div>
        <Button asChild>
          <Link href="/admin/tenants/new">Add Tenant</Link>
        </Button>
      </div>

      <div className="flex items-center gap-4">
        <div className="relative flex-1 max-w-sm">
          <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by name, code, or contact..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="pl-9"
          />
        </div>
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Active">Active</SelectItem>
            <SelectItem value="Suspended">Suspended</SelectItem>
            <SelectItem value="Inactive">Inactive</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <DataTable
        columns={columns}
        data={filteredItems}
        isLoading={isLoading}
        emptyMessage="No tenants found"
        keyExtractor={(tenant) => tenant.id}
        skeletonRows={pageSize}
        pagination={
          data
            ? {
                pageNumber: data.pageNumber,
                pageSize: data.pageSize,
                totalCount: statusFilter === "all" ? data.totalCount : filteredItems.length,
                totalPages: statusFilter === "all" ? data.totalPages : Math.ceil(filteredItems.length / data.pageSize),
              }
            : undefined
        }
        onPageChange={handlePageChange}
        onPageSizeChange={handlePageSizeChange}
        sortColumn={sortColumn}
        sortDirection={sortDirection}
        onSort={handleSort}
      />
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
