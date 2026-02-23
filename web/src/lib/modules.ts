export const MODULE_NAMES = {
  LEARNINGS: 'Learnings',
  LESSON_PARSER: 'LessonParser',
} as const;

export const MODULE_CONFIG = {
  Learnings: {
    label: 'Learnings',
    description: 'Safety training, toolbox talks, and compliance tracking',
    href: '/toolbox-talks',
    icon: 'BookOpen',
  },
  LessonParser: {
    label: 'Lesson Parser',
    description: 'Convert existing training documents into Learnings automatically',
    href: '/lesson-parser',
    icon: 'FileSearch',
  },
} as const;

export type ModuleName = (typeof MODULE_NAMES)[keyof typeof MODULE_NAMES];
