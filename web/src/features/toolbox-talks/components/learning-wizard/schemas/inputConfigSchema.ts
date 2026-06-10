import { z } from 'zod';

export const INPUT_MODES = ['Text', 'Pdf', 'Video'] as const;
export type InputMode = (typeof INPUT_MODES)[number];

export const AUDIENCE_ROLES = ['Operator', 'Supervisor', 'Auditor'] as const;
export type AudienceRole = (typeof AUDIENCE_ROLES)[number];

export const inputConfigSchema = z
  .object({
    // Basic
    title: z.string().min(1, 'Title is required').max(200, 'Title must be 200 characters or fewer'),

    // Source input
    inputMode: z.enum(INPUT_MODES),
    sourceText: z.string().optional(),
    sourceFileUrl: z.string().optional(),
    sourceFileName: z.string().optional(),
    sourceFileType: z.string().optional(),

    // Video URL mode
    videoUrl: z.string().optional(),
    videoRightsConfirmed: z.boolean(),

    // Languages
    targetLanguageCodes: z
      .array(z.string())
      .min(1, 'At least one target language is required'),

    // Quiz / settings
    passThreshold: z
      .number()
      .int()
      .min(50, 'Threshold must be at least 50')
      .max(100, 'Threshold must be at most 100'),
    includeQuiz: z.boolean(),

    // Generation preferences
    audienceRole: z.enum(AUDIENCE_ROLES),
    preserveSourceWording: z.boolean(),

    // Sector
    sectorKey: z.string().optional(),

    // Audit metadata
    reviewerName: z.string().max(200).optional(),
    reviewerOrg: z.string().max(200).optional(),
    reviewerRole: z.string().max(200).optional(),
    documentRef: z.string().max(100).optional(),
    clientName: z.string().max(200).optional(),
    auditPurpose: z.string().max(500).optional(),
  })
  .superRefine((data, ctx) => {
    if (data.inputMode === 'Text' && !data.sourceFileUrl && !data.sourceText?.trim()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Please enter source text or upload a file',
        path: ['sourceText'],
      });
    }
    if (data.inputMode === 'Pdf' && !data.sourceFileUrl) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Please upload a PDF file',
        path: ['sourceFileUrl'],
      });
    }
    if (data.inputMode === 'Video' && !data.videoUrl && !data.sourceFileUrl) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Please enter a video URL or upload a video file',
        path: ['videoUrl'],
      });
    }
    if (data.videoUrl && !data.videoRightsConfirmed) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Please confirm you have rights to use this video',
        path: ['videoRightsConfirmed'],
      });
    }
  });

export type InputConfigValues = z.infer<typeof inputConfigSchema>;
