import { z } from 'zod';

export const sectionSchema = z.object({
  id: z.string().optional(),
  title: z.string().min(1, 'Section title is required').max(200),
  content: z.string().min(1, 'Section content is required'),
  requiresAcknowledgment: z.boolean(),
  source: z.string(),
});

export const parseStepSchema = z.object({
  sections: z.array(sectionSchema).min(1, 'At least one section is required'),
});

export type SectionFormValue = z.infer<typeof sectionSchema>;
export type ParseStepFormValues = z.infer<typeof parseStepSchema>;
