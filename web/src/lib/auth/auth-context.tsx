"use client";

import { createContext, useCallback, useEffect, useState, type ReactNode } from "react";
import { apiClient, getStoredToken, setStoredToken, clearStoredTokens, setRememberMe, getRememberMe } from "@/lib/api/client";
import type { User, LoginResponse, MeResponse } from "@/types/auth";

interface AuthContextType {
  user: User | null;
  token: string | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  dpaAccepted: boolean;
  activeTenantId: string | null;
  setActiveTenantId: (tenantId: string | null) => void;
  login: (email: string, password: string, rememberMe?: boolean) => Promise<{ success: boolean; error?: string; user?: User }>;
  logout: () => void;
  refreshDpaStatus: () => Promise<void>;
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
  dpaAccepted: boolean;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [authState, setAuthState] = useState<AuthState>({
    user: null,
    token: null,
    isLoading: true,
    dpaAccepted: false,
  });
  const [activeTenantId, setActiveTenantIdState] = useState<string | null>(null);

  const { user, token, isLoading, dpaAccepted } = authState;

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

  const fetchDpaStatus = useCallback(async (accessToken: string): Promise<boolean> => {
    try {
      const response = await apiClient.get<{ accepted: boolean; version: string }>("/dpa/status", {
        headers: { Authorization: `Bearer ${accessToken}` },
      });
      return response.data.accepted;
    } catch {
      return false;
    }
  }, []);

  const loadUser = useCallback(async (accessToken: string): Promise<User | null> => {
    try {
      // The /me endpoint returns the user data directly, not wrapped in ApiResponse
      const response = await apiClient.get<MeResponse>("/auth/me", {
        headers: { Authorization: `Bearer ${accessToken}` },
      });

      const userData = response.data;
      if (userData && userData.id) {
        const loadedUser: User = {
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

        // SuperUsers bypass DPA gate
        const dpaStatus = loadedUser.isSuperUser ? true : await fetchDpaStatus(accessToken);

        setAuthState({
          user: loadedUser,
          token: accessToken,
          isLoading: false,
          dpaAccepted: dpaStatus,
        });
        return loadedUser;
      }
      return null;
    } catch {
      return null;
    }
  }, [fetchDpaStatus]);

  useEffect(() => {
    const initAuth = async () => {
      // Restore active tenant for super users
      const storedTenantId = typeof window !== "undefined" ? localStorage.getItem("activeTenantId") : null;
      if (storedTenantId) {
        setActiveTenantIdState(storedTenantId);
      }

      const storedToken = getStoredToken("accessToken");
      if (storedToken) {
        const loadedUser = await loadUser(storedToken);
        if (!loadedUser) {
          // Token refresh is handled by the API client interceptor
          // If we still fail after refresh, clear tokens
          const newToken = getStoredToken("accessToken");
          if (newToken && newToken !== storedToken) {
            // Token was refreshed, try again
            const retryUser = await loadUser(newToken);
            if (!retryUser) {
              clearStoredTokens();
              setAuthState({ user: null, token: null, isLoading: false, dpaAccepted: false });
            }
          } else {
            clearStoredTokens();
            setAuthState({ user: null, token: null, isLoading: false, dpaAccepted: false });
          }
        }
        // If loadUser succeeded, it already set isLoading: false atomically
      } else {
        setAuthState({ user: null, token: null, isLoading: false, dpaAccepted: false });
      }
    };

    initAuth();
  }, [loadUser]);

  const login = async (email: string, password: string, rememberMe: boolean = true): Promise<{ success: boolean; error?: string; user?: User }> => {
    try {
      const response = await apiClient.post<LoginResponse>("/auth/login", { email, password });

      if (response.data.success) {
        const { accessToken, refreshToken } = response.data;

        setRememberMe(rememberMe);
        setStoredToken("accessToken", accessToken, rememberMe);
        setStoredToken("refreshToken", refreshToken, rememberMe);

        // Hydrate full user from /me to get complete data (enabledModules, etc.)
        // rather than relying on the login response which may have a subset of fields
        const hydratedUser = await loadUser(accessToken);
        if (hydratedUser) {
          return { success: true, user: hydratedUser };
        }

        // Fallback: construct from login response if /me fails
        const userData = response.data.user;
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

        const fallbackDpa = loggedInUser.isSuperUser ? true : await fetchDpaStatus(accessToken);
        setAuthState({ user: loggedInUser, token: accessToken, isLoading: false, dpaAccepted: fallbackDpa });

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

  const refreshDpaStatus = useCallback(async () => {
    const currentToken = getStoredToken("accessToken");
    if (currentToken) {
      const status = await fetchDpaStatus(currentToken);
      setAuthState((prev) => ({ ...prev, dpaAccepted: status }));
    }
  }, [fetchDpaStatus]);

  const logout = useCallback(() => {
    clearStoredTokens();
    setAuthState({ user: null, token: null, isLoading: false, dpaAccepted: false });
    setActiveTenantId(null);
  }, [setActiveTenantId]);

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isLoading,
        isAuthenticated,
        dpaAccepted,
        activeTenantId,
        setActiveTenantId,
        login,
        logout,
        refreshDpaStatus,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
