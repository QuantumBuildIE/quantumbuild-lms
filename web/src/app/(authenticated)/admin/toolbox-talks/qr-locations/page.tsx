"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Plus, MapPin, QrCode, Download, Pencil, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import {
  useQrLocations,
  useQrCodes,
  useCreateQrLocation,
  useUpdateQrLocation,
  useDeleteQrLocation,
  useCreateQrCode,
  useUpdateQrCode,
  useDeleteQrCode,
} from "@/lib/api/toolbox-talks/use-qr-locations";
import type { QrLocationDto, QrCodeDto, ContentMode } from "@/lib/api/toolbox-talks/qr-locations";
import { useToolboxTalks } from "@/lib/api/toolbox-talks/use-toolbox-talks";

const CONTENT_MODE_LABELS: Record<ContentMode, string> = {
  ViewOnly: "View Only",
  Training: "Training",
  Induction: "Induction",
};

const CONTENT_MODE_COLOURS: Record<ContentMode, string> = {
  ViewOnly: "bg-slate-100 text-slate-700",
  Training: "bg-blue-100 text-blue-700",
  Induction: "bg-green-100 text-green-700",
};

// ── Location form ─────────────────────────────────────────────────────────────

interface LocationFormState {
  name: string;
  description: string;
  address: string;
}

