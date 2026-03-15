import type { User } from "@/types/auth";

/**
 * Determines the appropriate home page route based on user's role and DPA status
 */
export function getHomeRoute(user: User | null, dpaAccepted: boolean = true): string {
  if (!user) {
    return "/login";
  }

  if (user.isSuperUser) {
    return "/admin/tenants";
  }

  if (!dpaAccepted) {
    return "/dpa-acceptance";
  }

  return "/toolbox-talks";
}
