import type { User } from "@/types/auth";
import { MODULE_CONFIG, type ModuleName } from "@/lib/modules";

/**
 * Determines the appropriate home page route based on user's roles, permissions, and enabled modules
 */
export function getHomeRoute(user: User | null): string {
  if (!user) {
    return "/login";
  }

  if (user.isSuperUser) {
    return "/admin/tenants";
  }

  const modules = user.enabledModules ?? [];

  // Single module → go directly to it
  if (modules.length === 1) {
    const config = MODULE_CONFIG[modules[0] as ModuleName];
    if (config) return config.href;
  }

  // Multiple or zero modules → show dashboard selector
  return "/dashboard";
}
