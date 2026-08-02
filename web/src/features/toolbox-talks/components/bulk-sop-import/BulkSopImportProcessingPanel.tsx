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
  totalPdfCount: number;
}

export function BulkSopImportProcessingPanel({ totalPdfCount }: Props) {
  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>Import in Progress</CardTitle>
        <CardDescription>
          Processing {totalPdfCount} PDF{totalPdfCount !== 1 ? "s" : ""}. This may take several
          minutes — do not close this page.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex flex-col items-center gap-4 py-8">
          <Loader2 className="h-10 w-10 animate-spin text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            Parsing PDFs and generating quizzes for each learning…
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
