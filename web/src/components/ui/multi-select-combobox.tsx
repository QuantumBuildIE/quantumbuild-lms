"use client";

import * as React from "react";
import { X, Check, ChevronsUpDown, Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";

export interface MultiSelectOption {
  value: string;
  label: string;
  description?: string;
  metadata?: Record<string, unknown>;
}

export interface MultiSelectGroup {
  label: string;
  options: MultiSelectOption[];
}

interface MultiSelectComboboxProps {
  options: MultiSelectOption[];
  groups?: MultiSelectGroup[];
  selectedValues: string[];
  onValuesChange: (values: string[], options: MultiSelectOption[]) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  disabled?: boolean;
  isLoading?: boolean;
  maxDisplayItems?: number;
  className?: string;
  renderOption?: (option: MultiSelectOption) => React.ReactNode;
  showSelectAll?: boolean;
  renderTriggerContent?: (selectedOptions: MultiSelectOption[]) => React.ReactNode;
}

export function MultiSelectCombobox({
  options,
  groups,
  selectedValues,
  onValuesChange,
  placeholder = "Select items...",
  searchPlaceholder = "Search...",
  emptyText = "No results found.",
  disabled = false,
  isLoading = false,
  maxDisplayItems = 3,
  className,
  renderOption,
  showSelectAll = false,
  renderTriggerContent,
}: MultiSelectComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const [inputValue, setInputValue] = React.useState("");

  const selectedOptions = React.useMemo(
    () => options.filter((option) => selectedValues.includes(option.value)),
    [options, selectedValues]
  );

  const filteredOptions = React.useMemo(() => {
    if (!inputValue) return options;
    const lowerInput = inputValue.toLowerCase();
    return options.filter(
      (option) =>
        option.label.toLowerCase().includes(lowerInput) ||
        option.description?.toLowerCase().includes(lowerInput)
    );
  }, [options, inputValue]);

  const filteredValueSet = React.useMemo(
    () => new Set(filteredOptions.map((o) => o.value)),
    [filteredOptions]
  );

  const handleSelect = (selectedValue: string) => {
    const isSelected = selectedValues.includes(selectedValue);
    let newValues: string[];

    if (isSelected) {
      newValues = selectedValues.filter((v) => v !== selectedValue);
    } else {
      newValues = [...selectedValues, selectedValue];
    }

    const newOptions = options.filter((o) => newValues.includes(o.value));
    onValuesChange(newValues, newOptions);
  };

  const handleRemove = (valueToRemove: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const newValues = selectedValues.filter((v) => v !== valueToRemove);
    const newOptions = options.filter((o) => newValues.includes(o.value));
    onValuesChange(newValues, newOptions);
  };

  const handleClearAll = (e: React.MouseEvent) => {
    e.stopPropagation();
    onValuesChange([], []);
  };

  const handleSelectAll = (e: React.MouseEvent) => {
    e.stopPropagation();
    const allValues = options.map((o) => o.value);
    onValuesChange(allValues, [...options]);
  };

  const displayedOptions = selectedOptions.slice(0, maxDisplayItems);
  const remainingCount = selectedOptions.length - maxDisplayItems;

  const renderItem = (option: MultiSelectOption) => {
    const isSelected = selectedValues.includes(option.value);
    return (
      <CommandItem
        key={option.value}
        value={option.value}
        onSelect={() => handleSelect(option.value)}
        className="cursor-pointer"
      >
        <div
          className={cn(
            "mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary",
            isSelected
              ? "bg-primary text-primary-foreground"
              : "opacity-50 [&_svg]:invisible"
          )}
        >
          <Check className="h-3 w-3" />
        </div>
        <div className="flex-1 min-w-0">
          {renderOption ? (
            renderOption(option)
          ) : (
            <>
              <div className="font-medium truncate">{option.label}</div>
              {option.description && (
                <div className="text-xs text-muted-foreground truncate">
                  {option.description}
                </div>
              )}
            </>
          )}
        </div>
      </CommandItem>
    );
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className={cn(
            "w-full min-h-[2.5rem] h-auto justify-between font-normal",
            className
          )}
        >
          <div className="flex flex-wrap gap-1 flex-1">
            {selectedOptions.length === 0 ? (
              <span className="text-muted-foreground">{placeholder}</span>
            ) : renderTriggerContent ? (
              renderTriggerContent(selectedOptions)
            ) : (
              <>
                {displayedOptions.map((option) => (
                  <Badge
                    key={option.value}
                    variant="secondary"
                    className="mr-1 mb-0.5"
                  >
                    <span className="truncate max-w-[150px]">{option.label}</span>
                    <span
                      role="button"
                      tabIndex={0}
                      className="ml-1 ring-offset-background rounded-full outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 cursor-pointer"
                      onClick={(e) => handleRemove(option.value, e)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          handleRemove(option.value, e as unknown as React.MouseEvent);
                        }
                      }}
                    >
                      <X className="h-3 w-3" />
                      <span className="sr-only">Remove {option.label}</span>
                    </span>
                  </Badge>
                ))}
                {remainingCount > 0 && (
                  <Badge variant="outline" className="mb-0.5">
                    +{remainingCount} more
                  </Badge>
                )}
              </>
            )}
          </div>
          {isLoading ? (
            <Loader2 className="ml-2 h-4 w-4 shrink-0 animate-spin" />
          ) : (
            <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[--radix-popover-trigger-width] p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput
            placeholder={searchPlaceholder}
            value={inputValue}
            onValueChange={setInputValue}
          />
          {showSelectAll && (
            <div className="flex items-center gap-1 px-2 py-1.5 border-b">
              <Button
                variant="ghost"
                size="sm"
                className="flex-1 h-7 text-xs"
                onClick={handleSelectAll}
              >
                Select All
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="flex-1 h-7 text-xs"
                onClick={handleClearAll}
              >
                Clear All
              </Button>
            </div>
          )}
          <CommandList>
            {isLoading ? (
              <div className="flex items-center justify-center py-6">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span className="ml-2 text-sm text-muted-foreground">Loading...</span>
              </div>
            ) : (
              <>
                <CommandEmpty>{emptyText}</CommandEmpty>
                {groups && groups.length > 1 ? (
                  groups.map((group) => {
                    const visibleOptions = group.options.filter((o) =>
                      filteredValueSet.has(o.value)
                    );
                    if (visibleOptions.length === 0) return null;
                    return (
                      <CommandGroup key={group.label} heading={group.label}>
                        {visibleOptions.map(renderItem)}
                      </CommandGroup>
                    );
                  })
                ) : (
                  <CommandGroup>
                    {filteredOptions.map(renderItem)}
                  </CommandGroup>
                )}
              </>
            )}
          </CommandList>
          {selectedValues.length > 0 && !showSelectAll && (
            <div className="border-t p-2">
              <Button
                variant="ghost"
                size="sm"
                className="w-full"
                onClick={handleClearAll}
              >
                Clear all ({selectedValues.length})
              </Button>
            </div>
          )}
        </Command>
      </PopoverContent>
    </Popover>
  );
}
