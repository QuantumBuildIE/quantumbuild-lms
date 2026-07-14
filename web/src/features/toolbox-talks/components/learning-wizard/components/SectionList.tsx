'use client';

import { useState, useCallback, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Trash2, ChevronDown, ChevronRight, Plus } from 'lucide-react';
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
import type { ToolboxTalkSection } from '@/types/toolbox-talks';

// ============================================
// Types
// ============================================

export interface SectionDraft {
  /** undefined for new (unsaved) sections, existing id for DB sections */
  id?: string;
  title: string;
  content: string;
  requiresAcknowledgment: boolean;
  source: string;
}

interface SectionListProps {
  sections: SectionDraft[];
  onChange: (sections: SectionDraft[]) => void;
  disabled?: boolean;
}

// ============================================
// Helpers
// ============================================

export function toSectionDrafts(sections: ToolboxTalkSection[]): SectionDraft[] {
  return sections.map((s) => ({
    id: s.id,
    title: s.title,
    content: s.content,
    requiresAcknowledgment: s.requiresAcknowledgment,
    source: s.source ?? 'Manual',
  }));
}

// ============================================
// Single section card
// ============================================

interface SectionCardProps {
  section: SectionDraft;
  index: number;
  dndId: string;
  isExpanded: boolean;
  onToggleExpand: () => void;
  onTitleChange: (title: string) => void;
  onContentChange: (content: string) => void;
  onDelete: () => void;
  disabled?: boolean;
}

function SectionCard({
  section,
  index,
  dndId,
  isExpanded,
  onToggleExpand,
  onTitleChange,
  onContentChange,
  onDelete,
  disabled,
}: SectionCardProps) {
  const { attributes, listeners, setNodeRef, style, isDragging } = useSortableItem({
    id: dndId,
    disabled,
  });

  const [editingTitle, setEditingTitle] = useState(false);
  const titleInputRef = useRef<HTMLInputElement>(null);

  const handleTitleClick = useCallback(() => {
    if (disabled) return;
    setEditingTitle(true);
    setTimeout(() => titleInputRef.current?.focus(), 0);
  }, [disabled]);

  const handleTitleBlur = useCallback(() => setEditingTitle(false), []);

  const handleTitleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === 'Escape') {
      setEditingTitle(false);
    }
  }, []);

  // Strip HTML tags for plain-text preview
  const plainPreview = section.content.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim();

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'rounded-lg border bg-card transition-shadow',
        isDragging && 'shadow-lg ring-1 ring-primary/20'
      )}
      aria-label={`Section ${index + 1}: ${section.title}`}
    >
      {/* Header row */}
      <div className="flex items-center gap-2 px-3 py-2">
        {/* Drag handle */}
        {!disabled && (
          <DragHandle
            {...listeners}
            {...attributes}
            aria-label={`Drag section ${index + 1}`}
          />
        )}

        {/* Section number */}
        <Badge variant="secondary" className="shrink-0 tabular-nums min-w-[2rem] justify-center">
          {index + 1}
        </Badge>

        {/* Title — click to edit */}
        <div className="flex-1 min-w-0">
          {editingTitle ? (
            <Input
              ref={titleInputRef}
              value={section.title}
              onChange={(e) => onTitleChange(e.target.value)}
              onBlur={handleTitleBlur}
              onKeyDown={handleTitleKeyDown}
              className="h-7 text-sm font-medium"
              aria-label={`Section ${index + 1} title`}
            />
          ) : (
            <button
              type="button"
              onClick={handleTitleClick}
              disabled={disabled}
              className={cn(
                'block w-full truncate text-left text-sm font-medium',
                'rounded px-1 py-0.5',
                !disabled && 'hover:bg-muted/50 cursor-text'
              )}
              aria-label={`Edit section ${index + 1} title: ${section.title}`}
            >
              {section.title || <span className="text-muted-foreground italic">Untitled section</span>}
            </button>
          )}
        </div>

        {/* Expand / collapse */}
        <button
          type="button"
          onClick={onToggleExpand}
          className="shrink-0 rounded p-1 text-muted-foreground hover:text-foreground"
          aria-expanded={isExpanded}
          aria-label={isExpanded ? 'Collapse section' : 'Expand section'}
        >
          {isExpanded ? (
            <ChevronDown className="h-4 w-4" aria-hidden="true" />
          ) : (
            <ChevronRight className="h-4 w-4" aria-hidden="true" />
          )}
        </button>

        {/* Delete */}
        {!disabled && (
          <button
            type="button"
            onClick={onDelete}
            className="shrink-0 rounded p-1 text-muted-foreground hover:text-destructive"
            aria-label={`Delete section ${index + 1}`}
          >
            <Trash2 className="h-4 w-4" aria-hidden="true" />
          </button>
        )}
      </div>

      {/* Expanded body */}
      {isExpanded && (
        <div className="border-t px-3 py-3">
          {disabled ? (
            <p className="text-sm text-muted-foreground whitespace-pre-wrap">
              {plainPreview || <em>No content</em>}
            </p>
          ) : (
            <Textarea
              value={section.content}
              onChange={(e) => onContentChange(e.target.value)}
              className="min-h-[120px] resize-y text-sm font-mono"
              aria-label={`Section ${index + 1} content`}
              placeholder="Enter section content (HTML is accepted)…"
            />
          )}
        </div>
      )}
    </div>
  );
}

