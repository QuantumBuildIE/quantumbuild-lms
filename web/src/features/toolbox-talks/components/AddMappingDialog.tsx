'use client';

import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Loader2 } from 'lucide-react';
import {
  useContentOptions,
  useAddManualMapping,
} from '@/lib/api/admin/use-requirement-mappings';
import { toast } from 'sonner';

interface AddMappingDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  requirementId: string;
  requirementTitle: string;
}

export function AddMappingDialog({
  open,
  onOpenChange,
  requirementId,
  requirementTitle,
}: AddMappingDialogProps) {
  const [selectedContentId, setSelectedContentId] = useState<string>('');
  const { data: options, isLoading: optionsLoading } = useContentOptions();
  const addMutation = useAddManualMapping();

  const courses = options?.filter((o) => o.type === 'Course') ?? [];
  const talks = options?.filter((o) => o.type === 'Talk') ?? [];

  const selectedOption = options?.find((o) => o.id === selectedContentId);

  const handleSubmit = () => {
    if (!selectedOption) return;

    addMutation.mutate(
      {
        regulatoryRequirementId: requirementId,
        toolboxTalkId: selectedOption.type === 'Talk' ? selectedOption.id : null,
        courseId: selectedOption.type === 'Course' ? selectedOption.id : null,
      },
      {
        onSuccess: () => {
          toast.success('Mapping confirmed');
          setSelectedContentId('');
          onOpenChange(false);
        },
        onError: () => {
          toast.error('Failed to create mapping');
        },
      }
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Add Mapping</DialogTitle>
          <DialogDescription>
            Map &ldquo;{requirementTitle}&rdquo; to a talk or course.
          </DialogDescription>
        </DialogHeader>

        <div className="py-4">
          <Select
            value={selectedContentId}
            onValueChange={setSelectedContentId}
            disabled={optionsLoading}
          >
            <SelectTrigger>
              <SelectValue placeholder={optionsLoading ? 'Loading...' : 'Select a talk or course'} />
            </SelectTrigger>
            <SelectContent>
              {courses.length > 0 && (
                <SelectGroup>
                  <SelectLabel>Courses</SelectLabel>
                  {courses.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.title}
                    </SelectItem>
                  ))}
                </SelectGroup>
              )}
              {talks.length > 0 && (
                <SelectGroup>
                  <SelectLabel>Talks</SelectLabel>
                  {talks.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.title}
                    </SelectItem>
                  ))}
                </SelectGroup>
              )}
            </SelectContent>
          </Select>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={addMutation.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={!selectedContentId || addMutation.isPending}
          >
            {addMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Confirm Mapping
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
