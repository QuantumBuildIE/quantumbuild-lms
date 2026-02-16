"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { FileSpreadsheet, FileText } from "lucide-react";
import { toast } from "sonner";
import { apiClient } from "@/lib/api/client";

interface ExportButtonsProps {
  exportUrl: string;
  filters?: Record<string, string | number | boolean | null | undefined>;
  className?: string;
}

export function ExportButtons({ exportUrl, filters = {}, className = "" }: ExportButtonsProps) {
  const [isExportingExcel, setIsExportingExcel] = useState(false);
  const [isExportingPdf, setIsExportingPdf] = useState(false);

  const handleExport = async (format: "excel" | "pdf") => {
    const setLoading = format === "excel" ? setIsExportingExcel : setIsExportingPdf;

    try {
      setLoading(true);

      // Build query parameters
      const params = new URLSearchParams();
      params.append("format", format);

      // Add filters to query params
      Object.entries(filters).forEach(([key, value]) => {
        if (value !== null && value !== undefined && value !== "") {
          params.append(key, String(value));
        }
      });

      // Fetch the file
      const response = await apiClient.get(`${exportUrl}?${params.toString()}`, {
        responseType: "blob",
      });

      // Extract filename from content-disposition header if available
      const contentDisposition = response.headers["content-disposition"];
      let filename = `export_${new Date().toISOString().split("T")[0]}.${format === "excel" ? "xlsx" : "pdf"}`;

      if (contentDisposition) {
        const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
        if (filenameMatch && filenameMatch[1]) {
          filename = filenameMatch[1].replace(/['"]/g, "");
        }
      }

      // Create blob and download
      const blob = new Blob([response.data], {
        type:
          format === "excel"
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/pdf",
      });

      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      toast.success(`${format === "excel" ? "Excel" : "PDF"} file downloaded successfully`);
    } catch (error: any) {
      console.error(`Export ${format} error:`, error);
      toast.error(`Failed to export ${format === "excel" ? "Excel" : "PDF"} file`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={`flex gap-2 ${className}`}>
      <Button
        variant="outline"
        size="sm"
        onClick={() => handleExport("excel")}
        disabled={isExportingExcel || isExportingPdf}
      >
        <FileSpreadsheet className="h-4 w-4 mr-2" />
        {isExportingExcel ? "Exporting..." : "Export Excel"}
      </Button>

      <Button
        variant="outline"
        size="sm"
        onClick={() => handleExport("pdf")}
        disabled={isExportingExcel || isExportingPdf}
      >
        <FileText className="h-4 w-4 mr-2" />
        {isExportingPdf ? "Exporting..." : "Export PDF"}
      </Button>
    </div>
  );
}
