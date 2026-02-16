"use client";

import * as React from "react";
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
} from "@dnd-kit/core";
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import {
  restrictToVerticalAxis,
  restrictToParentElement,
} from "@dnd-kit/modifiers";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical } from "lucide-react";
import { cn } from "@/lib/utils";

// Drag Handle Component
interface DragHandleProps extends React.HTMLAttributes<HTMLButtonElement> {
  disabled?: boolean;
}

const DragHandle = React.forwardRef<HTMLButtonElement, DragHandleProps>(
  ({ className, disabled, ...props }, ref) => {
    return (
      <button
        ref={ref}
        type="button"
        className={cn(
          "touch-none cursor-grab p-1 text-muted-foreground hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50",
          disabled && "invisible",
          className
        )}
        disabled={disabled}
        {...props}
      >
        <GripVertical className="h-4 w-4" />
      </button>
    );
  }
);
DragHandle.displayName = "DragHandle";

// Sortable Item Hook wrapper
interface UseSortableItemOptions {
  id: string;
  disabled?: boolean;
}

function useSortableItem({ id, disabled = false }: UseSortableItemOptions) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id, disabled });

  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
    position: "relative" as const,
    zIndex: isDragging ? 1 : 0,
  };

  return {
    attributes,
    listeners,
    setNodeRef,
    style,
    isDragging,
  };
}

// Sortable Context Provider
interface SortableListProps<T> {
  items: T[];
  keyExtractor: (item: T) => string;
  onReorder: (items: T[]) => void;
  children: React.ReactNode;
  disabled?: boolean;
}

function SortableList<T>({
  items,
  keyExtractor,
  onReorder,
  children,
  disabled = false,
}: SortableListProps<T>) {
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8,
      },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;

    if (over && active.id !== over.id) {
      const oldIndex = items.findIndex(
        (item) => keyExtractor(item) === active.id
      );
      const newIndex = items.findIndex(
        (item) => keyExtractor(item) === over.id
      );
      const reorderedItems = arrayMove(items, oldIndex, newIndex);
      onReorder(reorderedItems);
    }
  };

  if (disabled) {
    return <>{children}</>;
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragEnd={handleDragEnd}
      modifiers={[restrictToVerticalAxis, restrictToParentElement]}
    >
      <SortableContext
        items={items.map(keyExtractor)}
        strategy={verticalListSortingStrategy}
      >
        {children}
      </SortableContext>
    </DndContext>
  );
}

// Sortable Table Row
interface SortableTableRowProps extends React.HTMLAttributes<HTMLTableRowElement> {
  id: string;
  disabled?: boolean;
  children: React.ReactNode;
}

const SortableTableRow = React.forwardRef<HTMLTableRowElement, SortableTableRowProps>(
  ({ id, disabled = false, className, children, ...props }, ref) => {
    const { attributes, listeners, setNodeRef, style, isDragging } =
      useSortableItem({ id, disabled });

    return (
      <tr
        ref={setNodeRef}
        style={style}
        className={cn(
          isDragging && "bg-muted",
          className
        )}
        {...props}
      >
        {React.Children.map(children, (child, index) => {
          if (index === 0 && React.isValidElement(child)) {
            // Clone the first cell to inject the drag handle
            return React.cloneElement(child as React.ReactElement<any>, {
              children: (
                <div className="flex items-center gap-1">
                  {!disabled && (
                    <DragHandle {...listeners} {...attributes} />
                  )}
                  {(child as React.ReactElement<any>).props.children}
                </div>
              ),
            });
          }
          return child;
        })}
      </tr>
    );
  }
);
SortableTableRow.displayName = "SortableTableRow";

// Sortable List Item (div-based)
interface SortableListItemProps extends React.HTMLAttributes<HTMLDivElement> {
  id: string;
  disabled?: boolean;
  showHandle?: boolean;
  children: React.ReactNode;
}

const SortableListItem = React.forwardRef<HTMLDivElement, SortableListItemProps>(
  ({ id, disabled = false, showHandle = true, className, children, ...props }, ref) => {
    const { attributes, listeners, setNodeRef, style, isDragging } =
      useSortableItem({ id, disabled });

    return (
      <div
        ref={setNodeRef}
        style={style}
        className={cn(
          isDragging && "bg-muted/50 shadow-sm",
          className
        )}
        {...props}
      >
        {showHandle && !disabled && (
          <DragHandle
            {...listeners}
            {...attributes}
            className="absolute left-2 top-1/2 -translate-y-1/2"
          />
        )}
        {children}
      </div>
    );
  }
);
SortableListItem.displayName = "SortableListItem";

export {
  DragHandle,
  SortableList,
  SortableTableRow,
  SortableListItem,
  useSortableItem,
};
