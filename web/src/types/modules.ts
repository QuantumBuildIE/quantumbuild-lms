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
    name: "Learnings",
    description: "Safety training and compliance tracking",
    icon: ClipboardList,
    path: "/toolbox-talks",
    requiredPermission: "Learnings.View",
  },
  {
    id: "admin",
    name: "Administration",
    description: "Manage sites, employees, companies, and users",
    icon: Settings,
    path: "/admin",
    requiredPermission: "Core.ManageEmployees",
  },
];
