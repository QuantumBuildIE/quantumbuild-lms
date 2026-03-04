'use client';

import { useState, useCallback, useRef, useEffect } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  ArrowUp,
  ArrowDown,
  Trash2,
  Check,
  X,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { DragHandle, useSortableItem } from '@/components/ui/sortable';
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import {
  restrictToVerticalAxis,
  restrictToParentElement,
} from '@dnd-kit/modifiers';
import type { ParsedSection } from '@/types/content-creation';

// ============================================
// Props
// ============================================

interface SectionListProps {
  sections: ParsedSection[];
  onChange: (sections: ParsedSection[]) => void;
}

// ============================================
// Main Section List
// ============================================

export function SectionList({ sections, onChange }: SectionListProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [editTitle, setEditTitle] = useState('');

  // Stable IDs: one per section, persisted across reorders
  const idsRef = useRef<string[]>([]);

  // Sync IDs with section count
  if (idsRef.current.length !== sections.length) {
    const existing = idsRef.current;
    const newIds = [...existing];
    // Add IDs for new sections
    while (newIds.length < sections.length) {
      newIds.push(crypto.randomUUID());
    }
    // Trim if sections were removed
    newIds.length = sections.length;
    idsRef.current = newIds;
  }

  const ids = idsRef.current;

  // DnD sensors
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event;
      if (!over || active.id === over.id) return;

      const oldIndex = ids.indexOf(String(active.id));
      const newIndex = ids.indexOf(String(over.id));
      if (oldIndex === -1 || newIndex === -1) return;

      // Reorder both sections and IDs in sync
      const reorderedSections = arrayMove([...sections], oldIndex, newIndex);
      idsRef.current = arrayMove([...ids], oldIndex, newIndex);

      onChange(reorderedSections.map((s, i) => ({ ...s, suggestedOrder: i })));
    },
    [sections, ids, onChange]
  );

  const handleMoveUp = useCallback(
    (index: number) => {
      if (index === 0) return;
      const updated = arrayMove([...sections], index, index - 1);
      idsRef.current = arrayMove([...ids], index, index - 1);
      onChange(updated.map((s, i) => ({ ...s, suggestedOrder: i })));
    },
    [sections, ids, onChange]
  );

  const handleMoveDown = useCallback(
    (index: number) => {
      if (index === sections.length - 1) return;
      const updated = arrayMove([...sections], index, index + 1);
      idsRef.current = arrayMove([...ids], index, index + 1);
      onChange(updated.map((s, i) => ({ ...s, suggestedOrder: i })));
    },
    [sections, ids, onChange]
  );

  const handleDelete = useCallback(
    (index: number) => {
      const updated = sections.filter((_, i) => i !== index);
      idsRef.current = ids.filter((_, i) => i !== index);
      onChange(updated.map((s, i) => ({ ...s, suggestedOrder: i })));
    },
    [sections, ids, onChange]
  );

  const startRename = (index: number) => {
    setEditingIndex(index);
    setEditTitle(sections[index].title);
  };

  const confirmRename = () => {
    if (editingIndex === null) return;
    const trimmed = editTitle.trim();
    if (!trimmed) {
      cancelRename();
      return;
    }
    const updated = sections.map((s, i) =>
      i === editingIndex ? { ...s, title: trimmed } : s
    );
    onChange(updated);
    setEditingIndex(null);
    setEditTitle('');
  };

  const cancelRename = () => {
    setEditingIndex(null);
    setEditTitle('');
  };

  const handleRenameKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      confirmRename();
    } else if (e.key === 'Escape') {
      cancelRename();
    }
  };

  return (
    <div>
      <div className="mb-3 flex items-center justify-between">
        <span className="text-sm font-medium">
          Sections ({sections.length})
        </span>
        <span className="text-xs text-muted-foreground">
          Drag to reorder · Double-click to rename
        </span>
      </div>

      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        onDragEnd={handleDragEnd}
        modifiers={[restrictToVerticalAxis, restrictToParentElement]}
      >
        <SortableContext items={ids} strategy={verticalListSortingStrategy}>
          <div className="space-y-2">
            {sections.map((section, index) => (
              <SectionRow
                key={ids[index]}
                id={ids[index]}
                section={section}
                index={index}
                isFirst={index === 0}
                isLast={index === sections.length - 1}
                isEditing={editingIndex === index}
                editTitle={editTitle}
                onEditTitleChange={setEditTitle}
                onStartRename={() => startRename(index)}
                onConfirmRename={confirmRename}
                onCancelRename={cancelRename}
                onRenameKeyDown={handleRenameKeyDown}
                onMoveUp={() => handleMoveUp(index)}
                onMoveDown={() => handleMoveDown(index)}
                onDelete={() => handleDelete(index)}
              />
            ))}
          </div>
        </SortableContext>
      </DndContext>
    </div>
  );
}

// ============================================
// Section Row (Sortable Item)
// ============================================

interface SectionRowProps {
  id: string;
  section: ParsedSection;
  index: number;
  isFirst: boolean;
  isLast: boolean;
  isEditing: boolean;
  editTitle: string;
  onEditTitleChange: (value: string) => void;
  onStartRename: () => void;
  onConfirmRename: () => void;
  onCancelRename: () => void;
  onRenameKeyDown: (e: React.KeyboardEvent) => void;
  onMoveUp: () => void;
  onMoveDown: () => void;
  onDelete: () => void;
}

function SectionRow({
  id,
  section,
  index,
  isFirst,
  isLast,
  isEditing,
  editTitle,
  onEditTitleChange,
  onStartRename,
  onConfirmRename,
  onCancelRename,
  onRenameKeyDown,
  onMoveUp,
  onMoveDown,
  onDelete,
}: SectionRowProps) {
  const { attributes, listeners, setNodeRef, style, isDragging } =
    useSortableItem({ id });

  const label = `L${String(index + 1).padStart(2, '0')}`;

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'flex items-center gap-3 rounded-lg border bg-card px-3 py-2.5',
        isDragging && 'bg-muted/50 shadow-md'
      )}
    >
      {/* Drag handle — always visible */}
      <DragHandle {...listeners} {...attributes} />

      {/* Badge */}
      <Badge variant="secondary" className="shrink-0 font-mono text-xs">
        {label}
      </Badge>

      {/* Title — inline edit on double-click */}
      {isEditing ? (
        <div className="flex flex-1 items-center gap-1">
          <Input
            value={editTitle}
            onChange={(e) => onEditTitleChange(e.target.value)}
            onKeyDown={onRenameKeyDown}
            autoFocus
            className="h-7 text-sm"
          />
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onConfirmRename}>
            <Check className="h-3.5 w-3.5" />
          </Button>
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onCancelRename}>
            <X className="h-3.5 w-3.5" />
          </Button>
        </div>
      ) : (
        <span
          className="flex-1 cursor-default truncate text-sm"
          onDoubleClick={onStartRename}
          title={section.title}
        >
          {section.title}
        </span>
      )}

      {/* Actions — always visible */}
      <div className="flex shrink-0 items-center gap-0.5">
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7"
          onClick={onMoveUp}
          disabled={isFirst}
          title="Move up"
        >
          <ArrowUp className="h-3.5 w-3.5" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7"
          onClick={onMoveDown}
          disabled={isLast}
          title="Move down"
        >
          <ArrowDown className="h-3.5 w-3.5" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7 text-destructive hover:text-destructive"
          onClick={onDelete}
          title="Delete section"
        >
          <Trash2 className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}