function LocationDialog({
  open,
  onOpenChange,
  initial,
  onSave,
  loading,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  initial?: LocationFormState;
  onSave: (data: LocationFormState) => void;
  loading: boolean;
}) {
  const [form, setForm] = useState<LocationFormState>(
    initial ?? { name: "", description: "", address: "" }
  );

  const set = (k: keyof LocationFormState) => (v: string) =>
    setForm((f) => ({ ...f, [k]: v }));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{initial ? "Edit Location" : "New Location"}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="loc-name">Name *</Label>
            <Input id="loc-name" value={form.name} onChange={(e) => set("name")(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="loc-desc">Description</Label>
            <Input id="loc-desc" value={form.description} onChange={(e) => set("description")(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="loc-addr">Address</Label>
            <Input id="loc-addr" value={form.address} onChange={(e) => set("address")(e.target.value)} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!form.name.trim() || loading} onClick={() => onSave(form)}>
            {loading ? "Saving…" : "Save"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── QR code form ──────────────────────────────────────────────────────────────

interface CodeFormState {
  name: string;
  toolboxTalkId: string;
  contentMode: string;
}

function QrCodeDialog({
  open,
  onOpenChange,
  onSave,
  loading,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onSave: (data: CodeFormState) => void;
  loading: boolean;
}) {
  const [form, setForm] = useState<CodeFormState>({
    name: "",
    toolboxTalkId: "",
    contentMode: "Training",
  });

  const { data: talksData } = useToolboxTalks({ pageNumber: 1, pageSize: 200 });
  const talks = talksData?.items ?? [];

  const set = <K extends keyof CodeFormState>(k: K) => (v: string) =>
    setForm((f) => ({ ...f, [k]: v }));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>New QR Code</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="code-name">Name *</Label>
            <Input id="code-name" value={form.name} onChange={(e) => set("name")(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="code-talk">Learning (optional)</Label>
            <Select value={form.toolboxTalkId} onValueChange={set("toolboxTalkId")}>
              <SelectTrigger id="code-talk">
                <SelectValue placeholder="Select a learning…" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">None</SelectItem>
                {talks.map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.code} — {t.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="code-mode">Content Mode *</Label>
            <Select value={form.contentMode} onValueChange={set("contentMode")}>
              <SelectTrigger id="code-mode">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ViewOnly">View Only</SelectItem>
                <SelectItem value="Training">Training</SelectItem>
                <SelectItem value="Induction">Induction</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!form.name.trim() || loading} onClick={() => onSave(form)}>
            {loading ? "Creating…" : "Create"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── QR code card ──────────────────────────────────────────────────────────────

function QrCodeCard({
  code,
  locationId,
}: {
  code: QrCodeDto;
  locationId: string;
}) {
  const updateCode = useUpdateQrCode(locationId);
  const deleteCode = useDeleteQrCode(locationId);

  const handleToggleActive = async () => {
    try {
      await updateCode.mutateAsync({
        codeId: code.id,
        data: {
          name: code.name,
          toolboxTalkId: code.toolboxTalkId,
          contentMode: code.contentMode,
          isActive: !code.isActive,
        },
      });
    } catch {
      toast.error("Failed to update QR code");
    }
  };

  const handleDelete = async () => {
    try {
      await deleteCode.mutateAsync(code.id);
      toast.success("QR code deleted");
    } catch {
      toast.error("Failed to delete QR code");
    }
  };

  return (
    <Card className="relative">
      <CardContent className="p-4 space-y-3">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="font-medium text-sm truncate">{code.name}</p>
            {code.talkTitle && (
              <p className="text-xs text-muted-foreground truncate mt-0.5">{code.talkTitle}</p>
            )}
          </div>
          <Badge className={cn("shrink-0 text-xs", CONTENT_MODE_COLOURS[code.contentMode])}>
            {CONTENT_MODE_LABELS[code.contentMode]}
          </Badge>
        </div>

        {code.qrImageUrl ? (
          <div className="flex items-center gap-3">
            <img
              src={code.qrImageUrl}
              alt={`QR code for ${code.name}`}
              className="w-20 h-20 border rounded"
            />
            <a
              href={code.qrImageUrl}
              download={`qr-${code.codeToken}.png`}
              className="inline-flex items-center gap-1.5 text-xs text-primary hover:underline"
            >
              <Download className="h-3.5 w-3.5" />
              Download
            </a>
          </div>
        ) : (
          <p className="text-xs text-muted-foreground italic">QR image unavailable</p>
        )}

        <div className="flex items-center justify-between pt-1">
          <div className="flex items-center gap-2">
            <Switch
              checked={code.isActive}
              onCheckedChange={handleToggleActive}
              disabled={updateCode.isPending}
            />
            <span className="text-xs text-muted-foreground">
              {code.isActive ? "Active" : "Inactive"}
            </span>
          </div>
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7 text-destructive"
            onClick={handleDelete}
            disabled={deleteCode.isPending}
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function QrLocationsPage() {
  const [selectedLocationId, setSelectedLocationId] = useState<string | null>(null);
  const [locationDialogOpen, setLocationDialogOpen] = useState(false);
  const [editingLocation, setEditingLocation] = useState<QrLocationDto | null>(null);
  const [codeDialogOpen, setCodeDialogOpen] = useState(false);
  const [search, setSearch] = useState("");

  const { data, isLoading } = useQrLocations({ search: search || undefined });
  const { data: codes, isLoading: codesLoading } = useQrCodes(selectedLocationId);

  const createLocation = useCreateQrLocation();
  const updateLocation = useUpdateQrLocation();
  const deleteLocation = useDeleteQrLocation();
  const createCode = useCreateQrCode(selectedLocationId ?? "");

  const locations = data?.items ?? [];
  const selectedLocation = locations.find((l) => l.id === selectedLocationId) ?? null;

  const handleSaveLocation = async (form: { name: string; description: string; address: string }) => {
    try {
      if (editingLocation) {
        await updateLocation.mutateAsync({
          id: editingLocation.id,
          data: { ...form, isActive: editingLocation.isActive },
        });
        toast.success("Location updated");
      } else {
        const created = await createLocation.mutateAsync(form);
        setSelectedLocationId(created.id);
        toast.success("Location created");
      }
      setLocationDialogOpen(false);
      setEditingLocation(null);
    } catch {
      toast.error("Failed to save location");
    }
  };

  const handleDeleteLocation = async (id: string) => {
    try {
      await deleteLocation.mutateAsync(id);
      if (selectedLocationId === id) setSelectedLocationId(null);
      toast.success("Location deleted");
    } catch {
      toast.error("Failed to delete location");
    }
  };

  const handleSaveCode = async (form: { name: string; toolboxTalkId: string; contentMode: string }) => {
    try {
      await createCode.mutateAsync({
        name: form.name,
        toolboxTalkId: form.toolboxTalkId || undefined,
        contentMode: form.contentMode,
      });
      setCodeDialogOpen(false);
      toast.success("QR code created");
    } catch {
      toast.error("Failed to create QR code");
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">QR Locations</h1>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
        {/* Left panel — location list */}
        <div className="lg:col-span-2 space-y-3">
          <div className="flex items-center gap-2">
            <Input
              placeholder="Search locations…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="flex-1"
            />
            <Button
              size="sm"
              onClick={() => {
                setEditingLocation(null);
                setLocationDialogOpen(true);
              }}
            >
              <Plus className="h-4 w-4 mr-1" />
              New
            </Button>
          </div>

          {isLoading ? (
            Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-16 w-full rounded-lg" />
            ))
          ) : locations.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
              <MapPin className="h-8 w-8 mb-2 opacity-40" />
              <p className="text-sm">No locations yet</p>
            </div>
          ) : (
            locations.map((loc) => (
              <button
                key={loc.id}
                onClick={() => setSelectedLocationId(loc.id)}
                className={cn(
                  "w-full text-left rounded-lg border p-3 transition-colors hover:bg-accent",
                  selectedLocationId === loc.id
                    ? "border-primary bg-accent"
                    : "border-border bg-card"
                )}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-medium text-sm truncate">{loc.name}</p>
                    {loc.address && (
                      <p className="text-xs text-muted-foreground truncate mt-0.5">{loc.address}</p>
                    )}
                  </div>
                  <div className="flex items-center gap-1 shrink-0">
                    <span className="text-xs text-muted-foreground">{loc.qrCodeCount} codes</span>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-6 w-6"
                      onClick={(e) => {
                        e.stopPropagation();
                        setEditingLocation(loc);
                        setLocationDialogOpen(true);
                      }}
                    >
                      <Pencil className="h-3 w-3" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-6 w-6 text-destructive"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDeleteLocation(loc.id);
                      }}
                    >
                      <X className="h-3 w-3" />
                    </Button>
                  </div>
                </div>
              </button>
            ))
          )}
        </div>

        {/* Right panel — selected location detail */}
        <div className="lg:col-span-3">
          {!selectedLocation ? (
            <div className="flex flex-col items-center justify-center h-64 text-muted-foreground border rounded-lg">
              <QrCode className="h-10 w-10 mb-3 opacity-30" />
              <p className="text-sm">Select a location to view its QR codes</p>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-lg font-semibold">{selectedLocation.name}</h2>
                  {selectedLocation.description && (
                    <p className="text-sm text-muted-foreground">{selectedLocation.description}</p>
                  )}
                </div>
                <Button size="sm" onClick={() => setCodeDialogOpen(true)}>
                  <Plus className="h-4 w-4 mr-1" />
                  New QR Code
                </Button>
              </div>

              {codesLoading ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <Skeleton key={i} className="h-36 w-full rounded-lg" />
                  ))}
                </div>
              ) : !codes || codes.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-12 text-muted-foreground border rounded-lg">
                  <QrCode className="h-8 w-8 mb-2 opacity-40" />
                  <p className="text-sm">No QR codes for this location yet</p>
                </div>
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {codes.map((code) => (
                    <QrCodeCard key={code.id} code={code} locationId={selectedLocation.id} />
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Location dialog */}
      <LocationDialog
        open={locationDialogOpen}
        onOpenChange={(v) => {
          setLocationDialogOpen(v);
          if (!v) setEditingLocation(null);
        }}
        initial={
          editingLocation
            ? {
                name: editingLocation.name,
                description: editingLocation.description ?? "",
                address: editingLocation.address ?? "",
              }
            : undefined
        }
        onSave={handleSaveLocation}
        loading={createLocation.isPending || updateLocation.isPending}
      />

      {/* QR code dialog */}
      {selectedLocation && (
        <QrCodeDialog
          open={codeDialogOpen}
          onOpenChange={setCodeDialogOpen}
          onSave={handleSaveCode}
          loading={createCode.isPending}
        />
      )}
    </div>
  );
}
