import type { ToolboxTalkFrequency } from '@/types/toolbox-talks';

export const FREQUENCY_VALUES = ['Once', 'Weekly', 'Monthly', 'Annually'] as const;

export const FREQUENCY_OPTIONS: { value: ToolboxTalkFrequency; label: string }[] = [
  { value: 'Once', label: 'Once' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Annually', label: 'Annually' },
];

export const FREQUENCY_OPTIONS_WITH_DESCRIPTIONS = [
  { value: 'Once', label: 'Once', description: 'One-time training' },
  { value: 'Weekly', label: 'Weekly', description: 'Repeat every week' },
  { value: 'Monthly', label: 'Monthly', description: 'Repeat every month' },
  { value: 'Annually', label: 'Annually', description: 'Repeat every year' },
] as const;
