import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5222/api";

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

// Storage helper to check both localStorage and sessionStorage
export function getStoredToken(key: string): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(key) || sessionStorage.getItem(key);
}

export function setStoredToken(key: string, value: string, rememberMe: boolean): void {
  if (typeof window === "undefined") return;
  if (rememberMe) {
    localStorage.setItem(key, value);
  } else {
    sessionStorage.setItem(key, value);
  }
}

export function clearStoredTokens(): void {
  if (typeof window === "undefined") return;
  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
  localStorage.removeItem("rememberMe");
  sessionStorage.removeItem("accessToken");
  sessionStorage.removeItem("refreshToken");
}

export function getRememberMe(): boolean {
  if (typeof window === "undefined") return false;
  return localStorage.getItem("rememberMe") === "true";
}

export function setRememberMe(value: boolean): void {
  if (typeof window === "undefined") return;
  if (value) {
    localStorage.setItem("rememberMe", "true");
  } else {
    localStorage.removeItem("rememberMe");
  }
}

function getLoginRedirectUrl(): string {
  const currentPath = window.location.pathname;
  const authPaths = ["/login", "/register", "/forgot-password"];
  const isAuthPage = authPaths.some((path) => currentPath.startsWith(path));

  if (isAuthPage) {
    return "/login";
  }

  sessionStorage.setItem("returnUrlFresh", "true");
  return `/login?returnUrl=${encodeURIComponent(currentPath)}`;
}

// Track if we're currently refreshing the token to prevent multiple refresh attempts
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value: unknown) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: Error | null, token: string | null = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });
  failedQueue = [];
};

// Request interceptor to add Bearer token and tenant context
apiClient.interceptors.request.use(
  (config) => {
    const token = getStoredToken("accessToken");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    // Add X-Tenant-Id header for super user tenant context switching
    if (typeof window !== "undefined") {
      const activeTenantId = localStorage.getItem("activeTenantId");
      if (activeTenantId) {
        config.headers["X-Tenant-Id"] = activeTenantId;
      }
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor for 401 handling with automatic token refresh
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // If we get a 401 and haven't already retried
    if (error.response?.status === 401 && !originalRequest._retry) {
      // Don't try to refresh if this is already a refresh token request
      if (originalRequest.url?.includes("/auth/refresh-token")) {
        clearStoredTokens();
        window.location.href = getLoginRedirectUrl();
        return Promise.reject(error);
      }

      if (isRefreshing) {
        // If we're already refreshing, queue this request
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`;
            return apiClient(originalRequest);
          })
          .catch((err) => {
            return Promise.reject(err);
          });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const refreshToken = getStoredToken("refreshToken");
      const accessToken = getStoredToken("accessToken");

      if (!refreshToken || !accessToken) {
        clearStoredTokens();
        window.location.href = getLoginRedirectUrl();
        return Promise.reject(error);
      }

      try {
        const response = await axios.post(`${API_BASE_URL}/auth/refresh-token`, {
          accessToken,
          refreshToken,
        });

        if (response.data.success) {
          const { accessToken: newAccessToken, refreshToken: newRefreshToken } = response.data;
          const rememberMe = getRememberMe();

          setStoredToken("accessToken", newAccessToken, rememberMe);
          setStoredToken("refreshToken", newRefreshToken, rememberMe);

          originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
          processQueue(null, newAccessToken);

          return apiClient(originalRequest);
        } else {
          processQueue(new Error("Refresh failed"), null);
          clearStoredTokens();
          window.location.href = getLoginRedirectUrl();
          return Promise.reject(error);
        }
      } catch (refreshError) {
        processQueue(refreshError as Error, null);
        clearStoredTokens();
        window.location.href = getLoginRedirectUrl();
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);
