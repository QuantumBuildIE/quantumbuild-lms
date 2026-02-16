import type { User } from "@/types/auth";

/**
 * Determines the appropriate home page route based on user's roles and permissions
 */
export function getHomeRoute(user: User | null): string {
  if (!user) {
    return "/login";
  }

  return "/toolbox-talks";
}
