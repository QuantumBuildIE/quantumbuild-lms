import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useCoursePreference } from '../useCoursePreference';

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

describe('useCoursePreference', () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockUseTenantSettings.mockReset();
  });

  it('returns true when URL param is coursemode=new, regardless of TenantSettings', () => {
    mockGet.mockImplementation((key: string) => (key === 'coursemode' ? 'new' : null));
    mockUseTenantSettings.mockReturnValue({ data: { UseNewCourseCreation: 'false' } });

    const { result } = renderHook(() => useCoursePreference());
    expect(result.current).toBe(true);
  });

  it('returns false when URL param is coursemode=old, regardless of TenantSettings', () => {
    mockGet.mockImplementation((key: string) => (key === 'coursemode' ? 'old' : null));
    mockUseTenantSettings.mockReturnValue({ data: { UseNewCourseCreation: 'true' } });

    const { result } = renderHook(() => useCoursePreference());
    expect(result.current).toBe(false);
  });

  it('returns true when no URL param and TenantSettings UseNewCourseCreation is "true"', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: { UseNewCourseCreation: 'true' } });

    const { result } = renderHook(() => useCoursePreference());
    expect(result.current).toBe(true);
  });

  it('returns false when no URL param and TenantSettings UseNewCourseCreation is "false"', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: { UseNewCourseCreation: 'false' } });

    const { result } = renderHook(() => useCoursePreference());
    expect(result.current).toBe(false);
  });

  it('returns true when no URL param and TenantSettings key is absent (default)', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: {} });

    const { result } = renderHook(() => useCoursePreference());
    expect(result.current).toBe(true);
  });

  it('returns true when no URL param and TenantSettings data is undefined (loading)', () => {
    mockGet.mockReturnValue(null);
    mockUseTenantSettings.mockReturnValue({ data: undefined });

    const { result } = renderHook(() => useCoursePreference());
    expect(result.current).toBe(true);
  });
});
