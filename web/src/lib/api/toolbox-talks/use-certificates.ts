import { useQuery } from '@tanstack/react-query';
import { getMyCertificates, downloadCertificate, getEmployeeCertificates, downloadCertificateAdmin, getCertificateReport } from './certificates';
import type { CertificateReportParams } from './certificates';

// ============================================
// Query Keys
// ============================================

export const MY_CERTIFICATES_KEY = ['my-certificates'];
export const EMPLOYEE_CERTIFICATES_KEY = ['employee-certificates'];
export const CERTIFICATE_REPORT_KEY = ['certificate-report'];

// ============================================
// Certificate Query Hooks
// ============================================

export function useMyCertificates() {
  return useQuery({
    queryKey: MY_CERTIFICATES_KEY,
    queryFn: getMyCertificates,
  });
}

export function useEmployeeCertificates(employeeId: string | undefined) {
  return useQuery({
    queryKey: [...EMPLOYEE_CERTIFICATES_KEY, employeeId],
    queryFn: () => getEmployeeCertificates(employeeId!),
    enabled: !!employeeId,
  });
}

export function useCertificateReport(params: CertificateReportParams) {
  return useQuery({
    queryKey: [...CERTIFICATE_REPORT_KEY, params],
    queryFn: () => getCertificateReport(params),
  });
}

// ============================================
// Download Helpers
// ============================================

export async function handleDownloadCertificate(id: string, certificateNumber: string) {
  const blob = await downloadCertificate(id);
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `Certificate-${certificateNumber}.pdf`;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
}

export async function handleDownloadCertificateAdmin(id: string, certificateNumber: string) {
  const blob = await downloadCertificateAdmin(id);
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `Certificate-${certificateNumber}.pdf`;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
}
