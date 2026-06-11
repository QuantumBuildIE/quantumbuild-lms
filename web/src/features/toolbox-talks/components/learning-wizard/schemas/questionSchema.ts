import { z } from 'zod';

export const questionSchema = z.object({
  id: z.string().optional(),
  questionNumber: z.number().int().min(1),
  questionText: z.string().min(1, 'Question text is required'),
  questionType: z.enum(['MultipleChoice', 'TrueFalse', 'ShortAnswer']),
  options: z.array(z.string().min(1, 'Option cannot be empty')).nullable().optional(),
  correctOptionIndex: z.number().int().min(0).nullable().optional(),
  correctAnswer: z.string().nullable().optional(),
  points: z.number().int().min(1),
  source: z.string(),
  isFromVideoFinalPortion: z.boolean(),
  videoTimestamp: z.string().nullable().optional(),
});

export type QuestionFormData = z.infer<typeof questionSchema>;
