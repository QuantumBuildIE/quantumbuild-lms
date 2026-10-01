import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

const mockUseAuth = vi.fn();
vi.mock('@/lib/auth/use-auth', () => ({ useAuth: () => mockUseAuth() }));

const mockGet = vi.fn();
const mockPut = vi.fn();
const mockDelete = vi.fn();
vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: (...a: unknown[]) => mockGet(...a),
    put: (...a: unknown[]) => mockPut(...a),
    delete: (...a: unknown[]) => mockDelete(...a),
  },
}));

import {
  brandingKeys,
  getEffectiveTenantId,
  useCurrentBranding,
  useDeleteTenantLogo,
  useTenantLogo,
  useUploadTenantLogo,
} from '../use-branding';

function makeWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return { queryClient, wrapper };
}

const superUser = { isSuperUser: true, tenantId: 'home-tenant' };
const tenantUser = { isSuperUser: false, tenantId: 'own-tenant' };

beforeEach(() => {
  mockUseAuth.mockReset();
  mockGet.mockReset();
  mockPut.mockReset();
  mockDelete.mockReset();
  mockGet.mockResolvedValue({ data: { logoUrl: 'https://cdn.example/l.png', tenantName: 'Acme' } });
});

describe('getEffectiveTenantId', () => {
  it('uses the active tenant for a SuperUser, never their home tenant', () => {
    expect(getEffectiveTenantId(superUser, 'obrien')).toBe('obrien');
    expect(getEffectiveTenantId(superUser, null)).toBeNull();
  });

  it('uses the user tenant for everyone else, ignoring activeTenantId', () => {
    expect(getEffectiveTenantId(tenantUser, 'obrien')).toBe('own-tenant');
  });
});

describe('useCurrentBranding', () => {
  it('keys the query by the active tenant and refetches when it changes', async () => {
    const { queryClient, wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: 'obrien' });
    const { result, rerender } = renderHook(() => useCurrentBranding(), { wrapper });

    await waitFor(() => expect(result.current.logoUrl).toBe('https://cdn.example/l.png'));
    expect(queryClient.getQueryCache().find({ queryKey: brandingKeys.current('obrien') })).toBeDefined();

    mockGet.mockResolvedValue({ data: { logoUrl: null } });
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: 'other' });
    rerender();

    // Immediately after switching: no stale logo, loading placeholder instead.
    expect(result.current.logoUrl).toBeNull();
    expect(result.current.isLoading).toBe(true);
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(queryClient.getQueryCache().find({ queryKey: brandingKeys.current('other') })).toBeDefined();
    expect(mockGet).toHaveBeenCalledTimes(2);
  });

  it('does not fetch for a SuperUser on All Tenants', () => {
    const { wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: null });
    const { result } = renderHook(() => useCurrentBranding(), { wrapper });
    expect(result.current).toEqual({ logoUrl: null, tenantName: null, isLoading: false });
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('keys by the user tenant for a non-SuperUser', async () => {
    const { queryClient, wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: tenantUser, activeTenantId: null });
    const { result } = renderHook(() => useCurrentBranding(), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(queryClient.getQueryCache().find({ queryKey: brandingKeys.current('own-tenant') })).toBeDefined();
  });
});

describe('useCurrentBranding tenantName', () => {
  it('exposes the tenantName from /branding/current for any user', async () => {
    const { wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: tenantUser, activeTenantId: null });
    const { result } = renderHook(() => useCurrentBranding(), { wrapper });
    await waitFor(() => expect(result.current.tenantName).toBe('Acme'));
    expect(mockGet).toHaveBeenCalledWith('/branding/current');
  });

  it('has a null tenantName for a SuperUser on All Tenants', () => {
    const { wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: null });
    const { result } = renderHook(() => useCurrentBranding(), { wrapper });
    expect(result.current.tenantName).toBeNull();
  });
});

describe('useTenantLogo', () => {
  it('reads via the tenant endpoint with no X-Tenant-Id header override', async () => {
    const { wrapper } = makeWrapper();
    mockGet.mockResolvedValue({ data: { logoUrl: 'https://cdn.example/edited.png' } });
    const { result } = renderHook(() => useTenantLogo('edited-tenant'), { wrapper });
    await waitFor(() => expect(result.current.data?.logoUrl).toBe('https://cdn.example/edited.png'));
    expect(mockGet).toHaveBeenCalledTimes(1);
    expect(mockGet.mock.calls[0]).toEqual(['/tenants/edited-tenant/branding']);
  });
});

describe('branding query keys', () => {
  it('card key and header key never collide, even for the same tenant id', () => {
    expect(brandingKeys.tenant('t1')).toEqual(['branding', 'tenant', 't1']);
    expect(brandingKeys.current('t1')).not.toEqual(brandingKeys.tenant('t1'));
    expect(brandingKeys.current(null)).not.toEqual(brandingKeys.tenant('null'));
  });

  it('card and header queries for the same tenant are cached separately', async () => {
    const { queryClient, wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: tenantUser, activeTenantId: null });
    renderHook(() => ({ header: useCurrentBranding(), card: useTenantLogo('own-tenant') }), { wrapper });
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2));
    const cache = queryClient.getQueryCache();
    expect(cache.find({ queryKey: brandingKeys.current('own-tenant'), exact: true })).toBeDefined();
    expect(cache.find({ queryKey: brandingKeys.tenant('own-tenant'), exact: true })).toBeDefined();
    expect(cache.findAll({ queryKey: ['branding'] })).toHaveLength(2);
  });

  it('upload invalidates the shared branding prefix, refetching both header and card queries', async () => {
    const { wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: tenantUser, activeTenantId: null });
    mockPut.mockResolvedValue({ data: { logoUrl: 'x' } });
    const { result } = renderHook(
      () => ({
        header: useCurrentBranding(),
        card: useTenantLogo('own-tenant'),
        upload: useUploadTenantLogo('own-tenant'),
      }),
      { wrapper }
    );
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(2));
    await act(async () => {
      await result.current.upload.mutateAsync(new File(['x'], 'l.png', { type: 'image/png' }));
    });
    await waitFor(() => expect(mockGet).toHaveBeenCalledTimes(4));
  });
});

describe('logo mutations', () => {
  it('upload targets the EDITED tenant id, not the active tenant, and invalidates branding', async () => {
    const { queryClient, wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: 'active-tenant' });
    mockPut.mockResolvedValue({ data: { logoUrl: 'https://cdn.example/new.png' } });
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    const { result } = renderHook(() => useUploadTenantLogo('edited-tenant'), { wrapper });
    const file = new File(['x'], 'logo.png', { type: 'image/png' });
    await act(async () => {
      await result.current.mutateAsync(file);
    });

    expect(mockPut).toHaveBeenCalledTimes(1);
    expect(mockPut.mock.calls[0][0]).toBe('/tenants/edited-tenant/branding/logo');
    expect(mockPut.mock.calls[0][0]).not.toContain('active-tenant');
    const form = mockPut.mock.calls[0][1] as FormData;
    expect(form.get('file')).toBe(file);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['branding'] });
  });

  it('delete targets the edited tenant id and invalidates branding', async () => {
    const { queryClient, wrapper } = makeWrapper();
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: 'active-tenant' });
    mockDelete.mockResolvedValue({});
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    const { result } = renderHook(() => useDeleteTenantLogo('edited-tenant'), { wrapper });
    await act(async () => {
      await result.current.mutateAsync();
    });

    expect(mockDelete).toHaveBeenCalledWith('/tenants/edited-tenant/branding/logo');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['branding'] });
  });
});
