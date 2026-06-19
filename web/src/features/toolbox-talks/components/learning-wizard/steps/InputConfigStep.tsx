'use client';

import { useEffect, useRef, useCallback, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import {
  AlertTriangle,
  FileText,
  FileVideo,
  Type,
  Upload,
  Loader2,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Switch } from '@/components/ui/switch';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent } from '@/components/ui/card';
import { WizardSectionDivider } from '@/components/ui/wizard-section-divider';
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
import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormMessage,
} from '@/components/ui/form';
import { cn } from '@/lib/utils';
import { useAuth } from '@/lib/auth/use-auth';
import { useLookupValues } from '@/hooks/use-lookups';
import { useAllCompanies } from '@/lib/api/admin/use-companies';
import { useTenantSectors, useAvailableSectors } from '@/lib/api/admin/use-tenant-sectors';
import { useTenantSettings } from '@/lib/api/admin/use-tenant-settings';
import { useAvailableLanguages } from '@/lib/api/toolbox-talks/use-subtitle-processing';
import type { SectorDto, TenantSectorDto } from '@/types/admin';
import { useInitialiseToolboxTalk } from '../hooks/useInitialiseToolboxTalk';
import { useUploadSourceFile } from '../hooks/useUploadSourceFile';
import { inputConfigSchema, type InputConfigValues } from '../schemas/inputConfigSchema';
import { getStepUrl } from '../lib/urlState';

// ============================================
// Constants
// ============================================

const INPUT_MODE_OPTIONS = [
  { mode: 'Text' as const, label: 'Text', icon: Type, description: 'Paste or type content directly' },
  { mode: 'Pdf' as const, label: 'Document', icon: FileText, description: 'Upload a PDF document' },
  { mode: 'Video' as const, label: 'Video', icon: FileVideo, description: 'Upload video or paste URL' },
];

const AUDIENCE_ROLE_OPTIONS = [
  { value: 'Operator', label: 'Operator' },
  { value: 'Supervisor', label: 'Supervisor' },
  { value: 'Auditor', label: 'Auditor' },
];

const DEFAULT_PASS_THRESHOLDS = [50, 60, 70, 75, 80, 85, 90, 95];

const DEFAULT_AUDIT_PURPOSES = [
  'Regulatory Compliance',
  'Internal Learning',
  'Safety Certification',
  'Client Requirement',
  'Quality Assurance',
  'Onboarding',
];

// ============================================
// Component
// ============================================

