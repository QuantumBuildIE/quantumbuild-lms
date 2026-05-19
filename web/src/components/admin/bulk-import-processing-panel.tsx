"use client";

import * as React from "react";
import { Loader2 } from "lucide-react";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

interface Props {
  totalRows: number;
}

export function BulkImportProcessingPanel({ totalRows }: Props) {
  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>Import in Progress</CardTitle>
        <CardDescription>
          Processing {totalRows} row{totalRows !== 1 ? "s" : ""}. This may take
          several minutes for large files — do not close this page.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex flex-col items-center gap-4 py-8">
          <Loader2 className="h-10 w-10 animate-spin text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            Creating employee records and sending invitation emails…
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
