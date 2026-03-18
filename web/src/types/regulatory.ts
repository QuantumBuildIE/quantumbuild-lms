// ============================================
// Regulatory Document & Ingestion Types
// ============================================

export interface RegulatoryDocumentListItem {
  id: string;
  regulatoryBodyName: string;
  regulatoryBodyCode: string;
  title: string;
  version: string;
  source: string | null;
  sourceUrl: string | null;
  effectiveDate: string | null;
  isActive: boolean;
  lastIngestedAt: string | null;
  sectorKeys: string[];
  draftCount: number;
  approvedCount: number;
  rejectedCount: number;
}

export interface IngestionSessionDto {
  regulatoryDocumentId: string;
  documentTitle: string;
  sourceUrl: string | null;
  status: "Idle" | "Queued" | "Processing" | "Completed" | "Failed";
  lastIngestedAt: string | null;
  draftCount: number;
  approvedCount: number;
  rejectedCount: number;
}

export interface DraftRequirementDto {
  id: string;
  title: string;
  description: string;
  section: string | null;
  sectionLabel: string | null;
  principle: string | null;
  principleLabel: string | null;
  priority: "high" | "med" | "low";
  displayOrder: number;
  ingestionSource: string;
  ingestionNotes: string | null;
  profileSectorKey: string;
  profileSectorName: string;
}

export interface RegulatoryRequirementDto extends DraftRequirementDto {
  ingestionStatus: "Draft" | "Approved" | "Rejected";
}

export interface ApproveRequirementRequest {
  title: string;
  description: string;
  section: string | null;
  sectionLabel: string | null;
  principle: string | null;
  principleLabel: string | null;
  priority: string;
  displayOrder: number;
}

export interface UpdateDraftRequirementRequest {
  title: string;
  description: string;
  section: string | null;
  sectionLabel: string | null;
  principle: string | null;
  principleLabel: string | null;
  priority: string;
  displayOrder: number;
}

export interface StartIngestionRequest {
  sourceUrl: string;
}

export interface RejectRequirementRequest {
  notes: string;
}
