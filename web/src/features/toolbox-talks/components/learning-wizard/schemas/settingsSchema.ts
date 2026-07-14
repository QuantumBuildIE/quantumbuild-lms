import { z } from 'zod';

export const REFRESHER_FREQUENCIES = ['Once', 'Monthly', 'Quarterly', 'Annually'] as const;
export type RefresherFrequency = (typeof REFRESHER_FREQUENCIES)[number];

export const settingsSchema = z.object({
  title: z
    .string()
    .min(1, 'Title is required.')
    .max(200, 'Title must not exceed 200 characters.'),
  description: z
    .string()
    .max(2000, 'Description must not exceed 2000 characters.')
    .nullable()
    .optional(),
  category: z.string().nullable().optional(),
  refresherFrequency: z.enum(REFRESHER_FREQUENCIES),
  isActiveOnPublish: z.boolean(),
  generateCertificate: z.boolean(),
  minimumWatchPercent: z.number().int().min(50).max(100),
  autoAssign: z.boolean(),
  autoAssignDueDays: z.number().int().min(1).max(90),
  generateSlideshow: z.boolean(),
});

export type SettingsFormData = z.infer<typeof settingsSchema>;
