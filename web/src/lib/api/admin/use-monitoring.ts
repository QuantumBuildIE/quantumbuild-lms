import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getCustomerUsageReport, markReviewed } from "./monitoring";

export const CUSTOMER_USAGE_REPORT_KEY = ["customer-usage-report"];

export function useCustomerUsageReport(comparisonDate?: string) {
  return useQuery({
    queryKey: [...CUSTOMER_USAGE_REPORT_KEY, comparisonDate],
    queryFn: () => getCustomerUsageReport(comparisonDate),
  });
}

export function useMarkReviewed() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: markReviewed,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CUSTOMER_USAGE_REPORT_KEY });
    },
  });
}

export type { CustomerUsageReportDto, TenantUsageRowDto, MarkReviewedResponseDto } from "./monitoring";
