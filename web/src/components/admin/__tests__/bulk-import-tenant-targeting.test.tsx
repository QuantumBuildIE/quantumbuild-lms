import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { InternalAxiosRequestConfig } from 'axios';

const mockUseAuth = vi.fn();
vi.mock('@/lib/auth/use-auth', () => ({
  useAuth: () => mockUseAuth(),
  useIsSuperUser: () => mockUseAuth().user?.isSuperUser ?? false,
}));

vi.mock('@/lib/api/admin/use-tenants', () => ({
  useTenants: () => ({
    data: { items: [{ id: 'tenant-x', name: 'Tenant X' }, { id: 'tenant-y', name: 'Tenant Y' }] },
  }),
}));

vi.mock('sonner', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));

import { apiClient } from '@/lib/api/client';
import { BulkImportUploadPanel } from '../bulk-import-upload-panel';
import { BulkSopImportUploadPanel } from '@/features/toolbox-talks/components/bulk-sop-import/BulkSopImportUploadPanel';

const superUser = { isSuperUser: true, tenantId: 'home' };
const tenantUser = { isSuperUser: false, tenantId: 'own' };

let requests: InternalAxiosRequestConfig[];

beforeEach(() => {
  mockUseAuth.mockReset();
  requests = [];
  localStorage.clear();
  // Capture the final request config (after interceptors) instead of hitting the network.
  apiClient.defaults.adapter = async (config) => {
    requests.push(config);
    return {
      data: { success: true, data: { sessionId: 's1', validation: {} } },
      status: 200,
      statusText: 'OK',
      headers: {},
      config,
    };
  };
});

function renderPanel(Panel: typeof BulkImportUploadPanel | typeof BulkSopImportUploadPanel) {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <Panel onSuccess={() => {}} />
    </QueryClientProvider>
  );
}

const panels = [
  { name: 'employee panel', Panel: BulkImportUploadPanel, accept: 'input[accept=".csv"]', file: new File(['a'], 'a.csv', { type: 'text/csv' }), path: '/employees/bulk-import' },
  { name: 'SOP panel', Panel: BulkSopImportUploadPanel, accept: 'input[accept=".zip"]', file: new File(['a'], 'a.zip', { type: 'application/zip' }), path: '/toolbox-talks/bulk-sop-import' },
];

describe.each(panels)('$name tenant targeting', ({ Panel, accept, file, path }) => {
  async function pickFile(container: HTMLElement) {
    const input = container.querySelector(accept) as HTMLInputElement;
    await userEvent.upload(input, file);
  }

  it('SuperUser with active tenant: sends the active tenant, shows it, renders no picker', async () => {
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: 'tenant-x' });
    localStorage.setItem('activeTenantId', 'tenant-x');
    const { container } = renderPanel(Panel);

    expect(screen.getByText('Tenant X')).toBeInTheDocument();
    expect(screen.getByText(/Importing into/)).toBeInTheDocument();
    expect(screen.queryByText('Target Tenant')).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();

    await pickFile(container);
    await userEvent.click(screen.getByRole('button', { name: /Upload & Validate/ }));

    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].url).toBe(path);
    expect(requests[0].headers['X-Tenant-Id']).toBe('tenant-x');
  });

  it('SuperUser with no active tenant: upload disabled and message shown', async () => {
    mockUseAuth.mockReturnValue({ user: superUser, activeTenantId: null });
    const { container } = renderPanel(Panel);

    expect(screen.getByText(/Select a tenant using the tenant switcher/)).toBeInTheDocument();
    await pickFile(container);
    expect(screen.getByRole('button', { name: /Upload & Validate/ })).toBeDisabled();
    expect(requests).toHaveLength(0);
  });

  it('tenant user: unchanged, no banner, upload enabled, no tenant override header', async () => {
    mockUseAuth.mockReturnValue({ user: tenantUser, activeTenantId: null });
    const { container } = renderPanel(Panel);

    expect(screen.queryByText(/Importing into/)).not.toBeInTheDocument();
    expect(screen.queryByText(/tenant switcher/)).not.toBeInTheDocument();

    await pickFile(container);
    const button = screen.getByRole('button', { name: /Upload & Validate/ });
    expect(button).toBeEnabled();
    await userEvent.click(button);

    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].headers['X-Tenant-Id']).toBeUndefined();
  });
});
