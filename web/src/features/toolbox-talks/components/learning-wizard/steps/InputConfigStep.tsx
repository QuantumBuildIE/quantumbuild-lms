'use client';

import { useEffect, useRef, useCallback } from 'react';
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

// ============================================
// Component
// ============================================

export function InputConfigStep() {
  const router = useRouter();
  const { user } = useAuth();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const selectedFileRef = useRef<File | null>(null);

  // Data queries
  const { data: languages = [], isLoading: languagesLoading } = useLookupValues('Language');
  const { data: availableLanguages } = useAvailableLanguages();
  const { data: tenantSettings } = useTenantSettings();
  const tenantId = user?.tenantId ?? '';
  const { data: tenantSectors = [], isLoading: tenantSectorsLoading, isError: tenantSectorsError } =
    useTenantSectors(tenantId);
  const { data: allSectors = [] } = useAvailableSectors();

  const initialiseMutation = useInitialiseToolboxTalk();
  const { upload, progress, isUploading, error: uploadError, reset: resetUpload } = useUploadSourceFile();

  // Derive default pass threshold from tenant settings
  const defaultPassThreshold = (() => {
    const raw = tenantSettings?.['ValidationPassThresholds'];
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as number[];
        if (Array.isArray(parsed) && parsed.length > 0) return parsed[0];
      } catch { /* ignore */ }
    }
    return 75;
  })();

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

  // ---- Sector 3-case branching ----
  // Case 1: Single sector → auto-lock (no dropdown)
  // Case 2: Multiple sectors → required dropdown
  // Case 3: No sectors or error → optional dropdown over all sectors with an alert
  useEffect(() => {
    if (tenantSectorsLoading || tenantSectorsError) return;
    const current = form.getValues('sectorKey');
    if (current) return; // already set
    if (tenantSectors.length === 1) {
      form.setValue('sectorKey', tenantSectors[0].sectorKey, { shouldDirty: false });
    } else if (tenantSectors.length > 1) {
      const defaultSector = tenantSectors.find((s) => s.isDefault);
      if (defaultSector) {
        form.setValue('sectorKey', defaultSector.sectorKey, { shouldDirty: false });
      }
    }
  }, [tenantSectors, tenantSectorsLoading, tenantSectorsError, form]);

  // Reset file state when mode changes away from file mode
  const handleModeChange = useCallback(
    (mode: InputConfigValues['inputMode']) => {
      form.setValue('inputMode', mode);
      if (mode !== inputMode) {
        // Clear source-specific fields
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

  // ---- File selection ----
  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;

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
      // Show the filename immediately; URL is uploaded on Continue
      form.setValue('sourceFileName', file.name, { shouldValidate: false });
      form.setValue('sourceFileType', file.type, { shouldValidate: false });
      // Clear any previous URL so the cross-field rule doesn't pass from a stale upload
      form.setValue('sourceFileUrl', undefined, { shouldValidate: false });
    },
    [inputMode, form]
  );

  const handleRemoveFile = useCallback(() => {
    selectedFileRef.current = null;
    resetUpload();
    form.setValue('sourceFileUrl', undefined);
    form.setValue('sourceFileName', undefined);
    form.setValue('sourceFileType', undefined);
    if (fileInputRef.current) fileInputRef.current.value = '';
  }, [form, resetUpload]);

  // ---- Continue / submit ----
  const onSubmit = useCallback(
    async (values: InputConfigValues) => {
      let finalSourceFileUrl = values.sourceFileUrl;
      let finalSourceFileName = values.sourceFileName;
      let finalSourceFileType = values.sourceFileType;

      // Upload file if one was selected but not yet uploaded
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
        const message = err instanceof Error ? err.message : 'Failed to create learning';
        toast.error(message);
      }
    },
    [initialiseMutation, upload, router]
  );

  const isSubmitting = initialiseMutation.isPending || isUploading;
  const { errors } = form.formState;

  // ---- Language options ----
  const languageOptions: MultiSelectOption[] = languages.map(
    (lang) => ({ value: lang.code, label: lang.name })
  );

  // ---- Sector UI ----
  // Three-case sector branching:
  //   1) 0 sectors (or error) → optional dropdown over allSectors + warning alert
  //   2) 1 sector → auto-locked, show read-only badge
  //   3) 2+ sectors → required dropdown
  const sectorField = (() => {
    if (tenantSectorsLoading) return null;
    if (tenantSectorsError || tenantSectors.length === 0) {
      // Case 1: no sectors configured — optional selection over system-wide list
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
                <FormMessage />
              </FormItem>
            )}
          />
        </div>
      );
    }
    if (tenantSectors.length === 1) {
      // Case 2: single sector — auto-locked, no user action needed
      return (
        <div className="space-y-1">
          <Label>Sector</Label>
          <p className="text-sm text-muted-foreground">
            {(tenantSectors as TenantSectorDto[])[0].sectorName}
          </p>
        </div>
      );
    }
    // Case 3: multiple sectors — required dropdown
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
            <FormMessage />
          </FormItem>
        )}
      />
    );
  })();

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit(onSubmit)}
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

        {/* ── Input mode ── */}
        <fieldset className="space-y-3">
          <legend className="text-sm font-medium">
            Content source <span aria-hidden="true">*</span>
          </legend>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {INPUT_MODE_OPTIONS.map(({ mode, label, icon: Icon, description }) => (
              <button
                key={mode}
                type="button"
                onClick={() => handleModeChange(mode)}
                className={cn(
                  'flex flex-col items-start gap-1 rounded-lg border p-4 text-left transition-colors',
                  'min-h-[44px] focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
                  inputMode === mode
                    ? 'border-primary bg-primary/5'
                    : 'border-border hover:border-primary/50'
                )}
                aria-pressed={inputMode === mode}
              >
                <div className="flex items-center gap-2">
                  <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
                  <span className="text-sm font-medium">{label}</span>
                </div>
                <span className="text-xs text-muted-foreground">{description}</span>
              </button>
            ))}
          </div>
        </fieldset>

        {/* ── Source-mode-specific fields ── */}
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
                <FormMessage id="source-text-error" role="alert" />
              </FormItem>
            )}
          />
        )}

        {(inputMode === 'Pdf' || inputMode === 'Video') && (
          <div className="space-y-2">
            <Label htmlFor="source-file-input">
              {inputMode === 'Pdf' ? 'PDF document' : 'Video file'}{' '}
              <span aria-hidden="true">*</span>
            </Label>

            {!form.watch('sourceFileName') ? (
              <label
                htmlFor="source-file-input"
                className={cn(
                  'flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 cursor-pointer',
                  'hover:border-primary/50 transition-colors',
                  'min-h-[100px]'
                )}
              >
                <Upload className="h-6 w-6 text-muted-foreground mb-2" aria-hidden="true" />
                <span className="text-sm text-muted-foreground">
                  Click to select {inputMode === 'Pdf' ? 'a PDF (max 50MB)' : 'a video file (max 500MB)'}
                </span>
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
                  <span className="truncate text-sm">{form.watch('sourceFileName')}</span>
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

            {form.watch('videoUrl') && (
              <FormField
                control={form.control}
                name="videoRightsConfirmed"
                render={({ field }) => (
                  <FormItem className="flex items-start gap-2">
                    <FormControl>
                      <input
                        type="checkbox"
                        id="video-rights"
                        checked={field.value}
                        onChange={field.onChange}
                        className="mt-0.5 h-4 w-4 rounded border-input"
                        aria-describedby={errors.videoRightsConfirmed ? 'rights-error' : undefined}
                      />
                    </FormControl>
                    <div className="space-y-0.5">
                      <Label htmlFor="video-rights" className="cursor-pointer">
                        I confirm I have the rights to use this video for training purposes
                      </Label>
                      {errors.videoRightsConfirmed && (
                        <p id="rights-error" role="alert" className="text-sm text-destructive">
                          {errors.videoRightsConfirmed.message}
                        </p>
                      )}
                    </div>
                  </FormItem>
                )}
              />
            )}
          </div>
        )}

        {/* ── Target languages ── */}
        <FormField
          control={form.control}
          name="targetLanguageCodes"
          render={({ field }) => (
            <FormItem>
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
              <FormMessage id="lang-error" role="alert" />
            </FormItem>
          )}
        />

        {/* ── Sector ── */}
        {sectorField}

        {/* ── Generation preferences ── */}
        <div className="space-y-4">
          <h3 className="text-sm font-medium">Generation preferences</h3>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="audienceRole"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Audience role</FormLabel>
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
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="passThreshold"
              render={({ field }) => (
                <FormItem>
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
                        {DEFAULT_PASS_THRESHOLDS.map((t) => (
                          <SelectItem key={t} value={String(t)}>
                            {t}%
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <div className="flex flex-col gap-3 sm:flex-row sm:gap-6">
            <FormField
              control={form.control}
              name="includeQuiz"
              render={({ field }) => (
                <FormItem className="flex items-center gap-2">
                  <FormControl>
                    <input
                      type="checkbox"
                      id="include-quiz"
                      checked={field.value}
                      onChange={field.onChange}
                      className="h-4 w-4 rounded border-input"
                    />
                  </FormControl>
                  <Label htmlFor="include-quiz" className="cursor-pointer font-normal">
                    Include quiz
                  </Label>
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="preserveSourceWording"
              render={({ field }) => (
                <FormItem className="flex items-center gap-2">
                  <FormControl>
                    <input
                      type="checkbox"
                      id="preserve-wording"
                      checked={field.value}
                      onChange={field.onChange}
                      className="h-4 w-4 rounded border-input"
                    />
                  </FormControl>
                  <Label htmlFor="preserve-wording" className="cursor-pointer font-normal">
                    Preserve source wording
                  </Label>
                </FormItem>
              )}
            />
          </div>
        </div>

        {/* ── Audit metadata ── */}
        <div className="space-y-4">
          <h3 className="text-sm font-medium">Audit metadata</h3>

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
                  <FormMessage />
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
                  <FormMessage />
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
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="clientName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Client name</FormLabel>
                  <FormControl>
                    <Input {...field} maxLength={200} />
                  </FormControl>
                  <FormMessage />
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
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="auditPurpose"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Audit purpose</FormLabel>
                <FormControl>
                  <Textarea
                    {...field}
                    maxLength={500}
                    placeholder="e.g. Regulatory compliance, Internal learning..."
                    className="resize-none"
                    rows={3}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        {/* ── Form-level error banner ── */}
        {initialiseMutation.isError && (
          <Alert variant="destructive" role="alert">
            <AlertTriangle className="h-4 w-4" aria-hidden="true" />
            <AlertDescription>
              {initialiseMutation.error instanceof Error
                ? initialiseMutation.error.message
                : 'Failed to create learning. Please try again.'}
            </AlertDescription>
          </Alert>
        )}

        {/* ── Continue button ── */}
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
