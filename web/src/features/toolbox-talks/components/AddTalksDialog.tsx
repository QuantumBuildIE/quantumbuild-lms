'use client';

import { useState, useMemo } from 'react';
import { SearchIcon } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { useToolboxTalks } from '@/lib/api/toolbox-talks';
import type { ToolboxTalkListItem } from '@/types/toolbox-talks';

interface AddTalksDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  excludeTalkIds: string[];
  onAdd: (talks: ToolboxTalkListItem[]) => void;
}

export function AddTalksDialog({ open, onOpenChange, excludeTalkIds, onAdd }: AddTalksDialogProps) {
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const { data, isLoading } = useToolboxTalks({
    isActive: true,
    pageSize: 100,
  });

  const availableTalks = useMemo(() => {
    if (!data?.items) return [];
    return data.items.filter((talk) => !excludeTalkIds.includes(talk.id));
  }, [data?.items, excludeTalkIds]);

  const filteredTalks = useMemo(() => {
    if (!search.trim()) return availableTalks;
    const term = search.toLowerCase();
    return availableTalks.filter(
      (talk) =>
        talk.title.toLowerCase().includes(term) ||
        talk.description?.toLowerCase().includes(term)
    );
  }, [availableTalks, search]);

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleAdd = () => {
    const selectedTalks = availableTalks.filter((talk) => selectedIds.has(talk.id));
    onAdd(selectedTalks);
    setSelectedIds(new Set());
    setSearch('');
    onOpenChange(false);
  };

  const handleOpenChange = (open: boolean) => {
    if (!open) {
      setSelectedIds(new Set());
      setSearch('');
    }
    onOpenChange(open);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Add Learnings to Course</DialogTitle>
          <DialogDescription>
            Select learnings to add to this course. Only active learnings are shown.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="relative">
            <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search learnings..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>

          {selectedIds.size > 0 && (
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">{selectedIds.size} selected</span>
              <Button variant="ghost" size="sm" onClick={() => setSelectedIds(new Set())}>
                Clear
              </Button>
            </div>
          )}

          <div className="max-h-[300px] overflow-y-auto space-y-1 rounded-md border p-2">
            {isLoading ? (
              <div className="py-8 text-center text-sm text-muted-foreground">Loading talks...</div>
            ) : filteredTalks.length === 0 ? (
              <div className="py-8 text-center text-sm text-muted-foreground">
                {availableTalks.length === 0
                  ? 'All active talks have been added to this course.'
                  : 'No talks match your search.'}
              </div>
            ) : (
              filteredTalks.map((talk) => (
                <label
                  key={talk.id}
                  className="flex items-start gap-3 rounded-md p-2 hover:bg-muted/50 cursor-pointer"
                >
                  <Checkbox
                    checked={selectedIds.has(talk.id)}
                    onCheckedChange={() => toggleSelect(talk.id)}
                    className="mt-0.5"
                  />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-sm truncate">{talk.title}</span>
                      {talk.status === 'Published' && (
                        <Badge variant="outline" className="text-xs shrink-0">Published</Badge>
                      )}
                    </div>
                    {talk.description && (
                      <p className="text-xs text-muted-foreground line-clamp-1 mt-0.5">
                        {talk.description}
                      </p>
                    )}
                    <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
                      {talk.sectionCount > 0 && (
                        <span>{talk.sectionCount} sections</span>
                      )}
                      {talk.questionCount > 0 && (
                        <span>{talk.questionCount} questions</span>
                      )}
                    </div>
                  </div>
                </label>
              ))
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleAdd} disabled={selectedIds.size === 0}>
            Add {selectedIds.size > 0 ? `${selectedIds.size} Learning${selectedIds.size > 1 ? 's' : ''}` : 'Learnings'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
