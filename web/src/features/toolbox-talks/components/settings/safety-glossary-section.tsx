'use client';

import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { Separator } from '@/components/ui/separator';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  ChevronDown,
  ChevronRight,
  Lock,
  Plus,
  Pencil,
  Trash2,
  Save,
  X,
  Copy,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  useGlossarySectors,
  useGlossarySector,
  useCreateGlossarySector,
  useUpdateGlossarySector,
  useCreateGlossaryTerm,
  useUpdateGlossaryTerm,
  useDeleteGlossaryTerm,
} from '@/lib/api/toolbox-talks/use-glossary';
import type {
  GlossarySectorListItem,
  GlossaryTermDto,
  CreateTermRequest,
} from '@/types/validation';

// ============================================
// Main Section Component
// ============================================

export function SafetyGlossarySection() {
  const { data: sectors, isLoading } = useGlossarySectors();
  const [expandedSector, setExpandedSector] = useState<string | null>(null);

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Safety Glossary</CardTitle>
          <CardDescription>Manage safety-critical terminology by industry sector</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Safety Glossary</CardTitle>
        <CardDescription>
          Manage safety-critical terminology by industry sector. System default sectors are
          read-only — create a tenant override to customise.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-2">
        {sectors && sectors.length > 0 ? (
          sectors.map((sector) => (
            <SectorRow
              key={sector.id}
              sector={sector}
              isExpanded={expandedSector === sector.sectorKey}
              onToggle={() =>
                setExpandedSector(
                  expandedSector === sector.sectorKey ? null : sector.sectorKey
                )
              }
            />
          ))
        ) : (
          <p className="text-sm text-muted-foreground py-4 text-center">
            No glossary sectors found.
          </p>
        )}
      </CardContent>
    </Card>
  );
}

// ============================================
// Sector Row
// ============================================

function SectorRow({
  sector,
  isExpanded,
  onToggle,
}: {
  sector: GlossarySectorListItem;
  isExpanded: boolean;
  onToggle: () => void;
}) {
  const createOverrideMutation = useCreateGlossarySector();
  const [editingSector, setEditingSector] = useState(false);
  const [editName, setEditName] = useState(sector.sectorName);
  const [editIcon, setEditIcon] = useState(sector.sectorIcon ?? '');
  const updateSectorMutation = useUpdateGlossarySector();

  const handleCreateOverride = async () => {
    try {
      await createOverrideMutation.mutateAsync({
        sectorKey: sector.sectorKey,
        sectorName: sector.sectorName,
        sectorIcon: sector.sectorIcon ?? undefined,
      });
      toast.success('Tenant override created');
    } catch {
      toast.error('Failed to create override');
    }
  };

  const handleSaveSector = async () => {
    try {
      await updateSectorMutation.mutateAsync({
        id: sector.id,
        data: { sectorName: editName, sectorIcon: editIcon || undefined },
      });
      setEditingSector(false);
      toast.success('Sector updated');
    } catch {
      toast.error('Failed to update sector');
    }
  };

  return (
    <div className="border rounded-lg">
      <div
        className="flex items-center justify-between p-3 cursor-pointer hover:bg-muted/50"
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <span className="text-lg">{sector.sectorIcon ?? '📁'}</span>
          <div>
            <div className="flex items-center gap-2">
              <span className="font-medium text-sm">{sector.sectorName}</span>
              {sector.isSystemDefault && (
                <Badge variant="secondary" className="gap-1 text-xs">
                  <Lock className="h-3 w-3" />
                  System
                </Badge>
              )}
            </div>
            <span className="text-xs text-muted-foreground">
              {sector.termCount} term{sector.termCount !== 1 ? 's' : ''}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {sector.isSystemDefault && (
            <Button
              variant="outline"
              size="sm"
              onClick={(e) => {
                e.stopPropagation();
                handleCreateOverride();
              }}
              disabled={createOverrideMutation.isPending}
            >
              <Copy className="h-3.5 w-3.5 mr-1" />
              Create Override
            </Button>
          )}
          {!sector.isSystemDefault && (
            <Button
              variant="ghost"
              size="sm"
              onClick={(e) => {
                e.stopPropagation();
                setEditingSector(true);
                setEditName(sector.sectorName);
                setEditIcon(sector.sectorIcon ?? '');
              }}
            >
              <Pencil className="h-3.5 w-3.5" />
            </Button>
          )}
          {isExpanded ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
        </div>
      </div>

      {/* Inline sector edit */}
      {editingSector && !sector.isSystemDefault && (
        <div className="px-3 pb-3 flex items-center gap-2">
          <Input
            value={editIcon}
            onChange={(e) => setEditIcon(e.target.value)}
            placeholder="Icon"
            className="w-16"
          />
          <Input
            value={editName}
            onChange={(e) => setEditName(e.target.value)}
            placeholder="Sector name"
            className="flex-1"
          />
          <Button
            size="sm"
            onClick={handleSaveSector}
            disabled={updateSectorMutation.isPending}
          >
            <Save className="h-3.5 w-3.5" />
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setEditingSector(false)}>
            <X className="h-3.5 w-3.5" />
          </Button>
        </div>
      )}

      {/* Expanded terms */}
      {isExpanded && (
        <SectorTerms sectorKey={sector.sectorKey} sectorId={sector.id} isSystemDefault={sector.isSystemDefault} />
      )}
    </div>
  );
}

