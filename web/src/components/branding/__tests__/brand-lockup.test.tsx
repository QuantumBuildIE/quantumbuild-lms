import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { BrandLockup } from '../brand-lockup';

describe('BrandLockup', () => {
  it('renders the CertifiedIQ logo with no badge or image when there is no tenant logo', () => {
    const { container } = render(<BrandLockup logoUrl={null} alt="Acme" />);
    expect(container.querySelector('svg')).toBeInTheDocument();
    expect(container.querySelector('img')).not.toBeInTheDocument();
    expect(screen.queryByText('Powered by')).not.toBeInTheDocument();
  });

  it('renders the tenant logo, divider and Powered by badge when a logo is set', () => {
    render(<BrandLockup logoUrl="https://cdn.example/logo.png" alt="Acme" />);
    const img = screen.getByRole('img', { name: 'Acme' });
    expect(img).toHaveAttribute('src', 'https://cdn.example/logo.png');
    expect(screen.getByText('Powered by')).toBeInTheDocument();
    // badge hidden below sm
    expect(screen.getByTestId('powered-by').className).toContain('hidden');
    expect(screen.getByTestId('powered-by').className).toContain('sm:flex');
  });

  it('falls back to the CertifiedIQ logo with no badge when the image fails to load', () => {
    const { container } = render(<BrandLockup logoUrl="https://cdn.example/broken.png" alt="Acme" />);
    fireEvent.error(screen.getByRole('img', { name: 'Acme' }));
    expect(screen.queryByRole('img', { name: 'Acme' })).not.toBeInTheDocument();
    expect(screen.queryByText('Powered by')).not.toBeInTheDocument();
    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('renders only an empty placeholder while loading (no CertifiedIQ flash)', () => {
    const { container } = render(<BrandLockup logoUrl={null} alt="Acme" isLoading />);
    expect(screen.getByTestId('brand-placeholder')).toBeInTheDocument();
    expect(container.querySelector('svg')).not.toBeInTheDocument();
    expect(container.textContent).toBe('');
  });
});
