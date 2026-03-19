'use client';

import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Loader2, ExternalLink, CheckCircle2 } from 'lucide-react';
import { useGenerateInspectionReport } from '@/lib/api/admin/use-requirement-mappings';
import { toast } from 'sonner';
import type { InspectionReportResultDto } from '@/types/requirement-mappings';

interface GenerateReportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sectorKey: string;
  sectorName: string;
  regulatoryBody: string;
}

export function GenerateReportDialog({
  open,
  onOpenChange,
  sectorKey,
  sectorName,
  regulatoryBody,
}: GenerateReportDialogProps) {
  const [responsiblePersonName, setResponsiblePersonName] = useState('');
  const [responsiblePersonRole, setResponsiblePersonRole] = useState('');
  const [auditPurpose, setAuditPurpose] = useState('');
  const [result, setResult] = useState<InspectionReportResultDto | null>(null);

  const generateMutation = useGenerateInspectionReport();

  const canSubmit =
    responsiblePersonName.trim().length > 0 &&
    responsiblePersonRole.trim().length > 0;

  const handleSubmit = () => {
    if (!canSubmit) return;

    generateMutation.mutate(
      {
        sectorKey,
        request: {
          responsiblePersonName: responsiblePersonName.trim(),
          responsiblePersonRole: responsiblePersonRole.trim(),
          auditPurpose: auditPurpose.trim() || undefined,
        },
      },
      {
        onSuccess: (data) => {
          setResult(data);
        },
        onError: () => {
          toast.error('Failed to generate inspection report');
        },
      }
    );
  };

  const handleClose = () => {
    setResponsiblePersonName('');
    setResponsiblePersonRole('');
    setAuditPurpose('');
    setResult(null);
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Generate Inspection Report</DialogTitle>
          <DialogDescription>
            {sectorName} — {regulatoryBody}
          </DialogDescription>
        </DialogHeader>

        {result ? (
          <div className="space-y-4 py-4">
            <div className="flex items-center gap-2 text-green-600">
              <CheckCircle2 className="h-5 w-5" />
              <span className="font-medium">Report generated successfully</span>
            </div>
            <p className="text-sm text-muted-foreground">
              Coverage: {result.coveragePercentage}% ({result.coveredCount} covered,{' '}
              {result.pendingCount} pending, {result.gapCount} gap out of{' '}
              {result.totalRequirements} requirements)
            </p>
            <Button asChild className="w-full">
              <a href={result.reportUrl} target="_blank" rel="noopener noreferrer">
                <ExternalLink className="mr-2 h-4 w-4" />
                Open Report PDF
              </a>
            </Button>
            <DialogFooter>
              <Button variant="outline" onClick={handleClose}>
                Close
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <>
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <Label htmlFor="responsiblePersonName">
                  Responsible Person Name <span className="text-red-500">*</span>
                </Label>
                <Input
                  id="responsiblePersonName"
                  value={responsiblePersonName}
                  onChange={(e) => setResponsiblePersonName(e.target.value)}
                  placeholder="e.g. Jane Smith"
                  maxLength={200}
                  disabled={generateMutation.isPending}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="responsiblePersonRole">
                  Role <span className="text-red-500">*</span>
                </Label>
                <Input
                  id="responsiblePersonRole"
                  value={responsiblePersonRole}
                  onChange={(e) => setResponsiblePersonRole(e.target.value)}
                  placeholder="e.g. Compliance Manager"
                  maxLength={200}
                  disabled={generateMutation.isPending}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="auditPurpose">Audit Purpose (optional)</Label>
                <Textarea
                  id="auditPurpose"
                  value={auditPurpose}
                  onChange={(e) => setAuditPurpose(e.target.value)}
                  placeholder="e.g. Annual regulatory inspection preparation"
                  maxLength={500}
                  rows={3}
                  disabled={generateMutation.isPending}
                />
              </div>
            </div>

            <DialogFooter>
              <Button
                variant="outline"
                onClick={handleClose}
                disabled={generateMutation.isPending}
              >
                Cancel
              </Button>
              <Button
                onClick={handleSubmit}
                disabled={!canSubmit || generateMutation.isPending}
              >
                {generateMutation.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Generating report...
                  </>
                ) : (
                  'Generate Report'
                )}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