// ============================================
// Sector Terms (expanded content)
// ============================================

function SectorTerms({
  sectorKey,
  sectorId,
  isSystemDefault,
}: {
  sectorKey: string;
  sectorId: string;
  isSystemDefault: boolean;
}) {
  const { data: detail, isLoading } = useGlossarySector(sectorKey);
  const [showAddForm, setShowAddForm] = useState(false);

  if (isLoading) {
    return (
      <div className="px-3 pb-3 space-y-2">
        <Skeleton className="h-8 w-full" />
        <Skeleton className="h-8 w-full" />
      </div>
    );
  }

  const terms = detail?.terms ?? [];

  return (
    <div className="border-t">
      <div className="p-3 space-y-2">
        {/* Terms table header */}
        {terms.length > 0 && (
          <div className="grid grid-cols-[1fr_120px_80px_1fr_auto] gap-2 text-xs font-medium text-muted-foreground px-2">
            <span>English Term</span>
            <span>Category</span>
            <span>Critical</span>
            <span>Translations</span>
            <span className="w-16" />
          </div>
        )}

        {/* Term rows */}
        {terms.map((term) => (
          <TermRow key={term.id} term={term} isSystemDefault={isSystemDefault} />
        ))}

        {terms.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-2">
            No terms in this sector.
          </p>
        )}

        {/* Add term button */}
        {!isSystemDefault && (
          <>
            <Separator />
            {showAddForm ? (
              <AddTermForm
                sectorId={sectorId}
                onClose={() => setShowAddForm(false)}
              />
            ) : (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowAddForm(true)}
                className="w-full"
              >
                <Plus className="h-3.5 w-3.5 mr-1" />
                Add Term
              </Button>
            )}
          </>
        )}
      </div>
    </div>
  );
}

// ============================================
// Term Row
// ============================================

