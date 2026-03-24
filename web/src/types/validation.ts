// ============================================
// Safety Glossary Types
// ============================================

export interface GlossarySectorListItem {
  id: string;
  sectorKey: string;
  sectorName: string;
  sectorIcon: string | null;
  isSystemDefault: boolean;
  termCount: number;
}

export interface GlossaryTermDto {
  id: string;
  englishTerm: string;
  category: string;
  isCritical: boolean;
  translations: string; // JSON: {"pl":"...", "ro":"...", ...}
}

export interface GlossarySectorDetail {
  id: string;
  sectorKey: string;
  sectorName: string;
  sectorIcon: string | null;
  isSystemDefault: boolean;
  terms: GlossaryTermDto[];
}

export interface CreateSectorRequest {
  sectorKey: string;
  sectorName: string;
  sectorIcon?: string;
}

export interface UpdateSectorRequest {
  sectorName: string;
  sectorIcon?: string;
}

export interface CreateTermRequest {
  englishTerm: string;
  category: string;
  isCritical: boolean;
  translations: string; // JSON string
}

export interface UpdateTermRequest {
  englishTerm: string;
  category: string;
  isCritical: boolean;
  translations: string; // JSON string
}

export interface ImportTermsResultDto {
  imported: number;
  skipped: number;
  errors: string[];
}
