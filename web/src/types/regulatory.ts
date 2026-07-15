// ============================================
// Tenant-admin browse types
// ============================================

export interface RegulatoryBrowseRequirement {
  id: string;
  title: string;
  description: string;
  priority: "high" | "med" | "low";
  section: string | null;
  sectionLabel: string | null;
  sectorKey: string;
  sectorName: string;
}

export interface RegulatoryBrowsePrincipleGroup {
  principle: string | null;
  principleLabel: string | null;
  requirements: RegulatoryBrowseRequirement[];
}

export interface RegulatoryBrowseDocument {
  id: string;
  title: string;
  version: string | null;
  sectorKeys: string[];
  principleGroups: RegulatoryBrowsePrincipleGroup[];
}

export interface RegulatoryBrowseBody {
  id: string;
  name: string;
  code: string;
  country: string | null;
  documents: RegulatoryBrowseDocument[];
}

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
  status: "Idle" | "Ingesting" | "Success" | "Failed";
  lastIngestedAt: string | null;
  lastIngestionErrorMessage: string | null;
  lastIngestionErrorCode: string | null;
  draftCount: number;
  approvedCount: number;
  rejectedCount: number;
}

export interface RegulatoryDocumentUploadResponse {
  sourceUrl: string;
  fileName: string;
  fileSizeBytes: number;
}

export interface RegulatoryBody {
  id: string;
  name: string;
  code: string;
  country: string;
}

export interface CreateRegulatoryDocumentRequest {
  regulatoryBodyId: string;
  title: string;
  version: string;
  sourceUrl?: string;
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
