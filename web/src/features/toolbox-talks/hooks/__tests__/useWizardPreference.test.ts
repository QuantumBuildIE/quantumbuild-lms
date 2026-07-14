import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useWizardPreference } from '../useWizardPreference';

// Mock next/navigation
const mockGet = vi.fn();
vi.mock('next/navigation', () => ({
  useSearchParams: () => ({ get: mockGet }),
}));

// Mock useTenantSettings
const mockUseTenantSettings = vi.fn();
vi.mock('@/lib/api/admin/use-tenant-settings', () => ({
  useTenantSettings: () => mockUseTenantSettings(),
  useUpdateTenantSettings: () => ({ mutateAsync: vi.fn() }),
}));

describe('useWizardPreference', () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockUseTenantSettings.mockReset();
  });

  it('returns "new" when URL param is wizard=new, regardless of TenantSettings', () => {
    mockGet.mockImplementation((key: string) => (key === 'wizard' ? 'new' : null));
    mockUseTenantSettings.mockReturnValue({ data: { UseNewWizard: 'false' } });

    const { result } = renderHook(() => useWizardPreference());
    expect(result.current).toBe('new');
  });

  it('returns "old" when URL param is wizard=old, regardless of TenantSettings', () => {
    mockGet.mockImplementation((key: string) => (key === 'wizard' ? 'old' : null));
    mockUseTenantSettings.mockReturnValue({ data: { UseNewWizard: 'true' } });

    const { result } = renderHook(() => useWizardPreference());
    expect(result.current).toBe('old');
  });

  it('returns "new" when no URL param and TenantSettings UseNewWizard is "true"', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: { UseNewWizard: 'true' } });

    const { result } = renderHook(() => useWizardPreference());
    expect(result.current).toBe('new');
  });

  it('returns "old" when no URL param and TenantSettings UseNewWizard is "false"', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: { UseNewWizard: 'false' } });

    const { result } = renderHook(() => useWizardPreference());
    expect(result.current).toBe('old');
  });

  it('returns "old" when no URL param and TenantSettings key is absent (default)', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: {} });

    const { result } = renderHook(() => useWizardPreference());
    expect(result.current).toBe('old');
  });

  it('returns "old" when no URL param and TenantSettings data is undefined (loading)', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: undefined });

    const { result } = renderHook(() => useWizardPreference());
    expect(result.current).toBe('old');
  });
});
