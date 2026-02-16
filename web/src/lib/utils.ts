import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * Extracts an error message from an API error response.
 * Handles the API response format: { success: false, errors: string[], message: string | null }
 */
export function getApiErrorMessage(error: unknown, fallback = "An unexpected error occurred"): string {
  // Check for Axios error response structure
  if (error && typeof error === "object" && "response" in error) {
    const axiosError = error as { response?: { data?: { errors?: string[]; message?: string } } };
    const apiErrors = axiosError.response?.data?.errors;
    const apiMessage = axiosError.response?.data?.message;

    if (apiErrors && apiErrors.length > 0) {
      return apiErrors.join(". ");
    }
    if (apiMessage) {
      return apiMessage;
    }
  }

  // Fall back to Error.message if available
  if (error instanceof Error) {
    return error.message;
  }

  return fallback;
}
