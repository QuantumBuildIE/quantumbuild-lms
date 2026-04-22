'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  MultiSelectCombobox,
  type MultiSelectOption,
} from '@/components/ui/multi-select-combobox';
import { Card, CardContent } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Switch } from '@/components/ui/switch';
import { WizardSectionDivider } from '@/components/ui/wizard-section-divider';
import {
  AlertTriangle,
  FileText,
  FileVideo,
  Type,
  Upload,
  Loader2,
  Info,
  ArrowRight,
} from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';
import { useAuth } from '@/lib/auth/use-auth';
import { useLookupValues } from '@/hooks/use-lookups';
import { useAllCompanies } from '@/lib/api/admin/use-companies';
import { useTenantSectors, useAvailableSectors } from '@/lib/api/admin/use-tenant-sectors';
import { useCreateSession, useUploadSessionFile, useUpdateSource } from '@/lib/api/toolbox-talks/use-content-creation';
import { useAvailableLanguages } from '@/lib/api/toolbox-talks/use-subtitle-processing';
import { useTenantSettings } from '@/lib/api/admin/use-tenant-settings';
import type { WizardState } from '../CreateWizard';
import type { InputMode } from '@/types/content-creation';

// ============================================
// Props
// ============================================

interface InputConfigStepProps {
  state: WizardState;
  updateState: (updates: Partial<WizardState>) => void;
  onNext: () => void;
  onCancel: () => void;
}

// ============================================
// Constants
// ============================================

const INPUT_MODES: { mode: InputMode; label: string; icon: React.ElementType; description: string }[] = [
  { mode: 'Text', label: 'Text', icon: Type, description: 'Paste or type content directly' },
  { mode: 'Pdf', label: 'Document', icon: FileText, description: 'Upload a PDF document' },
  { mode: 'Video', label: 'Video', icon: FileVideo, description: 'Upload video or paste URL' },
];

const DEFAULT_PASS_THRESHOLDS = [50, 60, 70, 75, 80, 85, 90, 95];

const DEFAULT_AUDIT_PURPOSES = [
  'Regulatory Compliance',
  'Internal Training',
  'Safety Certification',
  'Client Requirement',
  'Quality Assurance',
  'Onboarding',
];

// ============================================
// Component
// ============================================

