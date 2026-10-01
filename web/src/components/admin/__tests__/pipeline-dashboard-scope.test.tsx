import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

const mockUseCurrentBranding = vi.fn();
vi.mock('@/lib/api/branding/use-branding', () => ({
  useCurrentBranding: () => mockUseCurrentBranding(),
}));

import {
  AllTenantsScope,
  DashboardTenantLabel,
  GlossaryTermsCard,
} from '../pipeline-dashboard-scope';

beforeEach(() => mockUseCurrentBranding.mockReset());

describe('DashboardTenantLabel', () => {
  it('shows the tenant name from current branding', () => {
    mockUseCurrentBranding.mockReturnValue({ tenantName: 'Acme Care', isLoading: false });
    render(<DashboardTenantLabel />);
    expect(screen.getByText(/Showing:/)).toBeInTheDocument();
    expect(screen.getByText('Acme Care')).toBeInTheDocument();
  });

  it('shows a loading placeholder while the name loads', () => {
    mockUseCurrentBranding.mockReturnValue({ tenantName: null, isLoading: true });
    render(<DashboardTenantLabel />);
    expect(screen.getByText('Loading tenant...')).toBeInTheDocument();
  });

  it('renders nothing when there is no tenant', () => {
    mockUseCurrentBranding.mockReturnValue({ tenantName: null, isLoading: false });
    const { container } = render(<DashboardTenantLabel />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('AllTenantsScope', () => {
  it('renders the All tenants label', () => {
    render(<AllTenantsScope />);
    expect(screen.getByText('All tenants')).toBeInTheDocument();
  });
});

describe('GlossaryTermsCard', () => {
  it('renders both counts with the override and sector notes', () => {
    render(<GlossaryTermsCard systemCount={42} overrideCount={7} />);
    expect(screen.getByText(/System glossary \(your sectors\):/)).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText(/Your overrides \(your sectors\):/)).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByText(/Overrides replace matching system terms/)).toBeInTheDocument();
    expect(screen.getByText(/one selected sector/)).toBeInTheDocument();
  });

  it('shows zero overrides', () => {
    render(<GlossaryTermsCard systemCount={10} overrideCount={0} />);
    expect(screen.getByText('0')).toBeInTheDocument();
  });
});