export function InputConfigStep() {
  const router = useRouter();
  const { user } = useAuth();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const selectedFileRef = useRef<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [auditPurposeMode, setAuditPurposeMode] = useState<'preset' | 'custom'>('preset');
  const [customAuditPurpose, setCustomAuditPurpose] = useState('');

  // Data queries
  const { data: languages = [], isLoading: languagesLoading } = useLookupValues('Language');
  const { data: availableLanguages } = useAvailableLanguages();
  const { data: tenantSettings } = useTenantSettings();
  const { data: companies = [], isLoading: companiesLoading } = useAllCompanies();
  const tenantId = user?.tenantId ?? '';
  const {
    data: tenantSectors = [],
    isLoading: tenantSectorsLoading,
    isError: tenantSectorsError,
  } = useTenantSectors(tenantId);
  const { data: allSectors = [] } = useAvailableSectors();

  const initialiseMutation = useInitialiseToolboxTalk();
  const { upload, progress, isUploading, error: uploadError, reset: resetUpload } = useUploadSourceFile();

  // Derive pass thresholds and default from tenant settings
  const passThresholds = (() => {
    const raw = tenantSettings?.['ValidationPassThresholds'];
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as number[];
        if (Array.isArray(parsed) && parsed.length > 0) return parsed.sort((a, b) => a - b);
      } catch { /* ignore */ }
    }
    return DEFAULT_PASS_THRESHOLDS;
  })();

  const defaultPassThreshold = passThresholds[0] ?? 75;

  // Derive audit purposes from tenant settings
  const auditPurposes = (() => {
    const raw = tenantSettings?.['ValidationAuditPurposes'];
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as string[];
        if (Array.isArray(parsed) && parsed.length > 0) return parsed;
      } catch { /* ignore */ }
    }
    return DEFAULT_AUDIT_PURPOSES;
  })();

  const companyList = Array.isArray(companies) ? companies : [];

  // ---- Form setup ----
  const form = useForm<InputConfigValues>({
    resolver: zodResolver(inputConfigSchema),
    mode: 'onBlur',
    defaultValues: {
      title: '',
      inputMode: 'Text',
      sourceText: '',
      sourceFileUrl: undefined,
      sourceFileName: undefined,
      sourceFileType: undefined,
      videoUrl: '',
      videoRightsConfirmed: false,
      targetLanguageCodes: [],
      passThreshold: defaultPassThreshold,
      includeQuiz: true,
      audienceRole: 'Operator',
      preserveSourceWording: false,
      sectorKey: undefined,
      reviewerName: user ? `${user.firstName} ${user.lastName}`.trim() : '',
      reviewerOrg: '',
      reviewerRole: user?.roles[0] ?? '',
      documentRef: '',
      clientName: '',
      auditPurpose: '',
    },
  });

  const inputMode = form.watch('inputMode');
  const sourceText = form.watch('sourceText');
  const sourceFileName = form.watch('sourceFileName');
  const videoUrl = form.watch('videoUrl');
  const { errors } = form.formState;

  // Pre-populate reviewer defaults from JWT user
  useEffect(() => {
    if (!user) return;
    const { reviewerName, reviewerRole } = form.getValues();
    if (!reviewerName) {
      form.setValue('reviewerName', `${user.firstName} ${user.lastName}`.trim(), { shouldDirty: false });
    }
    if (!reviewerRole) {
      form.setValue('reviewerRole', user.roles[0] ?? '', { shouldDirty: false });
    }
  }, [user, form]);

  // Auto-populate target languages from employee preferred languages
  useEffect(() => {
    const current = form.getValues('targetLanguageCodes');
    if (!availableLanguages || current.length > 0) return;
    const codes = (availableLanguages.employeeLanguages ?? [])
      .filter((l) => l.employeeCount > 0 && l.languageCode !== 'en')
      .map((l) => l.languageCode);
    if (codes.length > 0) {
      form.setValue('targetLanguageCodes', codes, { shouldDirty: false });
    }
  }, [availableLanguages, form]);

  // Sector auto-select
  useEffect(() => {
    if (tenantSectorsLoading || tenantSectorsError) return;
    const current = form.getValues('sectorKey');
    if (current) return;
    if (tenantSectors.length === 1) {
      form.setValue('sectorKey', tenantSectors[0].sectorKey, { shouldDirty: false });
    } else if (tenantSectors.length > 1) {
      const defaultSector = tenantSectors.find((s) => s.isDefault);
      if (defaultSector) {
        form.setValue('sectorKey', defaultSector.sectorKey, { shouldDirty: false });
      }
    }
  }, [tenantSectors, tenantSectorsLoading, tenantSectorsError, form]);

  // Reset file state when mode changes
  const handleModeChange = useCallback(
    (mode: InputConfigValues['inputMode']) => {
      form.setValue('inputMode', mode);
      if (mode !== inputMode) {
        form.setValue('sourceText', '');
        form.setValue('sourceFileUrl', undefined);
        form.setValue('sourceFileName', undefined);
        form.setValue('sourceFileType', undefined);
        form.setValue('videoUrl', '');
        form.setValue('videoRightsConfirmed', false);
        selectedFileRef.current = null;
        resetUpload();
        if (fileInputRef.current) fileInputRef.current.value = '';
      }
    },
    [inputMode, form, resetUpload]
  );

  // File selection (shared by click and drag-and-drop)
  const handleFileSelected = useCallback(
    (file: File) => {
      const isPdf = inputMode === 'Pdf';
      const maxSize = isPdf ? 50 * 1024 * 1024 : 500 * 1024 * 1024;
      const allowed = isPdf
        ? ['application/pdf']
        : ['video/mp4', 'video/webm', 'video/quicktime'];

      if (!allowed.includes(file.type)) {
        toast.error(`Invalid file type. Allowed: ${allowed.join(', ')}`);
        return;
      }
      if (file.size > maxSize) {
        toast.error(`File too large. Maximum: ${isPdf ? '50MB' : '500MB'}`);
        return;
      }

      selectedFileRef.current = file;
      form.setValue('sourceFileName', file.name, { shouldValidate: false });
      form.setValue('sourceFileType', file.type, { shouldValidate: false });
      form.setValue('sourceFileUrl', undefined, { shouldValidate: false });
    },
    [inputMode, form]
  );

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) handleFileSelected(file);
    },
    [handleFileSelected]
  );

  const handleRemoveFile = useCallback(() => {
    selectedFileRef.current = null;
    resetUpload();
    form.setValue('sourceFileUrl', undefined);
    form.setValue('sourceFileName', undefined);
    form.setValue('sourceFileType', undefined);
    if (fileInputRef.current) fileInputRef.current.value = '';
  }, [form, resetUpload]);

  // Continue / submit
  const onSubmit = useCallback(
    async (values: InputConfigValues) => {
      let finalSourceFileUrl = values.sourceFileUrl;
      let finalSourceFileName = values.sourceFileName;
      let finalSourceFileType = values.sourceFileType;

      if (selectedFileRef.current && !finalSourceFileUrl) {
        try {
          const result = await upload(selectedFileRef.current);
          finalSourceFileUrl = result.publicUrl;
          finalSourceFileName = result.fileName;
          finalSourceFileType = result.contentType;
        } catch {
          toast.error('File upload failed. Please try again.');
          return;
        }
      }

      try {
        const talk = await initialiseMutation.mutateAsync({
          title: values.title,
          inputMode: values.inputMode,
          sourceLanguageCode: 'en',
          sourceText: values.inputMode === 'Text' ? values.sourceText : undefined,
          sourceFileUrl: finalSourceFileUrl,
          sourceFileName: finalSourceFileName,
          sourceFileType: finalSourceFileType,
          videoUrl: values.inputMode === 'Video' ? values.videoUrl : undefined,
          videoSource: values.videoUrl ? 'DirectUrl' : undefined,
          targetLanguageCodes: values.targetLanguageCodes,
          reviewerName: values.reviewerName || undefined,
          reviewerOrg: values.reviewerOrg || undefined,
          reviewerRole: values.reviewerRole || undefined,
          documentRef: values.documentRef || undefined,
          clientName: values.clientName || undefined,
          auditPurpose: values.auditPurpose || undefined,
          audienceRole: values.audienceRole,
          preserveSourceWording: values.preserveSourceWording,
          includeQuiz: values.includeQuiz,
        });

        router.push(getStepUrl(talk.id, 2));
      } catch (err) {
        // The backend returns 400 { message: "A learning with title 'X' already exists." }
        // for duplicate titles. The match relies on the substring "already exists" — if
        // the backend message ever changes, this falls back to the toast (correct, but
        // less useful). InvalidOperationException is the only source of this shape
        // from POST /toolbox-talks/initialise; FluentValidation errors have a different
        // shape ({ message, errors: [...] }) and won't false-positive here.
        const data = (err as { response?: { data?: { message?: string } } }).response?.data;
        const serverMessage = data?.message;
        if (serverMessage?.includes('already exists')) {
          form.setError('title', { type: 'manual', message: serverMessage });
          form.setFocus('title');
        } else {
          const fallback = serverMessage ?? (err instanceof Error ? err.message : 'Failed to create learning');
          toast.error(fallback);
        }
      }
    },
    [initialiseMutation, upload, router]
  );

  const isSubmitting = initialiseMutation.isPending || isUploading;

  // Language options
  const languageOptions: MultiSelectOption[] = languages.map(
    (lang) => ({ value: lang.code, label: lang.name })
  );

  // Sector UI — three-case branching
  const sectorField = (() => {
    if (tenantSectorsLoading) return null;
    if (tenantSectorsError || tenantSectors.length === 0) {
      return (
        <div className="space-y-2">
          <Alert>
            <AlertTriangle className="h-4 w-4" aria-hidden="true" />
            <AlertDescription>
              No sectors configured for this tenant. Select one below or{' '}
              <a href="/admin/regulatory/my-sectors" className="underline">
                add sectors
              </a>{' '}
              first.
            </AlertDescription>
          </Alert>
          <FormField
            control={form.control}
            name="sectorKey"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Sector (optional)</FormLabel>
                <FormControl>
                  <Select value={field.value ?? ''} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select a sector" />
                    </SelectTrigger>
                    <SelectContent>
                      {(allSectors as SectorDto[]).map((s) => (
                        <SelectItem key={s.key} value={s.key}>
                          {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FormControl>
                <p className="text-xs text-muted-foreground">Used for regulatory scoring and compliance reporting</p>
                <FormMessage role="alert" />
              </FormItem>
            )}
          />
        </div>
      );
    }
    if (tenantSectors.length === 1) {
      return (
        <div className="flex items-center gap-3 rounded-lg border p-4">
          {(tenantSectors as TenantSectorDto[])[0].sectorIcon && (
            <span className="text-lg" aria-hidden="true">
              {(tenantSectors as TenantSectorDto[])[0].sectorIcon}
            </span>
          )}
          <div className="min-w-0">
            <p className="text-sm font-medium">{(tenantSectors as TenantSectorDto[])[0].sectorName}</p>
            <p className="text-xs text-muted-foreground">
              Auto-selected — used for regulatory scoring and compliance reporting
            </p>
          </div>
        </div>
      );
    }
    return (
      <FormField
        control={form.control}
        name="sectorKey"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Sector <span aria-hidden="true">*</span></FormLabel>
            <FormControl>
              <Select value={field.value ?? ''} onValueChange={field.onChange}>
                <SelectTrigger>
                  <SelectValue placeholder="Select a sector" />
                </SelectTrigger>
                <SelectContent>
                  {(tenantSectors as TenantSectorDto[]).map((s) => (
                    <SelectItem key={s.sectorKey} value={s.sectorKey}>
                      {s.sectorName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FormControl>
            <p className="text-xs text-muted-foreground">Used for regulatory scoring and compliance reporting</p>
            <FormMessage role="alert" />
          </FormItem>
        )}
      />
    );
  })();

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit(onSubmit, (errs) => {
          const firstError = Object.keys(errs)[0];
          if (firstError) form.setFocus(firstError as keyof InputConfigValues);
        })}
        className="space-y-8"
        aria-label="Learning wizard step 1 — input and configuration"
        noValidate
      >
        {/* ── Title ── */}
        <FormField
          control={form.control}
          name="title"
          render={({ field }) => (
            <FormItem>
              <FormLabel>
                Title <span aria-hidden="true">*</span>
              </FormLabel>
              <FormControl>
                <Input
                  {...field}
                  placeholder="e.g. Manual Handling Safety"
                  maxLength={200}
                  aria-required="true"
                  aria-describedby={errors.title ? 'title-error' : undefined}
                  aria-invalid={!!errors.title}
                />
              </FormControl>
              <FormMessage id="title-error" role="alert" />
            </FormItem>
          )}
        />

        {/* ── 1a Content Source ── */}
        <WizardSectionDivider number="1a" label="Content Source" firstSection />

        <fieldset className="space-y-3">
          <legend className="sr-only">
            Content source <span aria-hidden="true">*</span>
          </legend>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {INPUT_MODE_OPTIONS.map(({ mode, label, icon: Icon, description }) => (
              <button
                key={mode}
                type="button"
                onClick={() => handleModeChange(mode)}
                className={cn(
                  'flex flex-col items-center gap-2 rounded-lg border-2 p-4 text-center transition-all',
                  'min-h-[44px] hover:border-primary/50 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
                  inputMode === mode
                    ? 'border-primary bg-primary/5'
                    : 'border-border'
                )}
                aria-pressed={inputMode === mode}
              >
                <Icon
                  className={cn(
                    'h-6 w-6 shrink-0',
                    inputMode === mode ? 'text-primary' : 'text-muted-foreground'
                  )}
                  aria-hidden="true"
                />
                <span className="text-sm font-medium">{label}</span>
                <span className="text-xs text-muted-foreground">{description}</span>
              </button>
            ))}
          </div>
        </fieldset>

        {/* Source text */}
        {inputMode === 'Text' && (
          <FormField
            control={form.control}
            name="sourceText"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Source text</FormLabel>
                <FormControl>
                  <Textarea
                    {...field}
                    placeholder="Paste or type the content that the AI should use to generate sections and quiz questions..."
                    className="min-h-[160px] resize-y"
                    aria-describedby={errors.sourceText ? 'source-text-error' : undefined}
                    aria-invalid={!!errors.sourceText}
                  />
                </FormControl>
                <p className="text-xs text-muted-foreground">
                  {sourceText && sourceText.trim().length > 0
                    ? `${sourceText.split(/\s+/).filter(Boolean).length} words`
                    : 'Minimum 50 words recommended'}
                </p>
                <FormMessage id="source-text-error" role="alert" />
              </FormItem>
            )}
          />
        )}

        {/* PDF / Video dropzone */}
        {(inputMode === 'Pdf' || inputMode === 'Video') && (
          <div className="space-y-2">
            <Label htmlFor="source-file-input">
              {inputMode === 'Pdf' ? 'PDF document' : 'Video file'}{' '}
              <span aria-hidden="true">*</span>
            </Label>

            {!sourceFileName ? (
              <label
                htmlFor="source-file-input"
                onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
                onDragLeave={(e) => { e.preventDefault(); setIsDragging(false); }}
                onDrop={(e) => {
                  e.preventDefault();
                  setIsDragging(false);
                  const file = e.dataTransfer.files[0];
                  if (file) handleFileSelected(file);
                }}
                className={cn(
                  'flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 cursor-pointer',
                  'transition-colors min-h-[120px]',
                  isDragging
                    ? 'border-primary bg-primary/5'
                    : 'hover:border-primary/50'
                )}
              >
                <Upload className="h-8 w-8 text-muted-foreground mb-2" aria-hidden="true" />
                <p className="text-sm font-medium">
                  {isDragging
                    ? 'Drop to upload'
                    : inputMode === 'Pdf'
                      ? 'Drop a PDF here or click to browse'
                      : 'Drop a video file here or click to browse'}
                </p>
                <p className="text-xs text-muted-foreground mt-1">
                  {inputMode === 'Pdf' ? 'PDF only, max 50MB' : 'MP4, WebM, MOV — max 500MB'}
                </p>
                <input
                  id="source-file-input"
                  ref={fileInputRef}
                  type="file"
                  accept={inputMode === 'Pdf' ? '.pdf,application/pdf' : 'video/mp4,video/webm,video/quicktime'}
                  className="sr-only"
                  onChange={handleFileChange}
                  aria-label={`Select ${inputMode === 'Pdf' ? 'PDF' : 'video'} file`}
                />
              </label>
            ) : (
              <div className="flex items-center justify-between rounded-lg border bg-muted/30 px-4 py-3">
                <div className="flex items-center gap-2 min-w-0">
                  {inputMode === 'Pdf' ? (
                    <FileText className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                  ) : (
                    <FileVideo className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                  )}
                  <span className="truncate text-sm">{sourceFileName}</span>
                </div>
                <button
                  type="button"
                  onClick={handleRemoveFile}
                  className="ml-2 shrink-0 text-muted-foreground hover:text-foreground"
                  aria-label="Remove file"
                >
                  <X className="h-4 w-4" aria-hidden="true" />
                </button>
              </div>
            )}

            {isUploading && (
              <div role="status" aria-live="polite" aria-label={`Uploading: ${progress}%`}>
                <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
                  <div
                    className="h-full bg-primary transition-all"
                    style={{ width: `${progress}%` }}
                  />
                </div>
                <p className="mt-1 text-xs text-muted-foreground">Uploading… {progress}%</p>
              </div>
            )}

            {uploadError && (
              <p role="alert" className="text-sm text-destructive">{uploadError}</p>
            )}

            {errors.sourceFileUrl && (
              <p role="alert" className="text-sm text-destructive">
                {errors.sourceFileUrl.message}
              </p>
            )}
          </div>
        )}

        {/* Video URL + rights checkbox */}
        {inputMode === 'Video' && (
          <div className="space-y-4">
            <div className="relative flex items-center gap-3">
              <div className="flex-1 border-t" />
              <span className="text-xs text-muted-foreground shrink-0">or paste a URL</span>
              <div className="flex-1 border-t" />
            </div>

            <FormField
              control={form.control}
              name="videoUrl"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Video URL</FormLabel>
                  <FormControl>
                    <Input
                      {...field}
                      type="url"
                      placeholder="https://..."
                      aria-describedby={errors.videoUrl ? 'video-url-error' : undefined}
                      aria-invalid={!!errors.videoUrl}
                    />
                  </FormControl>
                  <FormMessage id="video-url-error" role="alert" />
                </FormItem>
              )}
            />

            {videoUrl && (
              <FormField
                control={form.control}
                name="videoRightsConfirmed"
                render={({ field }) => (
                  <FormItem className="flex items-start gap-2">
                    <FormControl>
                      <Checkbox
                        id="video-rights"
                        checked={field.value}
                        onCheckedChange={(checked) => field.onChange(checked === true)}
                        aria-describedby={errors.videoRightsConfirmed ? 'rights-error' : undefined}
                      />
                    </FormControl>
                    <div className="space-y-0.5">
                      <Label htmlFor="video-rights" className="cursor-pointer">
                        I confirm I have the rights to use this video for training purposes
                      </Label>
                      <FormMessage id="rights-error" role="alert" />
                    </div>
                  </FormItem>
                )}
              />
            )}
          </div>
        )}

        {/* ── 1b Translation Settings ── */}
        <WizardSectionDivider number="1b" label="Translation Settings" />

        <div className="flex items-start gap-3 rounded-lg border p-4">
          <div className="min-w-0" style={{ flex: '1 1 60%' }}>
            <FormField
              control={form.control}
              name="targetLanguageCodes"
              render={({ field }) => (
                <FormItem className="space-y-1">
                  <FormLabel>
                    Target languages <span aria-hidden="true">*</span>
                  </FormLabel>
                  <FormControl>
                    <MultiSelectCombobox
                      options={languageOptions}
                      selectedValues={field.value}
                      onValuesChange={(values) => field.onChange(values)}
                      placeholder={languagesLoading ? 'Loading languages…' : 'Select target languages'}
                    />
                  </FormControl>
                  <p className="text-xs text-muted-foreground">
                    Content will be translated and validated for each selected language.
                  </p>
                  <FormMessage id="lang-error" role="alert" />
                </FormItem>
              )}
            />
          </div>
          <div className="w-52 shrink-0">
            <FormField
              control={form.control}
              name="passThreshold"
              render={({ field }) => (
                <FormItem className="space-y-1">
                  <FormLabel>Pass threshold (%)</FormLabel>
                  <FormControl>
                    <Select
                      value={String(field.value)}
                      onValueChange={(v) => field.onChange(Number(v))}
                    >
                      <SelectTrigger aria-label="Pass threshold percentage">
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
                  </FormControl>
                  <p className="text-xs text-muted-foreground">Minimum score to pass validation</p>
                  <FormMessage role="alert" />
                </FormItem>
              )}
            />
          </div>
        </div>

        {/* ── 1c Content Options ── */}
        <WizardSectionDivider number="1c" label="Content Options" />

        <div className="space-y-3">
          {/* Include Quiz */}
          <FormField
            control={form.control}
            name="includeQuiz"
            render={({ field }) => (
              <FormItem className="flex items-center justify-between rounded-lg border p-4">
                <div>
                  <FormLabel className="text-sm font-medium">Include quiz</FormLabel>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Generate quiz questions for this content. When disabled, the Quiz step is skipped.
                  </p>
                </div>
                <FormControl>
                  <Switch
                    checked={field.value}
                    onCheckedChange={field.onChange}
                    aria-label="Include quiz"
                  />
                </FormControl>
              </FormItem>
            )}
          />

          {/* Audience Role */}
          <FormField
            control={form.control}
            name="audienceRole"
            render={({ field }) => (
              <FormItem className="rounded-lg border p-4">
                <div className="flex items-start gap-4">
                  <div className="min-w-0 flex-1">
                    <FormLabel className="text-sm font-medium">Audience</FormLabel>
                    <p className="mt-0.5 text-xs text-muted-foreground">
                      Determines quiz question style. Operators focus on procedure; Supervisors plan and oversee; Auditors verify compliance.
                    </p>
                  </div>
                  <div className="w-44 shrink-0">
                    <FormControl>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger aria-label="Audience role">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {AUDIENCE_ROLE_OPTIONS.map((opt) => (
                            <SelectItem key={opt.value} value={opt.value}>
                              {opt.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FormControl>
                  </div>
                </div>
                <FormMessage role="alert" />
              </FormItem>
            )}
          />

          {/* Preserve Source Wording */}
          <FormField
            control={form.control}
            name="preserveSourceWording"
            render={({ field }) => (
              <FormItem className="flex items-center justify-between rounded-lg border p-4">
                <div>
                  <FormLabel className="text-sm font-medium">Preserve source wording</FormLabel>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    When on, the AI keeps your source text exactly as written instead of rewriting for clarity.
                    Useful for SOPs or approved policy text that must not be paraphrased.
                  </p>
                </div>
                <FormControl>
                  <Switch
                    checked={field.value}
                    onCheckedChange={field.onChange}
                    aria-label="Preserve source wording"
                  />
                </FormControl>
              </FormItem>
            )}
          />
        </div>

        {/* ── 1d Sector ── */}
        <WizardSectionDivider number="1d" label="Sector" />
        {sectorField}

        {/* ── 1e Audit Metadata ── */}
        <WizardSectionDivider number="1e" label="Audit Metadata" />

        <Card>
          <CardContent>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="reviewerName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Reviewer name</FormLabel>
                    <FormControl>
                      <Input {...field} maxLength={200} />
                    </FormControl>
                    <p className="text-xs text-muted-foreground">Pre-populated from your profile — edit if different</p>
                    <FormMessage role="alert" />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="reviewerRole"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Reviewer role</FormLabel>
                    <FormControl>
                      <Input {...field} maxLength={200} />
                    </FormControl>
                    <p className="text-xs text-muted-foreground">Pre-populated from your profile — edit if different</p>
                    <FormMessage role="alert" />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="reviewerOrg"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Organisation</FormLabel>
                    <FormControl>
                      <Input {...field} maxLength={200} />
                    </FormControl>
                    <p className="text-xs text-muted-foreground">Pre-populated from your profile — edit if different</p>
                    <FormMessage role="alert" />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="clientName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Client</FormLabel>
                    <Select
                      value={field.value}
                      onValueChange={field.onChange}
                      disabled={!companiesLoading && companyList.length === 0}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={
                            companiesLoading
                              ? 'Loading…'
                              : companyList.length === 0
                                ? 'No companies available'
                                : 'Select client…'
                          }
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {companyList.map((c) => (
                          <SelectItem key={c.id} value={c.companyName}>
                            {c.companyName}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {!companiesLoading && companyList.length === 0 && (
                      <p className="text-xs text-muted-foreground">No companies configured for this tenant</p>
                    )}
                    <FormMessage role="alert" />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="documentRef"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>
                      Document reference{' '}
                      <span className="text-xs text-muted-foreground font-normal">
                        (auto-generated if blank)
                      </span>
                    </FormLabel>
                    <FormControl>
                      <Input {...field} maxLength={100} />
                    </FormControl>
                    <FormMessage role="alert" />
                  </FormItem>
                )}
              />
            </div>

            <div className="mt-4">
              <FormField
                control={form.control}
                name="auditPurpose"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Audit purpose</FormLabel>
                    {auditPurposeMode === 'preset' ? (
                      <Select
                        value={field.value}
                        onValueChange={(v) => {
                          if (v === '_other') {
                            setAuditPurposeMode('custom');
                            field.onChange('');
                          } else {
                            field.onChange(v);
                          }
                        }}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select purpose…" />
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
                            field.onChange(e.target.value);
                          }}
                          placeholder="Describe purpose…"
                        />
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setAuditPurposeMode('preset');
                            setCustomAuditPurpose('');
                            field.onChange('');
                          }}
                          className="shrink-0"
                        >
                          Presets
                        </Button>
                      </div>
                    )}
                    <FormMessage role="alert" />
                  </FormItem>
                )}
              />
            </div>
          </CardContent>
        </Card>

        {/* Continue button */}
        <div className="flex justify-end pt-2">
          <Button
            type="submit"
            disabled={isSubmitting}
            aria-busy={isSubmitting}
            className="min-w-[120px] min-h-[44px]"
          >
            {isSubmitting ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
                {isUploading ? `Uploading… ${progress}%` : 'Creating…'}
              </>
            ) : (
              'Continue'
            )}
          </Button>
        </div>
      </form>
    </Form>
  );
}
