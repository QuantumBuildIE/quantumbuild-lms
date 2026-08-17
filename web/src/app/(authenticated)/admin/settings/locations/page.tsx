"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { MoreHorizontal } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
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
import { useSites, useUpdateSite } from "@/lib/api/admin/use-sites";
import { getApiErrorMessage } from "@/lib/utils";
import type { Site } from "@/types/admin";

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

export default function LocationsPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const pageNumber = Number(searchParams.get("page")) || 1;
  const pageSize = Number(searchParams.get("size")) || 20;
  const sortColumn = searchParams.get("sortColumn") || undefined;
  const sortDirection = (searchParams.get("sortDirection") as SortDirection) || undefined;
  const searchParam = searchParams.get("search") || "";

  const [searchInput, setSearchInput] = React.useState(searchParam);
  const debouncedSearch = useDebounce(searchInput, 300);

  React.useEffect(() => {
    if (debouncedSearch !== searchParam) {
      updateUrlParams({ search: debouncedSearch || null, page: 1 });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch]);

  const { data, isLoading, error } = useSites({
    pageNumber,
    pageSize,
    sortColumn,
    sortDirection,
    search: searchParam || undefined,
  });

  const updateSite = useUpdateSite();

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

  const handlePageChange = (page: number) => updateUrlParams({ page });
  const handlePageSizeChange = (size: number) => updateUrlParams({ size, page: 1 });
  const handleSort = (column: string, direction: SortDirection) =>
    updateUrlParams({ sortColumn: column, sortDirection: direction, page: 1 });

  const handleToggleActive = (site: Site) => {
    const isActive = !site.isActive;
    updateSite.mutate(
      {
        id: site.id,
        data: {
          siteCode: site.siteCode,
          siteName: site.siteName,
          address: site.address || undefined,
          city: site.city || undefined,
          postalCode: site.postalCode || undefined,
          phone: site.phone || undefined,
          email: site.email || undefined,
          notes: site.notes || undefined,
          isActive,
          siteManagerId: site.siteManagerId ?? null,
          companyId: site.companyId ?? null,
        },
      },
      {
        onSuccess: () => {
          toast.success(`${site.siteName} ${isActive ? "activated" : "deactivated"}`);
        },
        onError: (error) => {
          toast.error(getApiErrorMessage(error, "Failed to update location"));
        },
      }
    );
  };

  const columns: Column<Site>[] = [
    {
      key: "siteCode",
      header: "Code",
      sortable: true,
      className: "font-medium",
    },
    {
      key: "siteName",
      header: "Name",
      sortable: true,
    },
    {
      key: "city",
      header: "City",
      sortable: true,
      render: (site) =>
        site.city ? <span>{site.city}</span> : <span className="text-muted-foreground">-</span>,
    },
    {
      key: "phone",
      header: "Phone",
      render: (site) =>
        site.phone ? <span>{site.phone}</span> : <span className="text-muted-foreground">-</span>,
    },
    {
      key: "isActive",
      header: "Status",
      sortable: true,
      render: (site) =>
        site.isActive ? (
          <Badge variant="default">Active</Badge>
        ) : (
          <Badge variant="secondary">Inactive</Badge>
        ),
    },
    {
      key: "actions",
      header: "Actions",
      headerClassName: "text-right",
      className: "text-right",
      render: (site) => (
        <div className="flex items-center justify-end gap-2">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" onClick={(e) => e.stopPropagation()}>
                <MoreHorizontal className="h-4 w-4" />
                <span className="sr-only">Open menu</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem asChild>
                <Link href={`/admin/settings/locations/${site.id}/edit`}>Edit</Link>
              </DropdownMenuItem>
              <DropdownMenuItem
                onClick={(e) => {
                  e.stopPropagation();
                  handleToggleActive(site);
                }}
                disabled={updateSite.isPending}
              >
                {site.isActive ? "Deactivate" : "Activate"}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      ),
    },
  ];

  if (error) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Locations</h1>
          <p className="text-muted-foreground">Manage your organization&apos;s locations</p>
        </div>
        <div className="rounded-lg border bg-card p-8 text-center">
          <p className="text-destructive">Failed to load locations. Please try again.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Locations</h1>
          <p className="text-muted-foreground">
            Create, edit, and deactivate locations for your organization
          </p>
        </div>
        <Button asChild>
          <Link href="/admin/settings/locations/new">Add Location</Link>
        </Button>
      </div>

      <div className="flex items-center gap-4">
        <div className="relative flex-1 max-w-sm">
          <Input
            placeholder="Search by name, code, or city..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
        </div>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyMessage="No locations found"
        keyExtractor={(site) => site.id}
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
        onRowClick={(site) => router.push(`/admin/settings/locations/${site.id}/edit`)}
      />
    </div>
  );
}
