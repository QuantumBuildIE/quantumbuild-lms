"use client";

import * as React from "react";
import { Input } from "@/components/ui/input";
import { Combobox } from "@/components/ui/combobox";
import { useLookupValues } from "@/hooks/use-lookups";

interface LookupFieldProps {
  categoryName: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
}

/**
 * A form field that fetches lookup values for a given category and renders
 * a combobox when values exist, or falls back to a free-text input.
 */
export function LookupField({
  categoryName,
  value,
  onChange,
  placeholder,
  disabled,
}: LookupFieldProps) {
  const { data: lookupValues, isLoading } = useLookupValues(categoryName);

  const hasValues = !isLoading && lookupValues && lookupValues.length > 0;

  if (!hasValues && !isLoading) {
    return (
      <div className="space-y-1">
        <Input
          placeholder={placeholder}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
        />
        <p className="text-xs text-muted-foreground">
          Configure options in Settings &rarr; Lookups
        </p>
      </div>
    );
  }

  const options = (lookupValues ?? [])
    .filter((lv) => lv.isActive)
    .map((lv) => ({
      value: lv.name,
      label: lv.name,
    }));

  return (
    <Combobox
      options={options}
      value={value}
      onValueChange={(val) => onChange(val)}
      placeholder={placeholder}
      searchPlaceholder={`Search ${categoryName.toLowerCase()}...`}
      emptyText={`No ${categoryName.toLowerCase()} found.`}
      isLoading={isLoading}
      disabled={disabled}
      allowCustomValue={true}
    />
  );
}
