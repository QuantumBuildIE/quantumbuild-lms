'use client';

import { useState } from 'react';
import { PlusIcon } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { useLookupValues } from '@/hooks/use-lookups';
import { usePermission } from '@/lib/auth/use-auth';
import { useAddTargetLanguage } from '@/lib/api/toolbox-talks';

interface AddTargetLanguagePickerProps {
  talkId: string;
  existingLanguages: string[];
}

export function AddTargetLanguagePicker({ talkId, existingLanguages }: AddTargetLanguagePickerProps) {
  const canManage = usePermission('Learnings.Manage');
  const [selectedCode, setSelectedCode] = useState<string>('');
  const { data: languages = [], isLoading: languagesLoading } = useLookupValues('Language');
  const mutation = useAddTargetLanguage();

  if (!canManage) return null;

  // Filter out languages already configured for this talk
  const availableLanguages = languages.filter(
    (lang) => !existingLanguages.some(
      (existing) => existing.toLowerCase() === lang.code.toLowerCase()
    )
  );

  const handleAdd = async () => {
    if (!selectedCode) return;

    try {
      await mutation.mutateAsync({ talkId, languageCode: selectedCode });
      setSelectedCode('');
      const langName = languages.find((l) => l.code === selectedCode)?.name ?? selectedCode;
      toast.success(`${langName} added as a target language`);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to add language';
      toast.error('Could not add language', { description: message });
    }
  };

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="add-language-picker" className="text-sm font-medium">
          Add target language
        </Label>
        <Select
          value={selectedCode}
          onValueChange={setSelectedCode}
          disabled={mutation.isPending || languagesLoading}
        >
          <SelectTrigger id="add-language-picker" className="w-[220px]">
            <SelectValue placeholder="Select a language…" />
          </SelectTrigger>
          <SelectContent>
            {availableLanguages.length === 0 ? (
              <div className="px-3 py-2 text-sm text-muted-foreground">
                No additional languages available
              </div>
            ) : (
              availableLanguages
                .sort((a, b) => a.name.localeCompare(b.name))
                .map((lang) => (
                  <SelectItem key={lang.code} value={lang.code}>
                    {lang.name}
                  </SelectItem>
                ))
            )}
          </SelectContent>
        </Select>
      </div>

      <Button
        onClick={handleAdd}
        disabled={!selectedCode || mutation.isPending}
        size="sm"
      >
        <PlusIcon className="mr-1.5 h-4 w-4" />
        {mutation.isPending ? 'Adding…' : 'Add'}
      </Button>
    </div>
  );
}
