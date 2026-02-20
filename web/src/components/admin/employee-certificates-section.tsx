"use client";

import { useEmployeeCertificates, handleDownloadCertificateAdmin } from "@/lib/api/toolbox-talks/use-certificates";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Award, Download, BookOpen, GraduationCap, AlertTriangle, RefreshCw } from "lucide-react";
import { format } from "date-fns";

interface EmployeeCertificatesSectionProps {
  employeeId: string;
}

export function EmployeeCertificatesSection({ employeeId }: EmployeeCertificatesSectionProps) {
  const { data: certificates = [], isLoading } = useEmployeeCertificates(employeeId);

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Award className="h-5 w-5" />
            Training Certificates
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="animate-pulse space-y-2">
            <div className="h-12 bg-muted rounded" />
            <div className="h-12 bg-muted rounded" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Award className="h-5 w-5" />
          Training Certificates
          {certificates.length > 0 && (
            <Badge variant="secondary">{certificates.length}</Badge>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {certificates.length === 0 ? (
          <p className="text-sm text-muted-foreground">No certificates earned yet.</p>
        ) : (
          <div className="space-y-3">
            {certificates.map((cert) => (
              <div
                key={cert.id}
                className={`flex items-center justify-between p-3 rounded-lg border ${
                  cert.isExpired ? "bg-destructive/5 border-destructive/20" : "bg-muted/50"
                }`}
              >
                <div className="flex items-center gap-3">
                  {cert.certificateType === "Course" ? (
                    <GraduationCap className="h-5 w-5 text-primary" />
                  ) : (
                    <BookOpen className="h-5 w-5 text-primary" />
                  )}
                  <div>
                    <div className="font-medium flex items-center gap-2">
                      {cert.trainingCode && (
                        <span className="text-muted-foreground font-mono text-sm">{cert.trainingCode}</span>
                      )}
                      {cert.trainingTitle}
                      {cert.isRefresher && (
                        <Badge variant="outline" className="text-orange-600 border-orange-300 text-xs">
                          <RefreshCw className="h-3 w-3 mr-1" />
                          Refresher
                        </Badge>
                      )}
                    </div>
                    <div className="text-sm text-muted-foreground">
                      Issued {format(new Date(cert.issuedAt), "MMM d, yyyy")}
                      {cert.expiresAt && (
                        <span className={cert.isExpired ? "text-destructive" : cert.isExpiringSoon ? "text-amber-600" : ""}>
                          {" · "}
                          {cert.isExpired ? "Expired" : `Valid until ${format(new Date(cert.expiresAt), "MMM d, yyyy")}`}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {cert.isExpired && (
                    <Badge variant="destructive">Expired</Badge>
                  )}
                  {cert.isExpiringSoon && !cert.isExpired && (
                    <Badge variant="outline" className="text-amber-600 border-amber-300">
                      <AlertTriangle className="h-3 w-3 mr-1" />
                      Expiring
                    </Badge>
                  )}
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => handleDownloadCertificateAdmin(cert.id, cert.certificateNumber)}
                  >
                    <Download className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
