"use client";

import { createContext, useCallback, useEffect, useState, type ReactNode } from "react";
import { apiClient, getStoredToken, setStoredToken, clearStoredTokens, setRememberMe, getRememberMe } from "@/lib/api/client";
import type { User, LoginResponse, MeResponse } from "@/types/auth";

interface AuthContextType {
  user: User | null;
  token: string | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  activeTenantId: string | null;
  setActiveTenantId: (tenantId: string | null) => void;
  login: (email: string, password: string, rememberMe?: boolean) => Promise<{ success: boolean; error?: string; user?: User }>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

// Combined auth state guarantees atomic transitions — user, token, and
// isLoading always update in a single setState call, eliminating any
// intermediate renders where isLoading is false but user data is stale.
interface AuthState {
  user: User | null;
  token: string | null;
  isLoading: boolean;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [authState, setAuthState] = useState<AuthState>({
    user: null,
    token: null,
    isLoading: true,
  });
  const [activeTenantId, setActiveTenantIdState] = useState<string | null>(null);

  const { user, token, isLoading } = authState;

  const setActiveTenantId = useCallback((tenantId: string | null) => {
    setActiveTenantIdState(tenantId);
    if (typeof window !== "undefined") {
      if (tenantId) {
        localStorage.setItem("activeTenantId", tenantId);
      } else {
        localStorage.removeItem("activeTenantId");
      }
    }
  }, []);

  const isAuthenticated = !!user && !!token;

  const loadUser = useCallback(async (accessToken: string) => {
    try {
      // The /me endpoint returns the user data directly, not wrapped in ApiResponse
      const response = await apiClient.get<MeResponse>("/auth/me", {
        headers: { Authorization: `Bearer ${accessToken}` },
      });

      const userData = response.data;
      if (userData && userData.id) {
        setAuthState({
          user: {
            id: userData.id,
            email: userData.email,
            firstName: userData.firstName,
            lastName: userData.lastName,
            tenantId: userData.tenantId,
            roles: userData.roles,
            permissions: userData.permissions,
            isSuperUser: userData.isSuperUser ?? false,
            employeeId: userData.employeeId ?? null,
            enabledModules: userData.enabledModules ?? [],
          },
          token: accessToken,
          isLoading: false,
        });
        return true;
      }
      return false;
    } catch {
      return false;
    }
  }, []);

  useEffect(() => {
    const initAuth = async () => {
      // Restore active tenant for super users
      const storedTenantId = typeof window !== "undefined" ? localStorage.getItem("activeTenantId") : null;
      if (storedTenantId) {
        setActiveTenantIdState(storedTenantId);
      }

      const storedToken = getStoredToken("accessToken");
      if (storedToken) {
        const success = await loadUser(storedToken);
        if (!success) {
          // Token refresh is handled by the API client interceptor
          // If we still fail after refresh, clear tokens
          const newToken = getStoredToken("accessToken");
          if (newToken && newToken !== storedToken) {
            // Token was refreshed, try again
            const retrySuccess = await loadUser(newToken);
            if (!retrySuccess) {
              clearStoredTokens();
              setAuthState({ user: null, token: null, isLoading: false });
            }
          } else {
            clearStoredTokens();
            setAuthState({ user: null, token: null, isLoading: false });
          }
        }
        // If loadUser succeeded, it already set isLoading: false atomically
      } else {
        setAuthState({ user: null, token: null, isLoading: false });
      }
    };

    initAuth();
  }, [loadUser]);

  const login = async (email: string, password: string, rememberMe: boolean = true): Promise<{ success: boolean; error?: string; user?: User }> => {
    try {
      const response = await apiClient.post<LoginResponse>("/auth/login", { email, password });

      if (response.data.success) {
        const { accessToken, refreshToken, user: userData } = response.data;

        setRememberMe(rememberMe);
        setStoredToken("accessToken", accessToken, rememberMe);
        setStoredToken("refreshToken", refreshToken, rememberMe);

        const loggedInUser: User = {
          id: userData.id,
          email: userData.email,
          firstName: userData.firstName,
          lastName: userData.lastName,
          tenantId: userData.tenantId,
          roles: userData.roles,
          permissions: userData.permissions,
          isSuperUser: userData.isSuperUser ?? false,
          employeeId: userData.employeeId ?? null,
          enabledModules: userData.enabledModules ?? [],
        };

        setAuthState({ user: loggedInUser, token: accessToken, isLoading: false });

        return { success: true, user: loggedInUser };
      }

      return {
        success: false,
        error: response.data.errors?.join(", ") || "Login failed",
      };
    } catch (error) {
      const message = error instanceof Error ? error.message : "An error occurred during login";
      return { success: false, error: message };
    }
  };

  const logout = useCallback(() => {
    clearStoredTokens();
    setAuthState({ user: null, token: null, isLoading: false });
    setActiveTenantId(null);
  }, [setActiveTenantId]);

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isLoading,
        isAuthenticated,
        activeTenantId,
        setActiveTenantId,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
