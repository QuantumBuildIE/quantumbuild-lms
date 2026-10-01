import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

const mockUseAuth = vi.fn();
vi.mock('@/lib/auth/use-auth', () => ({ useAuth: () => mockUseAuth() }));

const mockGet = vi.fn();
vi.mock('@/lib/api/client', () => ({
  apiClient: { get: (...a: unknown[]) => mockGet(...a) },
}));

import { usePipelineAuditDashboard } from '../use-pipeline-audit';

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  mockUseAuth.mockReset();
  mockGet.mockReset();
  mockGet.mockResolvedValue({ data: { systemGlossaryTermCount: 3, tenantOverrideTermCount: 1 } });
});

describe('usePipelineAuditDashboard', () => {
  it('does not fire for a SuperUser with no active tenant', async () => {
    mockUseAuth.mockReturnValue({ user: { isSuperUser: true }, activeTenantId: null });
    const { result } = renderHook(() => usePipelineAuditDashboard(), { wrapper });

    await new Promise((r) => setTimeout(r, 20));
    expect(mockGet).not.toHaveBeenCalled();
    expect(result.current.data).toBeUndefined();
  });

  it('fires for a SuperUser with an active tenant, without a per-request header', async () => {
    mockUseAuth.mockReturnValue({ user: { isSuperUser: true }, activeTenantId: 'obrien' });
    const { result } = renderHook(() => usePipelineAuditDashboard(), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(1);
    expect(mockGet).toHaveBeenCalledWith('/toolbox-talks/pipeline/dashboard');
  });

  it('fires for a tenant user with no active tenant', async () => {
    mockUseAuth.mockReturnValue({ user: { isSuperUser: false }, activeTenantId: null });
    const { result } = renderHook(() => usePipelineAuditDashboard(), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(1);
  });
});
