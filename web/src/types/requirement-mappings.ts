export interface PendingMappingDto {
  id: string;
  regulatoryRequirementId: string;
  requirementTitle: string;
  requirementDescription: string;
  requirementSection: string | null;
  requirementSectionLabel: string | null;
  requirementPrinciple: string | null;
  requirementPrincipleLabel: string | null;
  requirementPriority: "high" | "med" | "low";
  confidenceScore: number | null;
  aiReasoning: string | null;
  reviewNotes: string | null;
  mappingStatus: string;
  contentTitle: string;
  contentType: "Talk" | "Course";
  contentId: string;
  createdAt: string;
}

export interface MappingSummaryDto {
  totalSuggested: number;
  totalConfirmed: number;
  totalRejected: number;
  pendingReview: PendingMappingDto[];
}

export interface RejectMappingRequest {
  mappingId: string;
  notes: string | null;
}

export interface UnconfirmedCountDto {
  count: number;
}

// ============================================
// Compliance Checklist Types
// ============================================

export interface MappingDetailDto {
  mappingId: string;
  contentId: string;
  contentTitle: string;
  contentType: "Talk" | "Course";
  mappingStatus: string;
  confidenceScore: number | null;
  validationScore: number | null;
  validationOutcome: string | null;
  validationDate: string | null;
}

export interface ComplianceRequirementDto {
  id: string;
  title: string;
  description: string;
  section: string | null;
  sectionLabel: string | null;
  principle: string | null;
  principleLabel: string | null;
  priority: "high" | "med" | "low";
  displayOrder: number;
  coverageStatus: "Covered" | "Pending" | "Gap";
  mappings: MappingDetailDto[];
}

export interface CompliancePrincipleGroupDto {
  principle: string;
  principleLabel: string;
  totalRequirements: number;
  coveredCount: number;
  pendingCount: number;
  gapCount: number;
  requirements: ComplianceRequirementDto[];
}

export interface ComplianceChecklistDto {
  sectorKey: string;
  sectorName: string;
  regulatoryBody: string;
  scoreLabel: string;
  totalRequirements: number;
  coveredCount: number;
  pendingCount: number;
  gapCount: number;
  coveragePercentage: number;
  principleGroups: CompliancePrincipleGroupDto[];
  lastUpdated: string;
}

export interface AddManualMappingRequest {
  regulatoryRequirementId: string;
  toolboxTalkId: string | null;
  courseId: string | null;
}

export interface ContentOptionDto {
  id: string;
  title: string;
  type: "Talk" | "Course";
}
