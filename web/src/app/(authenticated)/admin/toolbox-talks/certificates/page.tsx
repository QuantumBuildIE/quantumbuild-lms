'use client';

import { useState, useCallback } from 'react';
import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { format } from 'date-fns';
import {
  Download,
  Search,
  Award,
  CheckCircle,
  AlertTriangle,
  XCircle,
  BookOpen,
  GraduationCap,
  RefreshCw,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useCertificateReport, handleDownloadCertificateAdmin } from '@/lib/api/toolbox-talks';
import { toast } from 'sonner';
import type { CertificateReportItemDto } from '@/lib/api/toolbox-talks/certificates';

export default function AdminCertificatesReportPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const status = searchParams.get('status') || undefined;
  const type = searchParams.get('type') || undefined;
  const search = searchParams.get('search') || undefined;
  const page = parseInt(searchParams.get('page') || '1', 10);

  const [searchInput, setSearchInput] = useState(search || '');

  const { data: report, isLoading, error } = useCertificateReport({
    status,
    type,
    search,
    page,
    pageSize: 50,
  });

  const updateUrlParams = useCallback(
    (updates: Record<string, string | null | undefined>) => {
      const params = new URLSearchParams(searchParams.toString());
      Object.entries(updates).forEach(([key, value]) => {
        if (value === null || value === undefined || value === 'all') {
          params.delete(key);
        } else {
          params.set(key, String(value));
        }
      });
      // Reset page when filters change (unless page itself is being updated)
      if (!('page' in updates)) {
        params.delete('page');
      }
      const queryString = params.toString();
      router.push(queryString ? `${pathname}?${queryString}` : pathname);
    },
    [searchParams, pathname, router]
  );

  const handleSearch = useCallback(() => {
    updateUrlParams({ search: searchInput || null });
  }, [searchInput, updateUrlParams]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') {
        handleSearch();
      }
    },
    [handleSearch]
  );

  const handleExportCsv = useCallback(async () => {
    if (!report || report.items.length === 0) {
      toast.error('No data to export');
      return;
    }

    const headers = [
      'Certificate Number',
      'Employee Name',
      'Employee Code',
      'Training Title',
      'Type',
      'Issued',
      'Expires',
      'Status',
      'Refresher',
    ];

    const rows = report.items.map((item) => [
      item.certificateNumber,
      item.employeeName,
      item.employeeCode || '',
      item.trainingTitle,
      item.certificateType,
      format(new Date(item.issuedAt), 'yyyy-MM-dd'),
      item.expiresAt ? format(new Date(item.expiresAt), 'yyyy-MM-dd') : 'No Expiry',
      item.isExpired ? 'Expired' : item.isExpiringSoon ? 'Expiring Soon' : 'Valid',
      item.isRefresher ? 'Yes' : 'No',
    ]);

    const csvContent = [
      headers.join(','),
      ...rows.map((row) => row.map((cell) => `"${cell}"`).join(',')),
    ].join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Certificates_Report_${format(new Date(), 'yyyyMMdd_HHmmss')}.csv`;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
    toast.success('CSV exported successfully');
  }, [report]);

  const handleDownload = useCallback(async (id: string, certificateNumber: string) => {
    try {
      await handleDownloadCertificateAdmin(id, certificateNumber);
    } catch {
      toast.error('Failed to download certificate');
    }
  }, []);

  if (error) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Certificate Report</h1>
          <p className="text-muted-foreground">Error loading report</p>
        </div>
        <Card className="p-8 text-center">
          <p className="text-destructive">Failed to load certificate report. Please try again.</p>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Certificate Report</h1>
          <p className="text-muted-foreground">
            All training certificates across employees
          </p>
        </div>
        <Button variant="outline" onClick={handleExportCsv} disabled={isLoading || !report?.items.length}>
          <Download className="h-4 w-4 mr-2" />
          Export CSV
        </Button>
      </div>

      {/* Stats Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Certificates</CardTitle>
            <Award className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <div className="text-2xl font-bold">{report?.totalCertificates ?? 0}</div>
            )}
          </CardContent>
        </Card>

        <Card className="bg-green-50 border-green-200">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Valid</CardTitle>
            <CheckCircle className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <div className="text-2xl font-bold text-green-700">{report?.validCertificates ?? 0}</div>
            )}
          </CardContent>
        </Card>

        <Card className={(report?.expiringSoonCertificates ?? 0) > 0 ? 'bg-amber-50 border-amber-200' : ''}>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Expiring Soon</CardTitle>
            <AlertTriangle className={`h-4 w-4 ${(report?.expiringSoonCertificates ?? 0) > 0 ? 'text-amber-600' : 'text-muted-foreground'}`} />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <div className={`text-2xl font-bold ${(report?.expiringSoonCertificates ?? 0) > 0 ? 'text-amber-700' : ''}`}>
                {report?.expiringSoonCertificates ?? 0}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className={(report?.expiredCertificates ?? 0) > 0 ? 'bg-red-50 border-red-200' : ''}>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Expired</CardTitle>
            <XCircle className={`h-4 w-4 ${(report?.expiredCertificates ?? 0) > 0 ? 'text-red-600' : 'text-muted-foreground'}`} />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <div className={`text-2xl font-bold ${(report?.expiredCertificates ?? 0) > 0 ? 'text-red-700' : ''}`}>
                {report?.expiredCertificates ?? 0}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Search</label>
              <div className="flex gap-2">
                <Input
                  placeholder="Employee name, training title, or code..."
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={handleKeyDown}
                  className="w-[300px]"
                />
                <Button variant="outline" size="icon" onClick={handleSearch}>
                  <Search className="h-4 w-4" />
                </Button>
              </div>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Status</label>
              <Select
                value={status || 'all'}
                onValueChange={(value) => updateUrlParams({ status: value === 'all' ? null : value })}
              >
                <SelectTrigger className="w-[160px]">
                  <SelectValue placeholder="All Statuses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Statuses</SelectItem>
                  <SelectItem value="valid">Valid</SelectItem>
                  <SelectItem value="expiring">Expiring Soon</SelectItem>
                  <SelectItem value="expired">Expired</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Type</label>
              <Select
                value={type || 'all'}
                onValueChange={(value) => updateUrlParams({ type: value === 'all' ? null : value })}
              >
                <SelectTrigger className="w-[160px]">
                  <SelectValue placeholder="All Types" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Types</SelectItem>
                  <SelectItem value="Talk">Talk</SelectItem>
                  <SelectItem value="Course">Course</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Table */}
      <Card>
        <CardContent className="pt-6">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Training</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Issued</TableHead>
                <TableHead>Expires</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-[80px]"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 10 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-40" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-8" /></TableCell>
                  </TableRow>
                ))
              ) : (report?.items ?? []).length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center text-muted-foreground py-8">
                    No certificates found
                  </TableCell>
                </TableRow>
              ) : (
                report?.items.map((item) => (
                  <CertificateRow key={item.id} item={item} onDownload={handleDownload} />
                ))
              )}
            </TableBody>
          </Table>

          {/* Pagination */}
          {report && report.totalPages > 1 && (
            <div className="flex items-center justify-between mt-4 pt-4 border-t">
              <p className="text-sm text-muted-foreground">
                Showing {(report.page - 1) * report.pageSize + 1} to{' '}
                {Math.min(report.page * report.pageSize, report.totalCount)} of {report.totalCount} certificates
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => updateUrlParams({ page: String(page - 1) })}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= report.totalPages}
                  onClick={() => updateUrlParams({ page: String(page + 1) })}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function CertificateRow({
  item,
  onDownload,
}: {
  item: CertificateReportItemDto;
  onDownload: (id: string, certificateNumber: string) => void;
}) {
  return (
    <TableRow>
      <TableCell>
        <div>
          <div className="font-medium">{item.employeeName}</div>
          {item.employeeCode && (
            <div className="text-xs text-muted-foreground">{item.employeeCode}</div>
          )}
        </div>
      </TableCell>
      <TableCell>
        <div className="flex items-center gap-2">
          <span>{item.trainingTitle}</span>
          {item.isRefresher && (
            <Badge variant="outline" className="text-xs gap-1">
              <RefreshCw className="h-3 w-3" />
              Refresher
            </Badge>
          )}
        </div>
      </TableCell>
      <TableCell>
        <Badge variant="secondary" className="gap-1">
          {item.certificateType === 'Course' ? (
            <GraduationCap className="h-3 w-3" />
          ) : (
            <BookOpen className="h-3 w-3" />
          )}
          {item.certificateType}
        </Badge>
      </TableCell>
      <TableCell>{format(new Date(item.issuedAt), 'dd MMM yyyy')}</TableCell>
      <TableCell>
        {item.expiresAt ? format(new Date(item.expiresAt), 'dd MMM yyyy') : (
          <span className="text-muted-foreground">No expiry</span>
        )}
      </TableCell>
      <TableCell>
        {item.isExpired ? (
          <Badge variant="destructive">Expired</Badge>
        ) : item.isExpiringSoon ? (
          <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">Expiring Soon</Badge>
        ) : (
          <Badge className="bg-green-100 text-green-800 hover:bg-green-100">Valid</Badge>
        )}
      </TableCell>
      <TableCell>
        <Button
          variant="ghost"
          size="icon"
          onClick={() => onDownload(item.id, item.certificateNumber)}
          title="Download certificate"
        >
          <Download className="h-4 w-4" />
        </Button>
      </TableCell>
    </TableRow>
  );
}
