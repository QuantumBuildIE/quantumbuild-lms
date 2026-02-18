"use client";

import { QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { getQueryClient } from "./query-client";
import { AuthProvider } from "./auth/auth-context";
import { useAuth } from "./auth/use-auth";
import { Toaster } from "@/components/ui/sonner";
import { useEffect, useRef, type ReactNode } from "react";

function TenantQueryInvalidator() {
  const { activeTenantId } = useAuth();
  const queryClient = useQueryClient();
  const previousTenantId = useRef(activeTenantId);

  useEffect(() => {
    if (previousTenantId.current !== activeTenantId) {
      previousTenantId.current = activeTenantId;
      queryClient.invalidateQueries();
    }
  }, [activeTenantId, queryClient]);

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
