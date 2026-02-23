"use client";

import { useState } from "react";
import { X } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  useTenantModules,
  useAssignTenantModule,
  useRemoveTenantModule,
} from "@/lib/api/admin/use-tenant-modules";
import { MODULE_CONFIG, type ModuleName } from "@/lib/modules";
import { toast } from "sonner";

interface TenantModulesCardProps {
  tenantId: string;
}

const ALL_MODULE_NAMES = Object.keys(MODULE_CONFIG) as ModuleName[];

export function TenantModulesCard({ tenantId }: TenantModulesCardProps) {
  const { data: modules, isLoading } = useTenantModules(tenantId);
  const assignModule = useAssignTenantModule(tenantId);
  const removeModule = useRemoveTenantModule(tenantId);
  const [selectedModule, setSelectedModule] = useState<string>("");

  const assignedNames = new Set(modules?.map((m) => m.moduleName) ?? []);
  const availableModules = ALL_MODULE_NAMES.filter(
    (name) => !assignedNames.has(name)
  );

  const handleAssign = async () => {
    if (!selectedModule) return;
    try {
      await assignModule.mutateAsync({ moduleName: selectedModule });
      toast.success(`Module "${MODULE_CONFIG[selectedModule as ModuleName]?.label ?? selectedModule}" assigned`);
      setSelectedModule("");
    } catch {
      toast.error("Failed to assign module");
    }
  };

  const handleRemove = async (moduleName: string) => {
    try {
      await removeModule.mutateAsync(moduleName);
      toast.success(`Module "${MODULE_CONFIG[moduleName as ModuleName]?.label ?? moduleName}" removed`);
    } catch {
      toast.error("Failed to remove module");
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Modules</CardTitle>
        <CardDescription>
          Manage which modules are enabled for this tenant
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <div className="animate-pulse text-muted-foreground text-sm">
            Loading modules...
          </div>
        ) : (
          <>
            {/* Current modules */}
            <div className="flex flex-wrap gap-2">
              {modules && modules.length > 0 ? (
                modules.map((m) => (
                  <Badge
                    key={m.moduleName}
                    variant="secondary"
                    className="gap-1 pr-1 text-sm"
                  >
                    {MODULE_CONFIG[m.moduleName as ModuleName]?.label ??
                      m.moduleName}
                    <button
                      onClick={() => handleRemove(m.moduleName)}
                      disabled={removeModule.isPending}
                      className="ml-1 rounded-full p-0.5 hover:bg-muted-foreground/20 transition-colors"
                      aria-label={`Remove ${m.moduleName}`}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </Badge>
                ))
              ) : (
                <p className="text-sm text-muted-foreground">
                  No modules assigned
                </p>
              )}
            </div>

            {/* Add module */}
            {availableModules.length > 0 && (
              <div className="flex items-center gap-2">
                <Select
                  value={selectedModule}
                  onValueChange={setSelectedModule}
                >
                  <SelectTrigger className="w-[220px]">
                    <SelectValue placeholder="Select a module..." />
                  </SelectTrigger>
                  <SelectContent>
                    {availableModules.map((name) => (
                      <SelectItem key={name} value={name}>
                        {MODULE_CONFIG[name]?.label ?? name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button
                  onClick={handleAssign}
                  disabled={!selectedModule || assignModule.isPending}
                  size="sm"
                >
                  Add Module
                </Button>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
