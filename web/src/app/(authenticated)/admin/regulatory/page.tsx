"use client";

import { useState } from "react";
import Link from "next/link";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import { useRegulatoryDocuments } from "@/lib/api/admin/use-regulatory-ingestion";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { FileText, ExternalLink } from "lucide-react";
import type { RegulatoryDocumentListItem } from "@/types/regulatory";

function formatDate(dateStr: string | null): string {
  if (!dateStr) return "—";
  return new Date(dateStr).toLocaleDateString("en-IE", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function StatusBadge({ doc }: { doc: RegulatoryDocumentListItem }) {
  if (doc.draftCount > 0) {
    return (
      <Badge variant="outline" className="border-amber-500 text-amber-600">
        {doc.draftCount} draft{doc.draftCount !== 1 ? "s" : ""} pending
      </Badge>
    );
  }
  if (doc.approvedCount > 0) {
    return (
      <Badge variant="outline" className="border-green-500 text-green-600">
        {doc.approvedCount} approved
      </Badge>
    );
  }
  return (
    <span className="text-sm text-muted-foreground">No requirements</span>
  );
}

export default function RegulatoryDocumentsPage() {
  const isSuperUser = useIsSuperUser();
  const { data: documents, isLoading } = useRegulatoryDocuments();

  if (!isSuperUser) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to access Regulatory Documents.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold tracking-tight">
          Regulatory Documents
        </h2>
        <p className="text-sm text-muted-foreground">
          Manage regulatory documents and AI-powered requirement ingestion
        </p>
      </div>

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Regulatory Body</TableHead>
              <TableHead>Document Title</TableHead>
              <TableHead>Version</TableHead>
              <TableHead>Sectors</TableHead>
              <TableHead>Last Ingested</TableHead>
              <TableHead>Requirements</TableHead>
              <TableHead className="w-[100px]">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: 3 }).map((_, i) => (
                <TableRow key={i}>
                  {Array.from({ length: 7 }).map((_, j) => (
                    <TableCell key={j}>
                      <Skeleton className="h-4 w-full" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : documents && documents.length > 0 ? (
              documents.map((doc) => (
                <TableRow key={doc.id}>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Badge variant="secondary">{doc.regulatoryBodyCode}</Badge>
                      <span className="text-sm">{doc.regulatoryBodyName}</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <FileText className="h-4 w-4 text-muted-foreground" />
                      <span className="font-medium">{doc.title}</span>
                    </div>
                  </TableCell>
                  <TableCell className="text-sm">{doc.version}</TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      {doc.sectorKeys.map((key) => (
                        <Badge key={key} variant="outline" className="text-xs">
                          {key}
                        </Badge>
                      ))}
                    </div>
                  </TableCell>
                  <TableCell className="text-sm">
                    {formatDate(doc.lastIngestedAt)}
                  </TableCell>
                  <TableCell>
                    <StatusBadge doc={doc} />
                  </TableCell>
                  <TableCell>
                    <Button variant="outline" size="sm" asChild>
                      <Link href={`/admin/regulatory/${doc.id}`}>Manage</Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={7}
                  className="text-center text-muted-foreground py-8"
                >
                  No regulatory documents found.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
