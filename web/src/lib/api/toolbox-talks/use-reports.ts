import { useQuery, useMutation } from '@tanstack/react-query';
import {
  getComplianceReport,
  getOverdueReport,
  getCompletionsReport,
  exportOverdueReport,
  exportCompletionsReport,
  exportComplianceReport,
  type ComplianceReportParams,
  type OverdueReportParams,
  type CompletionsReportParams,
} from './reports';

// ============================================
// Query Keys
// ============================================

export const TOOLBOX_TALKS_REPORTS_KEY = ['toolbox-talks', 'reports'];

// ============================================
// Report Query Hooks
// ============================================

export function useComplianceReport(params?: ComplianceReportParams) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_REPORTS_KEY, 'compliance', params],
    queryFn: () => getComplianceReport(params),
  });
}

export function useOverdueReport(params?: OverdueReportParams) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_REPORTS_KEY, 'overdue', params],
    queryFn: () => getOverdueReport(params),
  });
}

export function useCompletionsReport(params?: CompletionsReportParams) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_REPORTS_KEY, 'completions', params],
    queryFn: () => getCompletionsReport(params),
  });
}

// ============================================
// Export Mutation Hooks
// ============================================

export function useExportOverdueReport() {
  return useMutation({
    mutationFn: async (params?: OverdueReportParams) => {
      const blob = await exportOverdueReport(params);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `OverdueToolboxTalks_${new Date().toISOString().slice(0, 10)}.xlsx`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    },
  });
}

export function useExportCompletionsReport() {
  return useMutation({
    mutationFn: async (params?: Omit<CompletionsReportParams, 'pageNumber' | 'pageSize'>) => {
      const blob = await exportCompletionsReport(params);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ToolboxTalkCompletions_${new Date().toISOString().slice(0, 10)}.xlsx`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    },
  });
}

export function useExportComplianceReport() {
  return useMutation({
    mutationFn: async (params?: ComplianceReportParams) => {
      const blob = await exportComplianceReport(params);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ToolboxTalkCompliance_${new Date().toISOString().slice(0, 10)}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    },
  });
}
