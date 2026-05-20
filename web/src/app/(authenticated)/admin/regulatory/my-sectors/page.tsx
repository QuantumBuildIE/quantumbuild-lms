"use client";

import { useState } from "react";
import { usePermission, useAuth } from "@/lib/auth/use-auth";
import {
  useTenantSectors,
  useAvailableSectors,
  useAssignTenantSector,
} from "@/lib/api/admin/use-tenant-sectors";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { AlertTriangle, Plus, Loader2 } from "lucide-react";
import { toast } from "sonner";
import type { SectorDto } from "@/types/admin";

export default function MySectorsPage() {
  const hasLearningsAdmin = usePermission("Learnings.Admin");
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? "";

  const { data: assigned, isLoading: assignedLoading } = useTenantSectors(tenantId);
  const { data: available, isLoading: availableLoading } = useAvailableSectors();
  const assignMutation = useAssignTenantSector(tenantId);

  const [addingId, setAddingId] = useState<string | null>(null);

  if (!hasLearningsAdmin) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to manage sectors.
      </div>
    );
  }

  const assignedSectorIds = new Set(assigned?.map((s) => s.sectorId) ?? []);
  const unassigned = (available ?? []).filter((s) => !assignedSectorIds.has(s.id));

  const handleAdd = (sector: SectorDto) => {
    setAddingId(sector.id);
    assignMutation.mutate(
      { sectorId: sector.id, isDefault: false },
      {
        onSuccess: () => {
          toast.success(`${sector.name} added to your sectors`);
          setAddingId(null);
        },
        onError: () => {
          toast.error(`Failed to add ${sector.name}`);
          setAddingId(null);
        },
      }
    );
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Sectors</h1>
        <p className="text-muted-foreground">
          Regulatory sectors assigned to your organisation.
        </p>
      </div>

      <Alert className="border-amber-200 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/30">
        <AlertTriangle className="h-4 w-4 text-amber-600" />
        <AlertDescription className="text-amber-800 dark:text-amber-300 text-sm">
          Sectors can be added but not removed from this page. To remove a sector, contact support.
        </AlertDescription>
      </Alert>

      {/* Current sectors */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Current Sectors</CardTitle>
        </CardHeader>
        <CardContent>
          {assignedLoading ? (
            <div className="space-y-2">
              {[1, 2].map((i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : !assigned || assigned.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              No sectors assigned yet.
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Sector</TableHead>
                  <TableHead>Key</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {assigned.map((sector) => (
                  <TableRow key={sector.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {sector.sectorIcon && (
                          <span>{sector.sectorIcon}</span>
                        )}
                        <span className="font-medium">{sector.sectorName}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className="text-xs">
                        {sector.sectorKey}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {sector.isDefault && (
                        <Badge className="bg-primary/10 text-primary text-xs">
                          Default
                        </Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Available sectors to add */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Add a Sector</CardTitle>
        </CardHeader>
        <CardContent>
          {availableLoading ? (
            <div className="space-y-2">
              {[1, 2, 3].map((i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : unassigned.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              All available sectors are already assigned to your organisation.
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Sector</TableHead>
                  <TableHead>Key</TableHead>
                  <TableHead className="w-[100px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {unassigned.map((sector) => (
                  <TableRow key={sector.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {sector.icon && <span>{sector.icon}</span>}
                        <span className="font-medium">{sector.name}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className="text-xs">
                        {sector.key}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => handleAdd(sector)}
                        disabled={assignMutation.isPending && addingId === sector.id}
                      >
                        {assignMutation.isPending && addingId === sector.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <>
                            <Plus className="mr-1 h-4 w-4" />
                            Add
                          </>
                        )}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