export function InputConfigStep({
  state,
  updateState,
  onNext,
  onCancel,
}: InputConfigStepProps) {
  const { user } = useAuth();
  const createSession = useCreateSession();
  const uploadFile = useUploadSessionFile();
  const updateSource = useUpdateSource();

  // Lookups
  const { data: languages = [], isLoading: languagesLoading } =
    useLookupValues('Language');
  const { data: companies = [], isLoading: companiesLoading } =
    useAllCompanies();
  const { data: tenantSettings } = useTenantSettings();

  // Employee preferred languages (for auto-populating target languages)
  const { data: availableLanguages } = useAvailableLanguages();

  // Sector data
  const tenantId = user?.tenantId ?? '';
  const {
    data: tenantSectors = [],
    isLoading: tenantSectorsLoading,
    isError: tenantSectorsError,
  } = useTenantSectors(tenantId);
  const { data: allSectors = [] } = useAvailableSectors();

  // Derive thresholds and audit purposes from tenant settings, falling back to defaults
  const passThresholds = (() => {
    const raw = tenantSettings?.['ValidationPassThresholds'];
    if (raw) {
      try {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed) && parsed.length > 0) return (parsed as number[]).sort((a, b) => a - b);
      } catch { /* use defaults */ }
    }
    return DEFAULT_PASS_THRESHOLDS;
  })();

  const auditPurposes = (() => {
    const raw = tenantSettings?.['ValidationAuditPurposes'];
    if (raw) {
      try {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed) && parsed.length > 0) return parsed as string[];
      } catch { /* use defaults */ }
    }
    return DEFAULT_AUDIT_PURPOSES;
  })();

  // Local state
  const [uploadProgress, setUploadProgress] = useState(0);
  const [isUploading, setIsUploading] = useState(false);
  const [customAuditPurpose, setCustomAuditPurpose] = useState('');
  const [auditPurposeMode, setAuditPurposeMode] = useState<'preset' | 'custom'>(
    state.auditPurpose && !auditPurposes.includes(state.auditPurpose)
      ? 'custom'
      : 'preset'
  );
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Pre-populate audit metadata from JWT user
  useEffect(() => {
    if (user && !state.reviewerName) {
      updateState({
        reviewerName: `${user.firstName} ${user.lastName}`.trim(),
        reviewerOrg: '', // Populated by user selection
        reviewerRole: user.roles[0] || '',
        documentRef: `DOC-${Date.now().toString(36).toUpperCase()}`,
      });
    }
  }, [user, state.reviewerName, updateState]);

  // Auto-populate target languages from employee preferred languages
  useEffect(() => {
    if (!availableLanguages || state.targetLanguageCodes.length > 0) return;
    const codes = availableLanguages.employeeLanguages
      .filter((l) => l.employeeCount > 0 && l.languageCode !== 'en')
      .map((l) => l.languageCode);
    if (codes.length > 0) {
      updateState({ targetLanguageCodes: codes });
    }
  }, [availableLanguages, state.targetLanguageCodes.length, updateState]);

  // Auto-select sector when tenant has exactly one, or default
  useEffect(() => {
    if (tenantSectorsLoading || tenantSectorsError || state.sectorKey) return;
    if (tenantSectors.length === 1) {
      updateState({ sectorKey: tenantSectors[0].sectorKey });
    } else if (tenantSectors.length > 1) {
      const defaultSector = tenantSectors.find((s) => s.isDefault);
      if (defaultSector) {
        updateState({ sectorKey: defaultSector.sectorKey });
      }
    }
  }, [tenantSectors, tenantSectorsLoading, tenantSectorsError, state.sectorKey, updateState]);

  // Build language options for multi-select
  const languageOptions: MultiSelectOption[] = languages.map((lang) => ({
    value: lang.code,
    label: lang.name,
    description: lang.code,
  }));

  // Build company options for client dropdown
  const companyList = Array.isArray(companies) ? companies : [];

  // ============================================
  // Mode selection
  // ============================================

  const handleModeSelect = (mode: InputMode) => {
    updateState({
      inputMode: mode,
      sourceText: mode !== state.inputMode ? '' : state.sourceText,
      sourceFile: mode !== state.inputMode ? null : state.sourceFile,
      sourceFileName: mode !== state.inputMode ? null : state.sourceFileName,
      videoUrl: mode !== state.inputMode ? '' : state.videoUrl,
    });
  };

  // ============================================
  // File handling
  // ============================================

  const handleFileSelect = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;

      const isPdf = state.inputMode === 'Pdf';
      const maxSize = isPdf ? 50 * 1024 * 1024 : 500 * 1024 * 1024;
      const allowedTypes = isPdf
        ? ['application/pdf']
        : ['video/mp4', 'video/mov', 'video/avi', 'video/webm', 'video/quicktime'];

      if (file.size > maxSize) {
        toast.error(`File too large. Maximum size is ${isPdf ? '50MB' : '500MB'}.`);
        return;
      }

      if (!allowedTypes.includes(file.type)) {
        toast.error(
          isPdf
            ? 'Only PDF files are accepted.'
            : 'Only MP4, MOV, AVI, and WebM files are accepted.'
        );
        return;
      }

      updateState({ sourceFile: file, sourceFileName: file.name });
    },
    [state.inputMode, updateState]
  );

  const handleFileDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      const file = e.dataTransfer.files[0];
      if (file) {
        const syntheticEvent = {
          target: { files: [file] },
        } as unknown as React.ChangeEvent<HTMLInputElement>;
        handleFileSelect(syntheticEvent);
      }
    },
    [handleFileSelect]
  );

  // ============================================
  // Continue / Session creation
  // ============================================

  // Sector is required when tenant has multiple sectors configured (Case B)
  const sectorRequired = !tenantSectorsLoading && !tenantSectorsError && tenantSectors.length > 1;

  const canContinue =
    !!state.inputMode &&
    (!sectorRequired || !!state.sectorKey) &&
    (state.inputMode === 'Text'
      ? state.sourceText.trim().length > 0
      : state.inputMode === 'Pdf'
        ? !!state.sourceFile
        : !!state.sourceFile || state.videoUrl.trim().length > 0);

  const handleContinue = async () => {
    if (!canContinue || !state.inputMode) return;

    try {
      // Resolve audit purpose
      const resolvedAuditPurpose =
        auditPurposeMode === 'custom'
          ? customAuditPurpose
          : state.auditPurpose;

      // Step 1: Create session if not yet created, or update source if returning
      let sessionId = state.sessionId;

      if (!sessionId) {
        const session = await createSession.mutateAsync({
          inputMode: state.inputMode,
          sourceText: state.inputMode === 'Text' ? state.sourceText : undefined,
          sectorKey: state.sectorKey || undefined,
          passThreshold: state.passThreshold,
          includeQuiz: state.includeQuiz,
          reviewerName: state.reviewerName || undefined,
          reviewerOrg: state.reviewerOrg || undefined,
          reviewerRole: state.reviewerRole || undefined,
          documentRef: state.documentRef || undefined,
          clientName: state.clientName || undefined,
          auditPurpose: resolvedAuditPurpose || undefined,
        });

        sessionId = session.id;
        updateState({ sessionId: session.id });
      } else {
        // Session exists — update source content and reset to Draft for re-parsing
        await updateSource.mutateAsync({
          sessionId,
          sourceText: state.inputMode === 'Text' ? state.sourceText : undefined,
        });
        updateState({
          parsedSections: [],
          suggestedOutputType: null,
          selectedOutputType: null,
        });
      }

      // Step 2: Upload file if applicable
      if (
        (state.inputMode === 'Pdf' || state.inputMode === 'Video') &&
        state.sourceFile
      ) {
        setIsUploading(true);
        setUploadProgress(0);

        await uploadFile.mutateAsync({
          sessionId,
          file: state.sourceFile,
          onProgress: setUploadProgress,
        });

        setIsUploading(false);
      }

      onNext();
    } catch (error) {
      setIsUploading(false);
      const message =
        error instanceof Error ? error.message : 'Failed to create session';
      toast.error('Error', { description: message });
    }
  };

  // ============================================
  // Render
  // ============================================

  return (
    <div className="space-y-6">
      {/* 1a — Content Source */}
      <WizardSectionDivider number="1a" label="Content Source" />

      {/* Mode Selection */}
      <div>
        <Label className="mb-3 block text-sm font-medium">Content Source</Label>
        <div className="grid grid-cols-3 gap-3">
          {INPUT_MODES.map(({ mode, label, icon: Icon, description }) => (
            <button
              key={mode}
              type="button"
              onClick={() => handleModeSelect(mode)}
              className={cn(
                'flex flex-col items-center gap-2 rounded-lg border-2 p-4 text-center transition-all hover:border-primary/50',
                state.inputMode === mode
                  ? 'border-primary bg-primary/5'
                  : 'border-muted'
              )}
            >
              <Icon
                className={cn(
                  'h-6 w-6',
                  state.inputMode === mode
                    ? 'text-primary'
                    : 'text-muted-foreground'
                )}
              />
              <span className="text-sm font-medium">{label}</span>
              <span className="text-xs text-muted-foreground">
                {description}
              </span>
            </button>
          ))}
        </div>
      </div>

      {/* Input area — shown when mode is selected */}
      {state.inputMode && (
        <div className="space-y-2">
          {state.inputMode === 'Text' && (
            <>
              <Label htmlFor="source-text">Content Text</Label>
              <Textarea
                id="source-text"
                placeholder="Paste or type your training content here..."
                value={state.sourceText}
                onChange={(e) => updateState({ sourceText: e.target.value })}
                rows={8}
                className="font-mono text-sm"
              />
              <p className="text-xs text-muted-foreground">
                {state.sourceText.length > 0
                  ? `${state.sourceText.split(/\s+/).filter(Boolean).length} words`
                  : 'Minimum 50 words recommended'}
              </p>
            </>
          )}

          {state.inputMode === 'Pdf' && (
            <div
              onDragOver={(e) => e.preventDefault()}
              onDrop={handleFileDrop}
              className={cn(
                'flex flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-8 transition-colors',
                state.sourceFile
                  ? 'border-primary bg-primary/5'
                  : 'border-muted hover:border-muted-foreground/50'
              )}
            >
              {state.sourceFile ? (
                <>
                  <FileText className="h-8 w-8 text-primary" />
                  <div className="text-center">
                    <p className="text-sm font-medium">{state.sourceFileName}</p>
                    <p className="text-xs text-muted-foreground">
                      {(state.sourceFile.size / (1024 * 1024)).toFixed(1)} MB
                    </p>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      updateState({ sourceFile: null, sourceFileName: null });
                      if (fileInputRef.current) fileInputRef.current.value = '';
                    }}
                  >
                    Remove
                  </Button>
                </>
              ) : (
                <>
                  <Upload className="h-8 w-8 text-muted-foreground" />
                  <div className="text-center">
                    <p className="text-sm font-medium">
                      Drop PDF here or click to browse
                    </p>
                    <p className="text-xs text-muted-foreground">
                      PDF only, max 50MB
                    </p>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => fileInputRef.current?.click()}
                  >
                    Browse Files
                  </Button>
                </>
              )}
              <input
                ref={fileInputRef}
                type="file"
                accept=".pdf"
                onChange={handleFileSelect}
                className="hidden"
              />
            </div>
          )}

          {state.inputMode === 'Video' && (
            <div className="space-y-4">
              {/* File upload */}
              <div
                onDragOver={(e) => e.preventDefault()}
                onDrop={handleFileDrop}
                className={cn(
                  'flex flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-6 transition-colors',
                  state.sourceFile
                    ? 'border-primary bg-primary/5'
                    : 'border-muted hover:border-muted-foreground/50'
                )}
              >
                {state.sourceFile ? (
                  <>
                    <FileVideo className="h-8 w-8 text-primary" />
                    <div className="text-center">
                      <p className="text-sm font-medium">
                        {state.sourceFileName}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {(state.sourceFile.size / (1024 * 1024)).toFixed(1)} MB
                      </p>
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        updateState({ sourceFile: null, sourceFileName: null });
                        if (fileInputRef.current) fileInputRef.current.value = '';
                      }}
                    >
                      Remove
                    </Button>
                  </>
                ) : (
                  <>
                    <Upload className="h-8 w-8 text-muted-foreground" />
                    <div className="text-center">
                      <p className="text-sm font-medium">
                        Drop video here or click to browse
                      </p>
                      <p className="text-xs text-muted-foreground">
                        MP4, MOV, AVI, WebM — max 500MB
                      </p>
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => fileInputRef.current?.click()}
                    >
                      Browse Files
                    </Button>
                  </>
                )}
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".mp4,.mov,.avi,.webm"
                  onChange={handleFileSelect}
                  className="hidden"
                />
              </div>

              {/* Web optimisation advisory */}
              <p className="flex items-start gap-1.5 text-xs text-muted-foreground">
                <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <span>
                  For best results, videos should be web-optimised before uploading. This ensures smooth playback for employees and reliable processing. We recommend{' '}
                  <a
                    href="https://handbrake.fr"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="underline underline-offset-2 hover:text-foreground"
                  >
                    HandBrake (free)
                  </a>
                  {' '}— use the Web preset.
                </span>
              </p>

              {/* OR divider */}
              {!state.sourceFile && (
                <>
                  <div className="relative">
                    <div className="absolute inset-0 flex items-center">
                      <span className="w-full border-t" />
                    </div>
                    <div className="relative flex justify-center text-xs uppercase">
                      <span className="bg-background px-2 text-muted-foreground">
                        or paste URL
                      </span>
                    </div>
                  </div>

                  <Input
                    placeholder="https://youtube.com/watch?v=... or direct video URL"
                    value={state.videoUrl}
                    onChange={(e) => updateState({ videoUrl: e.target.value })}
                  />
                </>
              )}
            </div>
          )}
        </div>
      )}

      {/* Upload progress */}
      {isUploading && (
        <div className="space-y-2">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Uploading... {uploadProgress}%
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-muted">
            <div
              className="h-full bg-primary transition-all"
              style={{ width: `${uploadProgress}%` }}
            />
          </div>
        </div>
      )}

      {/* 1b — Translation Settings */}
      <WizardSectionDivider number="1b" label="Translation Settings" />

      {/* Target Languages + Translation Pass Threshold */}
      <div className="flex items-start gap-4">
        <div className="min-w-0" style={{ flex: '1 1 60%' }}>
          <Label className="mb-2 block text-sm font-medium">
            Target Languages
          </Label>
          <MultiSelectCombobox
            options={languageOptions}
            selectedValues={state.targetLanguageCodes}
            onValuesChange={(values) =>
              updateState({ targetLanguageCodes: values })
            }
            placeholder="Select target languages..."
            searchPlaceholder="Search languages..."
            isLoading={languagesLoading}
            showSelectAll
            listClassName="[&_[cmdk-group-items]]:grid [&_[cmdk-group-items]]:md:grid-cols-2 [&_[cmdk-group-items]]:lg:grid-cols-3"
          />
          <p className="mt-1 text-xs text-muted-foreground">
            Content will be translated and validated for each selected language
          </p>
        </div>
        <div className="w-52 shrink-0">
          <Label className="mb-2 block text-sm font-medium">
            Translation Pass Threshold
          </Label>
          <Select
            value={String(state.passThreshold)}
            onValueChange={(v) => updateState({ passThreshold: Number(v) })}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {passThresholds.map((t) => (
                <SelectItem key={t} value={String(t)}>
                  {t}%
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="mt-1 text-xs text-muted-foreground">
            Min score to pass
          </p>
        </div>
      </div>

      {/* 1c — Content Options */}
      <WizardSectionDivider number="1c" label="Content Options" />

      {/* Include Quiz Toggle */}
      <div className="flex items-center justify-between rounded-lg border p-4">
        <div className="space-y-0.5">
          <Label htmlFor="include-quiz" className="text-sm font-medium">
            Include Quiz
          </Label>
          <p className="text-xs text-muted-foreground">
            Generate quiz questions for this content. When disabled, the Quiz step is skipped.
          </p>
        </div>
        <Switch
          id="include-quiz"
          checked={state.includeQuiz}
          onCheckedChange={(checked) => updateState({ includeQuiz: checked })}
        />
      </div>

      {/* 1d — Sector */}
      <WizardSectionDivider number="1d" label="Sector" />

      {/* Sector Selection */}
      {tenantSectorsLoading ? (
        <div className="flex items-center gap-3 rounded-lg border p-4">
          <Skeleton className="h-5 w-5 rounded" />
          <div className="flex-1 space-y-1.5">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-3 w-48" />
          </div>
        </div>
      ) : tenantSectorsError || tenantSectors.length === 0 ? (
        /* Case C — No sectors configured (or fetch error) */
        <div className="space-y-3">
          <Alert className="border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30">
            <AlertTriangle className="h-4 w-4 text-amber-600" />
            <AlertDescription className="text-amber-700 dark:text-amber-400">
              No sector configured for your account. Contact your administrator
              to set up sector configuration. Regulatory scoring will not be
              available for this run.
            </AlertDescription>
          </Alert>
          <div>
            <Label className="mb-2 block text-sm font-medium">
              Sector{' '}
              <span className="font-normal text-muted-foreground">
                (Optional — select to enable regulatory scoring)
              </span>
            </Label>
            <Select
              value={state.sectorKey ?? ''}
              onValueChange={(v) =>
                updateState({ sectorKey: v || null })
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="Select a sector..." />
              </SelectTrigger>
              <SelectContent>
                {allSectors.map((s) => (
                  <SelectItem key={s.id} value={s.key}>
                    {s.icon ? `${s.icon} ${s.name}` : s.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      ) : tenantSectors.length === 1 ? (
        /* Case A — Single sector, auto-selected */
        <div className="flex items-center gap-3 rounded-lg border p-4">
          {tenantSectors[0].sectorIcon && (
            <span className="text-lg">{tenantSectors[0].sectorIcon}</span>
          )}
          <div className="min-w-0">
            <p className="text-sm font-medium">{tenantSectors[0].sectorName}</p>
            <p className="text-xs text-muted-foreground">
              Auto-selected based on your account configuration
            </p>
          </div>
        </div>
      ) : (
        /* Case B — Multiple sectors, user picks */
        <div>
          <Label className="mb-2 block text-sm font-medium">Sector</Label>
          <Select
            value={state.sectorKey ?? ''}
            onValueChange={(v) => updateState({ sectorKey: v })}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select the sector this document belongs to" />
            </SelectTrigger>
            <SelectContent>
              {tenantSectors.map((s) => (
                <SelectItem key={s.id} value={s.sectorKey}>
                  {s.sectorIcon ? `${s.sectorIcon} ${s.sectorName}` : s.sectorName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="mt-1 text-xs text-muted-foreground">
            Select the sector this document belongs to
          </p>
        </div>
      )}

      {/* 1e — Audit Metadata */}
      <WizardSectionDivider number="1e" label="Audit Metadata" />

      {/* Audit Metadata */}
      <Card>
        <CardContent className="pt-4">
          <h3 className="mb-4 text-sm font-medium">Audit Metadata</h3>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="reviewer-name" className="text-xs">
                Reviewer Name
              </Label>
              <Input
                id="reviewer-name"
                value={state.reviewerName}
                onChange={(e) =>
                  updateState({ reviewerName: e.target.value })
                }
                placeholder="Auto-populated from profile"
              />
            </div>
            <div>
              <Label htmlFor="reviewer-org" className="text-xs">
                Organisation
              </Label>
              <Input
                id="reviewer-org"
                value={state.reviewerOrg}
                onChange={(e) =>
                  updateState({ reviewerOrg: e.target.value })
                }
                placeholder="Your organisation"
              />
            </div>
            <div>
              <Label htmlFor="reviewer-role" className="text-xs">
                Role
              </Label>
              <Input
                id="reviewer-role"
                value={state.reviewerRole}
                onChange={(e) =>
                  updateState({ reviewerRole: e.target.value })
                }
                placeholder="Auto-populated from JWT"
              />
            </div>
            <div>
              <Label htmlFor="document-ref" className="text-xs">
                Document Reference
              </Label>
              <Input
                id="document-ref"
                value={state.documentRef}
                onChange={(e) =>
                  updateState({ documentRef: e.target.value })
                }
                placeholder="Auto-generated"
              />
            </div>
            <div>
              <Label htmlFor="client-name" className="text-xs">
                Client
              </Label>
              <Select
                value={state.clientName}
                onValueChange={(v) => updateState({ clientName: v })}
              >
                <SelectTrigger id="client-name">
                  <SelectValue placeholder="Select client..." />
                </SelectTrigger>
                <SelectContent>
                  {companiesLoading ? (
                    <SelectItem value="_loading" disabled>
                      Loading...
                    </SelectItem>
                  ) : (
                    companyList.map((c) => (
                      <SelectItem key={c.id} value={c.companyName}>
                        {c.companyName}
                      </SelectItem>
                    ))
                  )}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="audit-purpose" className="text-xs">
                Audit Purpose
              </Label>
              {auditPurposeMode === 'preset' ? (
                <Select
                  value={state.auditPurpose}
                  onValueChange={(v) => {
                    if (v === '_other') {
                      setAuditPurposeMode('custom');
                      updateState({ auditPurpose: '' });
                    } else {
                      updateState({ auditPurpose: v });
                    }
                  }}
                >
                  <SelectTrigger id="audit-purpose">
                    <SelectValue placeholder="Select purpose..." />
                  </SelectTrigger>
                  <SelectContent position="popper" className="max-h-60">
                    {auditPurposes.map((p) => (
                      <SelectItem key={p} value={p}>
                        {p}
                      </SelectItem>
                    ))}
                    <SelectItem value="_other">Other...</SelectItem>
                  </SelectContent>
                </Select>
              ) : (
                <div className="flex gap-2">
                  <Input
                    value={customAuditPurpose}
                    onChange={(e) => {
                      setCustomAuditPurpose(e.target.value);
                      updateState({ auditPurpose: e.target.value });
                    }}
                    placeholder="Describe purpose..."
                  />
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      setAuditPurposeMode('preset');
                      setCustomAuditPurpose('');
                    }}
                    className="shrink-0"
                  >
                    Presets
                  </Button>
                </div>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Info alert */}
      {state.inputMode && !languagesLoading && languages.length === 0 && (
        <Alert variant="destructive">
          <Info className="h-4 w-4" />
          <AlertDescription>
            No languages configured for your organisation. Contact your
            administrator to set up target languages.
          </AlertDescription>
        </Alert>
      )}
      {/* Actions */}
      <div className="flex items-center justify-between pt-2">
        <Button variant="outline" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          onClick={handleContinue}
          disabled={
            !canContinue ||
            createSession.isPending ||
            updateSource.isPending ||
            uploadFile.isPending ||
            isUploading
          }
        >
          {createSession.isPending || updateSource.isPending || isUploading ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              {isUploading ? 'Uploading...' : updateSource.isPending ? 'Updating...' : 'Creating session...'}
            </>
          ) : (
            <>
              Continue
              <ArrowRight className="ml-2 h-4 w-4" />
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
