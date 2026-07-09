export interface ExternalReviewFlagDto {
  startOffset: number;
  endOffset: number;
  severity: string;         // "Info" | "Warning" | "Error"
  reason: string;
}

export interface ExternalReviewSectionDto {
  sectionIndex: number;
  sectionTitle: string;
  originalText: string;
  translatedText: string;
  flags: ExternalReviewFlagDto[];
}

export interface ExternalReviewPortalDto {
  talkTitle: string;
  languageCode: string;
  languageName: string;
  expiresAt: string;        // ISO datetime
  portalStatus: string;     // "Active" | "Used" | "Revoked" | "Expired" | "Unknown"
  contextType: string;
  flaggedWordCount: number;
  sections: ExternalReviewSectionDto[];
  /** Null = no restriction, all sections are editable. Non-null = only these section indices are editable. */
  editableSectionIndices: number[] | null;
}

export interface ExternalReviewEditedSectionDto {
  sectionIndex: number;
  translatedText: string;
}

export interface SubmitExternalReviewRequest {
  accepted: boolean;
  editedContent: string | null;   // JSON-serialised ExternalReviewEditedSectionDto[]
}

export interface DeclineExternalReviewRequest {
  reason: string;
}
