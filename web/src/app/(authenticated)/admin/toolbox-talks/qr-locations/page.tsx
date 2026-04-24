"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Plus, MapPin, QrCode, Download, Pencil, Trash2, X, Activity } from "lucide-react";
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
import { format } from "date-fns";
import {
  useQrLocations,
  useQrCodes,
  useCreateQrLocation,
  useUpdateQrLocation,
  useDeleteQrLocation,
  useCreateQrCode,
  useUpdateQrCode,
  useDeleteQrCode,
  useQrSessions,
  useQrSessionsSummary,
} from "@/lib/api/toolbox-talks/use-qr-locations";
import type {
  QrLocationDto,
  QrCodeDto,
  ContentMode,
  QrSessionStatus,
  QrSessionsParams,
} from "@/lib/api/toolbox-talks/qr-locations";
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

// ── Session status helpers ────────────────────────────────────────────────────

const STATUS_LABELS: Record<QrSessionStatus, string> = {
  Active: "Active",
  Completed: "Completed",
  Abandoned: "Abandoned",
};

const STATUS_COLOURS: Record<QrSessionStatus, string> = {
  Active: "bg-blue-100 text-blue-700",
  Completed: "bg-green-100 text-green-700",
  Abandoned: "bg-slate-100 text-slate-600",
};

const LANGUAGE_NAMES: Record<string, string> = {
  en: "English", es: "Spanish", fr: "French", pl: "Polish",
  ro: "Romanian", uk: "Ukrainian", pt: "Portuguese",
  lt: "Lithuanian", de: "German", lv: "Latvian",
};

// ── Sessions panel ────────────────────────────────────────────────────────────

