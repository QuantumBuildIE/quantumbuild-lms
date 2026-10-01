"use client";

import { useState } from "react";
import { CertifiedIqLogo } from "./certifiediq-logo";

interface BrandLockupProps {
  /** Tenant logo URL; null/undefined renders the plain CertifiedIQ logo. */
  logoUrl: string | null | undefined;
  /** Tenant name, used as the logo alt text. */
  alt: string;
  /** Renders a fixed-size empty placeholder (branding still loading). */
  isLoading?: boolean;
}

/**
 * The brand area of the header: tenant logo + 'Powered by CertifiedIQ' when a logo is set,
 * otherwise the CertifiedIQ logo alone. Shared by the header and the Tenant Management preview
 * so the preview shows exactly what the header will.
 */
export function BrandLockup({ logoUrl, alt, isLoading = false }: BrandLockupProps) {
  // Track the URL that failed (not a boolean) so a new URL gets a fresh attempt.
  const [failedUrl, setFailedUrl] = useState<string | null>(null);

  if (isLoading) {
    return <div data-testid="brand-placeholder" className="h-8 w-36" aria-hidden />;
  }

  if (!logoUrl || failedUrl === logoUrl) {
    return <CertifiedIqLogo />;
  }

  return (
    <div className="flex items-center gap-3">
      {/* Plain <img>: tenant logos are arbitrary R2 URLs, not configured for next/image. */}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={logoUrl}
        alt={alt}
        className="h-10 max-w-[200px] object-contain"
        onError={() => setFailedUrl(logoUrl)}
      />
      <div className="hidden sm:flex items-center gap-3" data-testid="powered-by">
        <span className="h-6 w-px bg-border" aria-hidden />
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
            Powered by
          </span>
          <CertifiedIqLogo size="sm" />
        </div>
      </div>
    </div>
  );
}
