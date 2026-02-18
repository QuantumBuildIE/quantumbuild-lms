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

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [activeTenantId, setActiveTenantIdState] = useState<string | null>(null);

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
        setUser({
          id: userData.id,
          email: userData.email,
          firstName: userData.firstName,
          lastName: userData.lastName,
          tenantId: userData.tenantId,
          roles: userData.roles,
          permissions: userData.permissions,
          isSuperUser: userData.isSuperUser ?? false,
        });
        setToken(accessToken);
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
            await loadUser(newToken);
          } else {
            clearStoredTokens();
          }
        }
      }
      setIsLoading(false);
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
        };

        setUser(loggedInUser);
        setToken(accessToken);

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
    setUser(null);
    setToken(null);
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
