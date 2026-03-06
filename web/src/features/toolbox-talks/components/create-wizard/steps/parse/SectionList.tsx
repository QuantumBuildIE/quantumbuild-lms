'use client';

import { useState, useCallback, useRef } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Trash2,
  ChevronDown,
  ChevronRight,
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
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  // Stable IDs: one per section, persisted across reorders
  const idsRef = useRef<string[]>([]);

  // Sync IDs with section count
  if (idsRef.current.length !== sections.length) {
    const existing = idsRef.current;
    const newIds = [...existing];
    while (newIds.length < sections.length) {
      newIds.push(crypto.randomUUID());
    }
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

      const reorderedSections = arrayMove([...sections], oldIndex, newIndex);
      idsRef.current = arrayMove([...ids], oldIndex, newIndex);

      onChange(reorderedSections.map((s, i) => ({ ...s, suggestedOrder: i })));
    },
    [sections, ids, onChange]
  );

  const handleDelete = useCallback(
    (index: number) => {
      const deletedId = ids[index];
      const updated = sections.filter((_, i) => i !== index);
      idsRef.current = ids.filter((_, i) => i !== index);
      setExpandedIds((prev) => {
        const next = new Set(prev);
        next.delete(deletedId);
        return next;
      });
      onChange(updated.map((s, i) => ({ ...s, suggestedOrder: i })));
    },
    [sections, ids, onChange]
  );

  const toggleExpanded = useCallback((id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

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
                isEditing={editingIndex === index}
                isExpanded={expandedIds.has(ids[index])}
                editTitle={editTitle}
                onEditTitleChange={setEditTitle}
                onStartRename={() => startRename(index)}
                onConfirmRename={confirmRename}
                onRenameKeyDown={handleRenameKeyDown}
                onToggleExpanded={() => toggleExpanded(ids[index])}
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
  isEditing: boolean;
  isExpanded: boolean;
  editTitle: string;
  onEditTitleChange: (value: string) => void;
  onStartRename: () => void;
  onConfirmRename: () => void;
  onRenameKeyDown: (e: React.KeyboardEvent) => void;
  onToggleExpanded: () => void;
  onDelete: () => void;
}

function SectionRow({
  id,
  section,
  index,
  isEditing,
  isExpanded,
  editTitle,
  onEditTitleChange,
  onStartRename,
  onConfirmRename,
  onRenameKeyDown,
  onToggleExpanded,
  onDelete,
}: SectionRowProps) {
  const { attributes, listeners, setNodeRef, style, isDragging } =
    useSortableItem({ id });

  // Manual double-click detection — immune to dnd-kit pointer event interference
  const clickTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const handleTitlePointerUp = useCallback(
    (e: React.PointerEvent) => {
      if (e.button !== 0) return; // left-click only
      if (clickTimer.current) {
        clearTimeout(clickTimer.current);
        clickTimer.current = null;
        onStartRename();
      } else {
        clickTimer.current = setTimeout(() => {
          clickTimer.current = null;
        }, 300);
      }
    },
    [onStartRename]
  );

  const label = `L${String(index + 1).padStart(2, '0')}`;

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'rounded-lg border bg-card',
        isDragging && 'bg-muted/50 shadow-md'
      )}
    >
      {/* Row header */}
      <div className="flex items-center gap-3 px-3 py-2.5">
        {/* Drag handle */}
        <DragHandle {...listeners} {...attributes} />

        {/* Expand/collapse toggle */}
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7 shrink-0"
          onClick={onToggleExpanded}
          title={isExpanded ? 'Collapse' : 'Expand'}
        >
          {isExpanded ? (
            <ChevronDown className="h-3.5 w-3.5" />
          ) : (
            <ChevronRight className="h-3.5 w-3.5" />
          )}
        </Button>

        {/* Badge */}
        <Badge variant="secondary" className="shrink-0 font-mono text-xs">
          {label}
        </Badge>

        {/* Title — inline edit on double-click */}
        {isEditing ? (
          <Input
            value={editTitle}
            onChange={(e) => onEditTitleChange(e.target.value)}
            onKeyDown={onRenameKeyDown}
            onBlur={onConfirmRename}
            autoFocus
            className="h-7 flex-1 text-sm"
          />
        ) : (
          <span
            className="flex-1 cursor-default truncate text-sm"
            onPointerUp={handleTitlePointerUp}
            title={section.title}
          >
            {section.title}
          </span>
        )}

        {/* Delete action */}
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7 shrink-0 text-destructive hover:text-destructive"
          onClick={onDelete}
          title="Delete section"
        >
          <Trash2 className="h-3.5 w-3.5" />
        </Button>
      </div>

      {/* Expanded content */}
      {isExpanded && (
        <div className="border-t px-3 py-3 pl-[4.5rem]">
          <div
            className="prose prose-sm dark:prose-invert max-w-none text-sm text-muted-foreground"
            dangerouslySetInnerHTML={{ __html: section.content }}
          />
        </div>
      )}
    </div>
  );
}
