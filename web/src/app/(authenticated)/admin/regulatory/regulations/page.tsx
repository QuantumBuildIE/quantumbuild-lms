"use client";

import { useState } from "react";
import { usePermission } from "@/lib/auth/use-auth";
import { useBrowsableRequirements } from "@/lib/api/admin/use-regulatory-ingestion";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ShieldCheck } from "lucide-react";
import type { RegulatoryBrowseBody, RegulatoryBrowseRequirement } from "@/types/regulatory";

function PriorityBadge({ priority }: { priority: string }) {
  const variant =
    priority === "high" ? "destructive" : priority === "low" ? "secondary" : "outline";
  return <Badge variant={variant} className="text-xs">{priority}</Badge>;
}

function RequirementCard({ req }: { req: RegulatoryBrowseRequirement }) {
  return (
    <div className="rounded-md border bg-card p-4 space-y-1">
      <div className="flex items-start gap-2 flex-wrap">
        <span className="font-medium text-sm">{req.title}</span>
        <PriorityBadge priority={req.priority} />
        <Badge variant="outline" className="text-xs">{req.sectorName}</Badge>
        {req.section && (
          <Badge variant="secondary" className="text-xs">
            {req.section}{req.sectionLabel ? ` — ${req.sectionLabel}` : ""}
          </Badge>
        )}
      </div>
      <p className="text-sm text-muted-foreground">{req.description}</p>
    </div>
  );
}

function DocumentSection({ doc }: { doc: { id: string; title: string; version: string | null; sectorKeys: string[]; principleGroups: { principle: string | null; principleLabel: string | null; requirements: RegulatoryBrowseRequirement[] }[] } }) {
  const totalRequirements = doc.principleGroups.reduce(
    (sum, g) => sum + g.requirements.length,
    0
  );

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3 flex-wrap">
        <h3 className="font-semibold">{doc.title}</h3>
        {doc.version && (
          <Badge variant="secondary" className="text-xs">{doc.version}</Badge>
        )}
        {doc.sectorKeys.map((k) => (
          <Badge key={k} variant="outline" className="text-xs">{k}</Badge>
        ))}
        <span className="text-xs text-muted-foreground ml-auto">
          {totalRequirements} requirement{totalRequirements !== 1 ? "s" : ""}
        </span>
      </div>

      <Accordion type="multiple" className="space-y-1">
        {doc.principleGroups.map((group, i) => {
          const key = group.principle ?? `__uncategorised_${i}`;
          const label = group.principle
            ? `${group.principle}${group.principleLabel ? ` — ${group.principleLabel}` : ""}`
            : "Uncategorised";
          return (
            <AccordionItem key={key} value={key} className="border rounded-lg px-1">
              <AccordionTrigger className="hover:no-underline px-3 py-2">
                <div className="flex items-center gap-2">
                  <span className="font-medium text-sm">{label}</span>
                  <span className="text-xs text-muted-foreground">
                    ({group.requirements.length})
                  </span>
                </div>
              </AccordionTrigger>
              <AccordionContent className="px-2 pb-3">
                <div className="space-y-2">
                  {group.requirements.map((req) => (
                    <RequirementCard key={req.id} req={req} />
                  ))}
                </div>
              </AccordionContent>
            </AccordionItem>
          );
        })}
      </Accordion>
    </div>
  );
}

export default function RegulationsPage() {
  const hasLearningsAdmin = usePermission("Learnings.Admin");
  const { data: bodies, isLoading } = useBrowsableRequirements();

  if (!hasLearningsAdmin) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to view regulations.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Regulations</h1>
        <p className="text-muted-foreground">
          Approved regulatory requirements applicable to your organisation&apos;s sectors.
        </p>
      </div>

      {isLoading ? (
        <div className="space-y-4">
          {[1, 2].map((i) => (
            <Card key={i}>
              <CardHeader>
                <Skeleton className="h-6 w-48" />
              </CardHeader>
              <CardContent className="space-y-3">
                {[1, 2, 3].map((j) => (
                  <Skeleton key={j} className="h-20 w-full" />
                ))}
              </CardContent>
            </Card>
          ))}
        </div>
      ) : !bodies || bodies.length === 0 ? (
        <Card className="p-8 text-center">
          <div className="flex flex-col items-center gap-2">
            <ShieldCheck className="h-8 w-8 text-muted-foreground" />
            <p className="text-muted-foreground">
              No regulatory requirements found for your sectors. Your administrator may need to ingest requirements from the System Administration page.
            </p>
          </div>
        </Card>
      ) : (
        <div className="space-y-8">
          {bodies.map((body) => (
            <Card key={body.id}>
              <CardHeader>
                <div className="flex items-center gap-3">
                  <Badge variant="secondary">{body.code}</Badge>
                  <CardTitle className="text-lg">{body.name}</CardTitle>
                  {body.country && (
                    <span className="text-sm text-muted-foreground">{body.country}</span>
                  )}
                </div>
              </CardHeader>
              <CardContent className="space-y-6">
                {body.documents.map((doc) => (
                  <DocumentSection key={doc.id} doc={doc} />
                ))}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
