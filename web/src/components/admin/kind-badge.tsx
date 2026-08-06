import { Badge } from "@/components/ui/badge";
import type { RegulatoryBodyKind } from "@/types/regulatory";

/**
 * Regulation vs Standard badge — same visual weight, different color, so neither
 * framework type reads as more or less legitimate than the other.
 */
export function KindBadge({ kind }: { kind: RegulatoryBodyKind | string }) {
  return kind === "Standard" ? (
    <Badge variant="outline" className="border-sky-500 text-sky-600">
      Standard
    </Badge>
  ) : (
    <Badge variant="outline" className="border-slate-500 text-slate-600">
      Regulation
    </Badge>
  );
}
