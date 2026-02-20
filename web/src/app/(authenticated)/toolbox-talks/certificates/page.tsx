"use client";

import { useState } from "react";
import Link from "next/link";
import { format } from "date-fns";
import {
  Download,
  Award,
  AlertTriangle,
  RefreshCw,
  BookOpen,
  GraduationCap,
} from "lucide-react";
import { toast } from "sonner";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  useMyCertificates,
  handleDownloadCertificate,
} from "@/lib/api/toolbox-talks/use-certificates";

export default function CertificatesPage() {
  const { data: certificates = [], isLoading } = useMyCertificates();
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const handleDownload = async (id: string, certificateNumber: string) => {
    setDownloadingId(id);
    try {
      await handleDownloadCertificate(id, certificateNumber);
    } catch {
      toast.error("Failed to download certificate");
    } finally {
      setDownloadingId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="animate-pulse space-y-4">
          <div className="h-8 w-48 bg-muted rounded" />
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-48 bg-muted rounded-lg" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Award className="h-8 w-8 text-primary" />
          <div>
            <h1 className="text-2xl font-bold">My Certificates</h1>
            <p className="text-muted-foreground">
              Your training completion certificates
            </p>
          </div>
        </div>
        <Link href="/toolbox-talks">
          <Button variant="outline">Back to My Learnings</Button>
        </Link>
      </div>

      {/* Certificates Grid */}
      {certificates.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <Award className="h-12 w-12 text-muted-foreground mb-4" />
            <h3 className="text-lg font-medium">No certificates yet</h3>
            <p className="text-muted-foreground text-center mt-1">
              Complete your assigned training to earn certificates
            </p>
            <Link href="/toolbox-talks" className="mt-4">
              <Button>View My Training</Button>
            </Link>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {certificates.map((cert) => (
            <Card
              key={cert.id}
              className={
                cert.isExpired ? "opacity-60 border-destructive/50" : ""
              }
            >
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-2">
                    {cert.certificateType === "Course" ? (
                      <GraduationCap className="h-5 w-5 text-primary" />
                    ) : (
                      <BookOpen className="h-5 w-5 text-primary" />
                    )}
                    <Badge variant="outline">{cert.certificateType}</Badge>
                  </div>
                  <div className="flex gap-1">
                    {cert.isRefresher && (
                      <Badge
                        variant="outline"
                        className="text-orange-600 border-orange-300"
                      >
                        <RefreshCw className="h-3 w-3 mr-1" />
                        Refresher
                      </Badge>
                    )}
                    {cert.isExpired && (
                      <Badge variant="destructive">Expired</Badge>
                    )}
                    {cert.isExpiringSoon && !cert.isExpired && (
                      <Badge
                        variant="outline"
                        className="text-amber-600 border-amber-300"
                      >
                        <AlertTriangle className="h-3 w-3 mr-1" />
                        Expiring Soon
                      </Badge>
                    )}
                  </div>
                </div>
                <CardTitle className="text-lg mt-2">
                  {cert.trainingCode && (
                    <span className="text-muted-foreground font-mono text-sm mr-1.5">{cert.trainingCode}</span>
                  )}
                  {cert.trainingTitle}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {/* Included talks for courses */}
                {cert.includedTalks && cert.includedTalks.length > 0 && (
                  <div className="text-sm text-muted-foreground">
                    <p className="font-medium mb-1">Includes:</p>
                    <ul className="list-disc list-inside space-y-0.5">
                      {cert.includedTalks.slice(0, 3).map((talk, i) => (
                        <li key={i} className="truncate">
                          {talk}
                        </li>
                      ))}
                      {cert.includedTalks.length > 3 && (
                        <li>+{cert.includedTalks.length - 3} more</li>
                      )}
                    </ul>
                  </div>
                )}

                {/* Dates */}
                <div className="text-sm space-y-1">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Issued:</span>
                    <span>
                      {format(new Date(cert.issuedAt), "MMM d, yyyy")}
                    </span>
                  </div>
                  {cert.expiresAt && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">
                        Valid until:
                      </span>
                      <span
                        className={
                          cert.isExpired
                            ? "text-destructive"
                            : cert.isExpiringSoon
                              ? "text-amber-600"
                              : ""
                        }
                      >
                        {format(new Date(cert.expiresAt), "MMM d, yyyy")}
                      </span>
                    </div>
                  )}
                </div>

                {/* Certificate number */}
                <div className="text-xs text-muted-foreground pt-2 border-t">
                  {cert.certificateNumber}
                </div>

                {/* Download button */}
                <Button
                  className="w-full"
                  variant={cert.isExpired ? "outline" : "default"}
                  onClick={() =>
                    handleDownload(cert.id, cert.certificateNumber)
                  }
                  disabled={downloadingId === cert.id}
                >
                  <Download className="h-4 w-4 mr-2" />
                  {downloadingId === cert.id
                    ? "Downloading..."
                    : "Download Certificate"}
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