// ============================================
// SectionList
// ============================================

export function SectionList({ sections, onChange, disabled }: SectionListProps) {
  const [expandedIndexes, setExpandedIndexes] = useState<Set<number>>(new Set());

  // Stable DnD IDs: one per slot, keyed to the section's id or a stable random fallback
  const dndIdsRef = useRef<string[]>([]);
  if (dndIdsRef.current.length !== sections.length) {
    const updated = [...dndIdsRef.current];
    while (updated.length < sections.length) {
      updated.push(crypto.randomUUID());
    }
    updated.length = sections.length;
    dndIdsRef.current = updated;
  }
  const dndIds = dndIdsRef.current;

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event;
      if (!over || active.id === over.id) return;

      const oldIndex = dndIds.indexOf(active.id as string);
      const newIndex = dndIds.indexOf(over.id as string);
      if (oldIndex === -1 || newIndex === -1) return;

      const reorderedIds = arrayMove(dndIds, oldIndex, newIndex);
      dndIdsRef.current = reorderedIds;

      const reorderedSections = arrayMove(sections, oldIndex, newIndex);
      onChange(reorderedSections);

      // Remap expanded indexes
      setExpandedIndexes((prev) => {
        const next = new Set<number>();
        prev.forEach((i) => {
          if (i === oldIndex) next.add(newIndex);
          else if (oldIndex < newIndex && i > oldIndex && i <= newIndex) next.add(i - 1);
          else if (oldIndex > newIndex && i < oldIndex && i >= newIndex) next.add(i + 1);
          else next.add(i);
        });
        return next;
      });
    },
    [sections, dndIds, onChange]
  );

  const toggleExpand = useCallback((index: number) => {
    setExpandedIndexes((prev) => {
      const next = new Set(prev);
      if (next.has(index)) next.delete(index);
      else next.add(index);
      return next;
    });
  }, []);

  const handleTitleChange = useCallback(
    (index: number, title: string) => {
      const updated = [...sections];
      updated[index] = { ...updated[index], title };
      onChange(updated);
    },
    [sections, onChange]
  );

  const handleContentChange = useCallback(
    (index: number, content: string) => {
      const updated = [...sections];
      updated[index] = { ...updated[index], content };
      onChange(updated);
    },
    [sections, onChange]
  );

  const handleDelete = useCallback(
    (index: number) => {
      const updated = sections.filter((_, i) => i !== index);
      dndIdsRef.current = dndIds.filter((_, i) => i !== index);
      onChange(updated);
      setExpandedIndexes((prev) => {
        const next = new Set<number>();
        prev.forEach((i) => {
          if (i < index) next.add(i);
          else if (i > index) next.add(i - 1);
          // i === index is dropped
        });
        return next;
      });
    },
    [sections, dndIds, onChange]
  );

  const handleAddSection = useCallback(() => {
    const newSection: SectionDraft = {
      title: '',
      content: '',
      requiresAcknowledgment: true,
      source: 'Manual',
    };
    const newIndex = sections.length;
    dndIdsRef.current = [...dndIds, crypto.randomUUID()];
    onChange([...sections, newSection]);
    // Auto-expand new section
    setExpandedIndexes((prev) => new Set([...prev, newIndex]));
  }, [sections, dndIds, onChange]);

  if (sections.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        <p className="text-sm">No sections yet.</p>
        {!disabled && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={handleAddSection}
            className="mt-3"
          >
            <Plus className="mr-2 h-4 w-4" aria-hidden="true" />
            Add first section
          </Button>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        onDragEnd={handleDragEnd}
        modifiers={[restrictToVerticalAxis, restrictToParentElement]}
      >
        <SortableContext items={dndIds} strategy={verticalListSortingStrategy}>
          {sections.map((section, index) => (
            <SectionCard
              key={dndIds[index]}
              section={section}
              index={index}
              dndId={dndIds[index]}
              isExpanded={expandedIndexes.has(index)}
              onToggleExpand={() => toggleExpand(index)}
              onTitleChange={(title) => handleTitleChange(index, title)}
              onContentChange={(content) => handleContentChange(index, content)}
              onDelete={() => handleDelete(index)}
              disabled={disabled}
            />
          ))}
        </SortableContext>
      </DndContext>

      {!disabled && (
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={handleAddSection}
          className="w-full"
        >
          <Plus className="mr-2 h-4 w-4" aria-hidden="true" />
          Add section
        </Button>
      )}
    </div>
  );
}
