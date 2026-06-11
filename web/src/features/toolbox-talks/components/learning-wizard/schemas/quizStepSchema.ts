import { z } from 'zod';
import { questionSchema } from './questionSchema';

export const quizStepSchema = z.object({
  questions: z.array(questionSchema).min(1, 'At least one question is required'),
});

export type QuizStepFormValues = z.infer<typeof quizStepSchema>;
