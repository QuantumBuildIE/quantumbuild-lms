"use client";

import { useState } from "react";
import { usePermission, useAuth } from "@/lib/auth/use-auth";
import {
  useAvailableStandards,
  useSubscribedStandards,
  useSubscribeToStandard,
  useUnsubscribeFromStandard,
} from "@/lib/api/admin/use-tenant-standards";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { DeleteConfirmationDialog } from "@/components/shared/delete-confirmation-dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Plus, Loader2, X } from "lucide-react";
import { toast } from "sonner";
import type { AvailableStandardDto, TenantStandardSubscriptionDto } from "@/types/regulatory";

export default function MyStandardsPage() {
  const hasLearningsAdmin = usePermission("Learnings.Admin");
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? "";

  const [showCrossSector, setShowCrossSector] = useState(false);
  const [pendingUnsubscribe, setPendingUnsubscribe] = useState<TenantStandardSubscriptionDto | null>(null);

  const { data: subscribed, isLoading: subscribedLoading } = useSubscribedStandards(tenantId);
  const { data: available, isLoading: availableLoading } = useAvailableStandards(tenantId, showCrossSector);
  const subscribeMutation = useSubscribeToStandard(tenantId);
  const unsubscribeMutation = useUnsubscribeFromStandard(tenantId);

  const [subscribingId, setSubscribingId] = useState<string | null>(null);

  if (!hasLearningsAdmin) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to manage standards.
      </div>
    );
  }

  const unsubscribed = (available ?? []).filter((s) => !s.isSubscribed);

  const handleSubscribe = (standard: AvailableStandardDto) => {
    setSubscribingId(standard.id);
    subscribeMutation.mutate(standard.id, {
      onSuccess: () => {
        toast.success(`${standard.name} added to your standards`);
        setSubscribingId(null);
      },
      onError: () => {
        toast.error(`Failed to add ${standard.name}`);
        setSubscribingId(null);
      },
    });
  };

  const handleUnsubscribe = () => {
    if (!pendingUnsubscribe) return;
    unsubscribeMutation.mutate(pendingUnsubscribe.regulatoryBodyId, {
      onSuccess: () => {
        toast.success(`${pendingUnsubscribe.name} removed from your standards`);
        setPendingUnsubscribe(null);
      },
      onError: () => {
        toast.error(`Failed to remove ${pendingUnsubscribe.name}`);
        setPendingUnsubscribe(null);
      },
    });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Standards</h1>
        <p className="text-muted-foreground">
          Configure which industry standards your organisation adheres to. Regulations that
          apply to your sectors are handled automatically and do not require subscription.
        </p>
      </div>

      {/* Current subscriptions */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Subscribed Standards</CardTitle>
        </CardHeader>
        <CardContent>
          {subscribedLoading ? (
            <div className="space-y-2">
              {[1, 2].map((i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : !subscribed || subscribed.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              No standards subscribed yet.
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Standard</TableHead>
                  <TableHead>Sector</TableHead>
                  <TableHead>Source Body</TableHead>
                  <TableHead className="w-[120px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {subscribed.map((standard) => (
                  <TableRow key={standard.id}>
                    <TableCell>
                      <span className="font-medium">{standard.name}</span>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Badge variant="outline" className="text-xs">
                          {standard.sectorName}
                        </Badge>
                        {standard.isCrossSector && (
                          <Badge variant="secondary" className="text-xs">
                            Outside your sectors
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {standard.code}
                    </TableCell>
                    <TableCell>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => setPendingUnsubscribe(standard)}
                      >
                        <X className="mr-1 h-4 w-4" />
                        Unsubscribe
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Available standards to subscribe to */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-base">Available Standards</CardTitle>
            <div className="flex items-center gap-2">
              <Switch
                id="show-cross-sector"
                checked={showCrossSector}
                onCheckedChange={setShowCrossSector}
              />
              <Label htmlFor="show-cross-sector" className="text-sm font-normal text-muted-foreground">
                Show standards from other sectors
              </Label>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {availableLoading ? (
            <div className="space-y-2">
              {[1, 2, 3].map((i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : unsubscribed.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              {showCrossSector
                ? "No standards are available to subscribe to."
                : "No standards are available for your sectors. Try showing standards from other sectors."}
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Standard</TableHead>
                  <TableHead>Sector</TableHead>
                  <TableHead>Source Body</TableHead>
                  <TableHead className="w-[100px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {unsubscribed.map((standard) => (
                  <TableRow key={standard.id}>
                    <TableCell>
                      <span className="font-medium">{standard.name}</span>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Badge variant="outline" className="text-xs">
                          {standard.sectorName}
                        </Badge>
                        {standard.isCrossSector && (
                          <Badge variant="secondary" className="text-xs">
                            Outside your sectors
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {standard.code}
                    </TableCell>
                    <TableCell>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => handleSubscribe(standard)}
                        disabled={subscribeMutation.isPending && subscribingId === standard.id}
                      >
                        {subscribeMutation.isPending && subscribingId === standard.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <>
                            <Plus className="mr-1 h-4 w-4" />
                            Subscribe
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

      <DeleteConfirmationDialog
        open={!!pendingUnsubscribe}
        onOpenChange={(open) => !open && setPendingUnsubscribe(null)}
        title="Unsubscribe from standard?"
        description={
          pendingUnsubscribe
            ? `This will stop ${pendingUnsubscribe.name} from applying to your regulatory requirements. Existing learning content will remain but will no longer be checked against this standard.`
            : ""
        }
        onConfirm={handleUnsubscribe}
        isLoading={unsubscribeMutation.isPending}
        confirmLabel="Unsubscribe"
        confirmLoadingLabel="Unsubscribing..."
      />
    </div>
  );
}
