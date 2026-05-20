import { apiClient } from "@/lib/api/client";

export interface TenantUsageRowDto {
  tenantId: string;
  tenantName: string;
  signUpDate: string;
  activeEmployeeCount: number;
  totalLearnings: number;
  newLearnings: number;
  completions: number;
  lastLoginAt: string | null;
  isAtRisk: boolean;
}

export interface CustomerUsageReportDto {
  lastReviewedAt: string | null;
  comparisonDate: string;
  rows: TenantUsageRowDto[];
}

export interface MarkReviewedResponseDto {
  lastReviewedAt: string;
}

export async function getCustomerUsageReport(
  comparisonDate?: string
): Promise<CustomerUsageReportDto> {
  const queryParams = new URLSearchParams();
  if (comparisonDate) {
    queryParams.append("comparisonDate", comparisonDate);
  }
  const queryString = queryParams.toString();
  const url = queryString
    ? `/admin/monitoring/customer-usage?${queryString}`
    : "/admin/monitoring/customer-usage";
  const response = await apiClient.get<CustomerUsageReportDto>(url);
  return response.data;
}

export async function markReviewed(): Promise<MarkReviewedResponseDto> {
  const response = await apiClient.post<MarkReviewedResponseDto>(
    "/admin/monitoring/customer-usage/mark-reviewed"
  );
  return response.data;
}