function TermRow({
  term,
  isSystemDefault,
}: {
  term: GlossaryTermDto;
  isSystemDefault: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [editTerm, setEditTerm] = useState(term.englishTerm);
  const [editCategory, setEditCategory] = useState(term.category);
  const [editCritical, setEditCritical] = useState(term.isCritical);
  const [editTranslations, setEditTranslations] = useState(term.translations);
  const updateMutation = useUpdateGlossaryTerm();
  const deleteMutation = useDeleteGlossaryTerm();

  // Parse translations for display
  const translationEntries = parseTranslations(term.translations);

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({
        termId: term.id,
        data: {
          englishTerm: editTerm,
          category: editCategory,
          isCritical: editCritical,
          translations: editTranslations,
        },
      });
      setEditing(false);
      toast.success('Term updated');
    } catch {
      toast.error('Failed to update term');
    }
  };

  const handleDelete = async () => {
    try {
      await deleteMutation.mutateAsync(term.id);
      toast.success('Term deleted');
    } catch {
      toast.error('Failed to delete term');
    }
  };

  if (editing && !isSystemDefault) {
    return (
      <div className="space-y-2 p-2 border rounded bg-muted/30">
        <div className="grid grid-cols-[1fr_120px_80px] gap-2">
          <Input
            value={editTerm}
            onChange={(e) => setEditTerm(e.target.value)}
            placeholder="English term"
          />
          <Input
            value={editCategory}
            onChange={(e) => setEditCategory(e.target.value)}
            placeholder="Category"
          />
          <div className="flex items-center justify-center">
            <Switch checked={editCritical} onCheckedChange={setEditCritical} />
          </div>
        </div>
        <div>
          <label className="text-xs font-medium text-muted-foreground mb-1 block">
            Translations
          </label>
          <TranslationFields
            value={editTranslations}
            onChange={setEditTranslations}
          />
        </div>
        <div className="flex gap-2 justify-end">
          <Button
            size="sm"
            onClick={handleSave}
            disabled={updateMutation.isPending}
          >
            <Save className="h-3.5 w-3.5 mr-1" />
            Save
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
            Cancel
          </Button>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="grid grid-cols-[1fr_120px_80px_1fr_auto] gap-2 items-center px-2 py-1.5 rounded hover:bg-muted/30 text-sm">
        <span>{term.englishTerm}</span>
        <span className="text-muted-foreground">{term.category}</span>
        <span>
          {term.isCritical && (
            <Badge variant="destructive" className="text-xs">
              Critical
            </Badge>
          )}
        </span>
        <div className="flex flex-wrap gap-1">
          {translationEntries.map(([lang]) => (
            <Badge key={lang} variant="outline" className="text-xs">
              {lang}
            </Badge>
          ))}
          {translationEntries.length === 0 && (
            <span className="text-xs text-muted-foreground">None</span>
          )}
        </div>
        {!isSystemDefault && (
          <div className="flex gap-1 w-16 justify-end">
            <Button
              variant="ghost"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={() => {
                setEditing(true);
                setEditTerm(term.englishTerm);
                setEditCategory(term.category);
                setEditCritical(term.isCritical);
                setEditTranslations(term.translations);
              }}
            >
              <Pencil className="h-3 w-3" />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="h-7 w-7 p-0 text-destructive hover:text-destructive"
              onClick={() => setDeleteConfirm(true)}
            >
              <Trash2 className="h-3 w-3" />
            </Button>
          </div>
        )}
      </div>

      <AlertDialog open={deleteConfirm} onOpenChange={setDeleteConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Term</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete &ldquo;{term.englishTerm}&rdquo;? This action cannot
              be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

// ============================================
// Add Term Form
// ============================================

function AddTermForm({
  sectorId,
  onClose,
}: {
  sectorId: string;
  onClose: () => void;
}) {
  const [englishTerm, setEnglishTerm] = useState('');
  const [category, setCategory] = useState('');
  const [isCritical, setIsCritical] = useState(true);
  const [translations, setTranslations] = useState('{}');
  const createMutation = useCreateGlossaryTerm();

  const handleSubmit = async () => {
    if (!englishTerm.trim() || !category.trim()) {
      toast.error('English term and category are required');
      return;
    }

    try {
      await createMutation.mutateAsync({
        sectorId,
        data: {
          englishTerm: englishTerm.trim(),
          category: category.trim(),
          isCritical,
          translations,
        } as CreateTermRequest,
      });
      toast.success('Term added');
      setEnglishTerm('');
      setCategory('');
      setIsCritical(true);
      setTranslations('{}');
      onClose();
    } catch {
      toast.error('Failed to add term');
    }
  };

  return (
    <div className="space-y-2 p-2 border rounded bg-muted/30">
      <div className="grid grid-cols-[1fr_120px_80px] gap-2">
        <Input
          value={englishTerm}
          onChange={(e) => setEnglishTerm(e.target.value)}
          placeholder="English term"
          autoFocus
        />
        <Input
          value={category}
          onChange={(e) => setCategory(e.target.value)}
          placeholder="Category"
        />
        <div className="flex items-center justify-center">
          <Switch checked={isCritical} onCheckedChange={setIsCritical} />
        </div>
      </div>
      <div>
        <label className="text-xs font-medium text-muted-foreground mb-1 block">
          Translations
        </label>
        <TranslationFields
          value={translations}
          onChange={setTranslations}
        />
      </div>
      <div className="flex gap-2 justify-end">
        <Button
          size="sm"
          onClick={handleSubmit}
          disabled={createMutation.isPending}
        >
          <Plus className="h-3.5 w-3.5 mr-1" />
          Add Term
        </Button>
        <Button size="sm" variant="ghost" onClick={onClose}>
          Cancel
        </Button>
      </div>
    </div>
  );
}

// ============================================
// Translation Language Grid
// ============================================

const SUPPORTED_LANGUAGES = [
  { code: 'fr', name: 'French', flag: '🇫🇷' },
  { code: 'pl', name: 'Polish', flag: '🇵🇱' },
  { code: 'ro', name: 'Romanian', flag: '🇷🇴' },
  { code: 'uk', name: 'Ukrainian', flag: '🇺🇦' },
  { code: 'pt', name: 'Portuguese', flag: '🇵🇹' },
  { code: 'es', name: 'Spanish', flag: '🇪🇸' },
  { code: 'lt', name: 'Lithuanian', flag: '🇱🇹' },
  { code: 'de', name: 'German', flag: '🇩🇪' },
  { code: 'lv', name: 'Latvian', flag: '🇱🇻' },
] as const;

function parseTranslationsToRecord(json: string): Record<string, string> {
  try {
    const parsed = JSON.parse(json);
    if (typeof parsed === 'object' && parsed !== null) {
      return parsed as Record<string, string>;
    }
  } catch {
    // malformed JSON — default all fields to empty
  }
  return {};
}

function serialiseTranslations(values: Record<string, string>): string {
  const filtered: Record<string, string> = {};
  for (const [key, val] of Object.entries(values)) {
    if (val.trim()) {
      filtered[key] = val.trim();
    }
  }
  return JSON.stringify(filtered);
}

function TranslationFields({
  value,
  onChange,
}: {
  value: string;
  onChange: (json: string) => void;
}) {
  const parsed = parseTranslationsToRecord(value);

  const handleFieldChange = (code: string, fieldValue: string) => {
    const updated = { ...parseTranslationsToRecord(value), [code]: fieldValue };
    onChange(serialiseTranslations(updated));
  };

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
      {SUPPORTED_LANGUAGES.map(({ code, name, flag }) => (
        <div key={code}>
          <label className="text-xs font-medium text-muted-foreground mb-1 block">
            {flag} {name}
          </label>
          <Input
            value={parsed[code] ?? ''}
            onChange={(e) => handleFieldChange(code, e.target.value)}
            placeholder={`Translation in ${name}...`}
          />
        </div>
      ))}
    </div>
  );
}

// ============================================
// Helpers
// ============================================

function parseTranslations(json: string): [string, string][] {
  try {
    const parsed = JSON.parse(json);
    if (typeof parsed === 'object' && parsed !== null) {
      return Object.entries(parsed) as [string, string][];
    }
  } catch {
    // invalid JSON
  }
  return [];
}
