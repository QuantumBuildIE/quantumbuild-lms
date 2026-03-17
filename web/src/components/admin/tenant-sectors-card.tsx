"use client";

import { useState } from "react";
import { AlertTriangle, Star, X } from "lucide-react";
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
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  useAvailableSectors,
  useTenantSectors,
  useAssignTenantSector,
  useRemoveTenantSector,
  useSetDefaultTenantSector,
} from "@/lib/api/admin/use-tenant-sectors";
import { toast } from "sonner";

interface TenantSectorsCardProps {
  tenantId: string;
}

export function TenantSectorsCard({ tenantId }: TenantSectorsCardProps) {
  const { data: availableSectors, isLoading: loadingSectors } =
    useAvailableSectors();
  const { data: tenantSectors, isLoading: loadingTenantSectors } =
    useTenantSectors(tenantId);
  const assignSector = useAssignTenantSector(tenantId);
  const removeSector = useRemoveTenantSector(tenantId);
  const setDefault = useSetDefaultTenantSector(tenantId);
  const [selectedSectorId, setSelectedSectorId] = useState<string>("");

  const isLoading = loadingSectors || loadingTenantSectors;
  const assignedSectorIds = new Set(
    tenantSectors?.map((ts) => ts.sectorId) ?? []
  );
  const unassignedSectors = (availableSectors ?? []).filter(
    (s) => !assignedSectorIds.has(s.id)
  );
  const isOnlyOneSector = (tenantSectors?.length ?? 0) === 1;

  const handleAssign = async () => {
    if (!selectedSectorId) return;
    try {
      await assignSector.mutateAsync({
        sectorId: selectedSectorId,
        isDefault: false,
      });
      const sector = availableSectors?.find((s) => s.id === selectedSectorId);
      toast.success(
        `Sector "${sector?.name ?? "Unknown"}" assigned`
      );
      setSelectedSectorId("");
    } catch {
      toast.error("Failed to assign sector");
    }
  };

  const handleRemove = async (sectorId: string, sectorName: string) => {
    try {
      await removeSector.mutateAsync(sectorId);
      toast.success(`Sector "${sectorName}" removed`);
    } catch {
      toast.error("Failed to remove sector");
    }
  };

  const handleSetDefault = async (sectorId: string, sectorName: string) => {
    try {
      await setDefault.mutateAsync(sectorId);
      toast.success(`"${sectorName}" set as default sector`);
    } catch {
      toast.error("Failed to set default sector");
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Sectors</CardTitle>
        <CardDescription>
          Manage which industry sectors apply to this tenant
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <div className="space-y-3">
            <div className="h-10 animate-pulse rounded-md bg-muted" />
            <div className="h-10 animate-pulse rounded-md bg-muted" />
          </div>
        ) : (
          <>
            {/* Assigned sectors list */}
            {tenantSectors && tenantSectors.length > 0 ? (
              <div className="space-y-2">
                {tenantSectors.map((ts) => (
                  <div
                    key={ts.id}
                    className="flex items-center justify-between rounded-md border px-3 py-2"
                  >
                    <div className="flex items-center gap-2">
                      {ts.sectorIcon && (
                        <span className="text-base">{ts.sectorIcon}</span>
                      )}
                      <span className="text-sm font-medium">
                        {ts.sectorName}
                      </span>
                      {ts.isDefault && (
                        <Badge
                          variant="default"
                          className="bg-green-600 hover:bg-green-600 text-xs"
                        >
                          Default
                        </Badge>
                      )}
                    </div>
                    <div className="flex items-center gap-1">
                      {!ts.isDefault && !isOnlyOneSector && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() =>
                            handleSetDefault(ts.sectorId, ts.sectorName)
                          }
                          disabled={setDefault.isPending}
                          className="text-xs"
                        >
                          <Star className="mr-1 h-3 w-3" />
                          Set Default
                        </Button>
                      )}
                      <TooltipProvider>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <span>
                              <button
                                onClick={() =>
                                  handleRemove(ts.sectorId, ts.sectorName)
                                }
                                disabled={
                                  isOnlyOneSector || removeSector.isPending
                                }
                                className="rounded-full p-1 hover:bg-muted-foreground/20 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                aria-label={`Remove ${ts.sectorName}`}
                              >
                                <X className="h-3.5 w-3.5" />
                              </button>
                            </span>
                          </TooltipTrigger>
                          {isOnlyOneSector && (
                            <TooltipContent>
                              At least one sector is required
                            </TooltipContent>
                          )}
                        </Tooltip>
                      </TooltipProvider>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-3 dark:border-amber-700 dark:bg-amber-950/30">
                <AlertTriangle className="mt-0.5 h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
                <p className="text-sm text-amber-800 dark:text-amber-300">
                  No sectors configured. At least one sector is required for
                  regulatory scoring and glossary management.
                </p>
              </div>
            )}

            {/* Add sector */}
            {unassignedSectors.length > 0 && (
              <div className="flex items-center gap-2">
                <Select
                  value={selectedSectorId}
                  onValueChange={setSelectedSectorId}
                >
                  <SelectTrigger className="w-[220px]">
                    <SelectValue placeholder="Select a sector..." />
                  </SelectTrigger>
                  <SelectContent>
                    {unassignedSectors.map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.icon ? `${s.icon} ` : ""}
                        {s.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button
                  onClick={handleAssign}
                  disabled={!selectedSectorId || assignSector.isPending}
                  size="sm"
                >
                  Add Sector
                </Button>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
