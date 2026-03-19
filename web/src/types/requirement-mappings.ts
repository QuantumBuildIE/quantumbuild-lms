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
