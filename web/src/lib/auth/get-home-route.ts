import type { User } from "@/types/auth";

/**
 * Determines the appropriate home page route based on user's roles and permissions
 */
export function getHomeRoute(user: User | null): string {
  if (!user) {
    return "/login";
  }

  const { roles, permissions } = user;

  // Admin goes to dashboard
  if (roles.includes("Admin")) {
    return "/dashboard";
  }

  // Finance goes to dashboard
  if (roles.includes("Finance")) {
    return "/dashboard";
  }

  // SiteManager goes to dashboard
  if (roles.includes("SiteManager")) {
    return "/dashboard";
  }

  // OfficeStaff goes to dashboard
  if (roles.includes("OfficeStaff")) {
    return "/dashboard";
  }

  // WarehouseStaff goes to dashboard
  if (roles.includes("WarehouseStaff")) {
    return "/dashboard";
  }

  // Fallback: check permissions directly
  if (permissions.includes("ToolboxTalks.View")) {
    return "/toolbox-talks";
  }

  // Default fallback
  return "/dashboard";
}
