import { ClipboardList, Settings, type LucideIcon } from "lucide-react";

export interface Module {
  id: string;
  name: string;
  description: string;
  icon: LucideIcon;
  path: string;
  requiredPermission: string;
}

export const availableModules: Module[] = [
  {
    id: "toolbox-talks",
    name: "Toolbox Talks",
    description: "Safety training talks and compliance tracking",
    icon: ClipboardList,
    path: "/toolbox-talks",
    requiredPermission: "ToolboxTalks.View",
  },
  {
    id: "admin",
    name: "Administration",
    description: "Manage sites, employees, companies, and users",
    icon: Settings,
    path: "/admin",
    requiredPermission: "Core.Admin",
  },
];