function SessionsPanel({ locations }: { locations: QrLocationDto[] }) {
  const [filters, setFilters] = useState<QrSessionsParams>({ page: 1, pageSize: 15 });
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  const { data: summary } = useQrSessionsSummary();
  const { data: sessions, isLoading } = useQrSessions(filters);

  const applyDates = () => {
    setFilters((f) => ({
      ...f,
      page: 1,
      from: fromDate || undefined,
      to: toDate || undefined,
    }));
  };

  const setFilter = <K extends keyof QrSessionsParams>(key: K, value: QrSessionsParams[K]) => {
    setFilters((f) => ({ ...f, page: 1, [key]: value || undefined }));
  };

  const items = sessions?.items ?? [];
  const total = sessions?.totalCount ?? 0;
  const totalPages = sessions?.totalPages ?? 1;
  const currentPage = filters.page ?? 1;

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold flex items-center gap-2">
        <Activity className="h-5 w-5" />
        QR Sessions
      </h2>

      {/* Summary cards */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        {[
          { label: "Total", value: summary?.totalSessions ?? 0 },
          { label: "Completed", value: summary?.completedSessions ?? 0 },
          { label: "Abandoned", value: summary?.abandonedSessions ?? 0 },
          { label: "Active", value: summary?.activeSessions ?? 0 },
          {
            label: "Avg Score",
            value: summary?.averageScore != null
              ? `${Math.round(summary.averageScore)}%`
              : "—",
          },
        ].map((card) => (
          <Card key={card.label}>
            <CardContent className="p-4">
              <p className="text-xs text-muted-foreground">{card.label}</p>
              <p className="text-2xl font-bold mt-1">{card.value}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-2">
        <div className="space-y-1">
          <Label className="text-xs">Status</Label>
          <Select
            value={filters.status ?? ""}
            onValueChange={(v) => setFilter("status", (v || undefined) as QrSessionStatus | undefined)}
          >
            <SelectTrigger className="h-8 w-32 text-xs">
              <SelectValue placeholder="All" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">All</SelectItem>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Abandoned">Abandoned</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1">
          <Label className="text-xs">Location</Label>
          <Select
            value={filters.qrCodeId ?? ""}
            onValueChange={() => {}}
          >
            <SelectTrigger className="h-8 w-40 text-xs">
              <SelectValue placeholder="All locations" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">All locations</SelectItem>
              {locations.map((loc) => (
                <SelectItem key={loc.id} value={loc.id}>{loc.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1">
          <Label className="text-xs">From</Label>
          <Input
            type="date"
            className="h-8 text-xs w-36"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">To</Label>
          <Input
            type="date"
            className="h-8 text-xs w-36"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </div>
        <Button size="sm" variant="outline" className="h-8 text-xs" onClick={applyDates}>
          Apply
        </Button>
        <Button
          size="sm"
          variant="ghost"
          className="h-8 text-xs"
          onClick={() => {
            setFromDate("");
            setToDate("");
            setFilters({ page: 1, pageSize: 15 });
          }}
        >
          Clear
        </Button>
      </div>

      {/* Table */}
      <div className="border rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                {["Employee", "Location", "Talk", "Mode", "Language", "Status", "Score", "Started", "Completed"].map(
                  (h) => (
                    <th key={h} className="px-3 py-2 text-left text-xs font-medium text-muted-foreground whitespace-nowrap">
                      {h}
                    </th>
                  )
                )}
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 9 }).map((__, j) => (
                      <td key={j} className="px-3 py-2">
                        <Skeleton className="h-4 w-full" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={9} className="px-3 py-8 text-center text-muted-foreground text-sm">
                    No sessions found
                  </td>
                </tr>
              ) : (
                items.map((s) => (
                  <tr key={s.id} className="border-t hover:bg-muted/30 transition-colors">
                    <td className="px-3 py-2 font-medium whitespace-nowrap">{s.employeeName}</td>
                    <td className="px-3 py-2 text-muted-foreground whitespace-nowrap">{s.locationName}</td>
                    <td className="px-3 py-2 max-w-[180px] truncate" title={s.talkTitle ?? "—"}>
                      {s.talkTitle ?? <span className="text-muted-foreground">—</span>}
                    </td>
                    <td className="px-3 py-2">
                      <Badge className={cn("text-xs", CONTENT_MODE_COLOURS[s.contentMode])}>
                        {CONTENT_MODE_LABELS[s.contentMode]}
                      </Badge>
                    </td>
                    <td className="px-3 py-2 text-muted-foreground">
                      {LANGUAGE_NAMES[s.language] ?? s.language}
                    </td>
                    <td className="px-3 py-2">
                      <Badge className={cn("text-xs", STATUS_COLOURS[s.status])}>
                        {STATUS_LABELS[s.status]}
                      </Badge>
                    </td>
                    <td className="px-3 py-2 text-center">
                      {s.score != null ? `${s.score}%` : <span className="text-muted-foreground">—</span>}
                    </td>
                    <td className="px-3 py-2 text-muted-foreground whitespace-nowrap text-xs">
                      {format(new Date(s.startedAt), "dd MMM yyyy HH:mm")}
                    </td>
                    <td className="px-3 py-2 text-muted-foreground whitespace-nowrap text-xs">
                      {s.completedAt
                        ? format(new Date(s.completedAt), "dd MMM yyyy HH:mm")
                        : <span>—</span>}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Pagination */}
      {total > 0 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>{total} session{total !== 1 ? "s" : ""}</span>
          <div className="flex items-center gap-2">
            <Button
              size="sm"
              variant="outline"
              className="h-7 px-2 text-xs"
              disabled={currentPage <= 1}
              onClick={() => setFilters((f) => ({ ...f, page: (f.page ?? 1) - 1 }))}
            >
              Previous
            </Button>
            <span className="text-xs">
              {currentPage} / {totalPages}
            </span>
            <Button
              size="sm"
              variant="outline"
              className="h-7 px-2 text-xs"
              disabled={currentPage >= totalPages}
              onClick={() => setFilters((f) => ({ ...f, page: (f.page ?? 1) + 1 }))}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
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

      {/* Sessions panel */}
      <div className="border-t pt-6">
        <SessionsPanel locations={locations} />
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
