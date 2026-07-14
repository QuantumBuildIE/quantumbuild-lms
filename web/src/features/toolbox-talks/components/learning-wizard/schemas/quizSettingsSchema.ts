import { z } from 'zod';

export const quizSettingsSchema = z.object({
  requiresQuiz: z.boolean(),
  passingScore: z.number().int().min(1).max(100),
  quizQuestionCount: z.number().int().min(1).nullable().optional(),
  shuffleQuestions: z.boolean(),
  shuffleOptions: z.boolean(),
  useQuestionPool: z.boolean(),
  allowRetry: z.boolean(),
});

export type QuizSettingsFormData = z.infer<typeof quizSettingsSchema>;
