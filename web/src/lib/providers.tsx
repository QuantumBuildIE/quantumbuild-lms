"use client";

import { QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { getQueryClient } from "./query-client";
import { AuthProvider } from "./auth/auth-context";
import { useAuth } from "./auth/use-auth";
import { Toaster } from "@/components/ui/sonner";
import { useEffect, useRef, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";

const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function TenantQueryInvalidator() {
  const { activeTenantId } = useAuth();
  const queryClient = useQueryClient();
  const previousTenantId = useRef(activeTenantId);
  const pathname = usePathname();
  const router = useRouter();

  useEffect(() => {
    if (previousTenantId.current !== activeTenantId) {
      previousTenantId.current = activeTenantId;
      queryClient.invalidateQueries();

      // If on a detail/edit page (path contains a UUID segment),
      // redirect to the parent list page
      const segments = pathname.split("/");
      const uuidIndex = segments.findIndex((s) => UUID_REGEX.test(s));
      if (uuidIndex > 0) {
        const parentPath = segments.slice(0, uuidIndex).join("/");
        router.replace(parentPath);
      }
    }
  }, [activeTenantId, queryClient, pathname, router]);

  return null;
}

interface ProvidersProps {
  children: ReactNode;
}

export function Providers({ children }: ProvidersProps) {
  const queryClient = getQueryClient();

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TenantQueryInvalidator />
        {children}
        <Toaster position="top-right" richColors />
      </AuthProvider>
    </QueryClientProvider>
  );
}
