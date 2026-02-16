"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  DataTable,
  type Column,
  type SortDirection,
} from "@/components/shared/data-table";
import { useCompanies, useDeleteCompany, type CompanyWithContacts } from "@/lib/api/admin/use-companies";

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

export default function CompaniesPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // Parse URL params
  const pageNumber = Number(searchParams.get("page")) || 1;
  const pageSize = Number(searchParams.get("size")) || 20;
  const sortColumn = searchParams.get("sortColumn") || undefined;
  const sortDirection = (searchParams.get("sortDirection") as SortDirection) || undefined;
  const searchParam = searchParams.get("search") || "";

  const [searchInput, setSearchInput] = React.useState(searchParam);
  const debouncedSearch = useDebounce(searchInput, 300);

  // Update URL when search changes (debounced)
  React.useEffect(() => {
    if (debouncedSearch !== searchParam) {
      updateUrlParams({ search: debouncedSearch || null, page: 1 });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch]);

  const { data, isLoading, error } = useCompanies({
    pageNumber,
    pageSize,
    sortColumn,
    sortDirection,
    search: searchParam || undefined,
  });

  const deleteCompany = useDeleteCompany();

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

  const handleDelete = async (id: string) => {
    if (window.confirm("Are you sure you want to delete this company?")) {
      try {
        await deleteCompany.mutateAsync(id);
      } catch {
        // Error handling is done by the mutation
      }
    }
  };

  const columns: Column<CompanyWithContacts>[] = [
    {
      key: "companyCode",
      header: "Code",
      sortable: true,
      className: "font-medium",
    },
    {
      key: "companyName",
      header: "Company Name",
      sortable: true,
      render: (company) => (
        <Link
          href={`/admin/companies/${company.id}`}
          className="text-primary hover:underline font-medium"
        >
          {company.companyName}
        </Link>
      ),
    },
    {
      key: "companyType",
      header: "Type",
      sortable: true,
      render: (company) =>
        company.companyType ? (
          <Badge variant="outline">{company.companyType}</Badge>
        ) : (
          <span className="text-muted-foreground">-</span>
        ),
    },
    {
      key: "phone",
      header: "Phone",
      render: (company) =>
        company.phone ? (
          <span>{company.phone}</span>
        ) : (
          <span className="text-muted-foreground">-</span>
        ),
    },
    {
      key: "email",
      header: "Email",
      render: (company) =>
        company.email ? (
          <span>{company.email}</span>
        ) : (
          <span className="text-muted-foreground">-</span>
        ),
    },
    {
      key: "contactCount",
      header: "Contacts",
      render: (company) => (
        <span className="text-center">{company.contactCount}</span>
      ),
    },
    {
      key: "isActive",
      header: "Status",
      sortable: true,
      render: (company) =>
        company.isActive ? (
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
      render: (company) => (
        <div className="flex items-center justify-end gap-2">
          <Button variant="ghost" size="sm" asChild>
            <Link href={`/admin/companies/${company.id}`}>View</Link>
          </Button>
          <Button variant="ghost" size="sm" asChild>
            <Link href={`/admin/companies/${company.id}/edit`}>Edit</Link>
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="text-destructive hover:text-destructive"
            onClick={(e) => {
              e.stopPropagation();
              handleDelete(company.id);
            }}
            disabled={deleteCompany.isPending}
          >
            Delete
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
            <h1 className="text-2xl font-semibold tracking-tight">Companies</h1>
            <p className="text-muted-foreground">Manage companies and their contacts</p>
          </div>
        </div>
        <div className="rounded-lg border bg-card p-8 text-center">
          <p className="text-destructive">
            Failed to load companies. Please try again.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Companies</h1>
          <p className="text-muted-foreground">Manage companies and their contacts</p>
        </div>
        <Button asChild>
          <Link href="/admin/companies/new">Add Company</Link>
        </Button>
      </div>

      <div className="flex items-center gap-4">
        <div className="relative flex-1 max-w-sm">
          <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by name or email..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="pl-9"
          />
        </div>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyMessage="No companies found"
        keyExtractor={(company) => company.id}
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
