import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getGlossarySectors,
  getGlossarySectorById,
  createGlossarySector,
  updateGlossarySector,
  createGlossaryTerm,
  updateGlossaryTerm,
  deleteGlossaryTerm,
  importGlossaryTerms,
} from './glossary';
import type {
  CreateSectorRequest,
  UpdateSectorRequest,
  CreateTermRequest,
  UpdateTermRequest,
} from '@/types/validation';

// ============================================
// Query Keys
// ============================================

export const GLOSSARY_KEY = ['glossary'];
export const GLOSSARY_SECTORS_KEY = [...GLOSSARY_KEY, 'sectors'];

// ============================================
// Query Hooks
// ============================================

export function useGlossarySectors() {
  return useQuery({
    queryKey: GLOSSARY_SECTORS_KEY,
    queryFn: getGlossarySectors,
  });
}

export function useGlossarySector(id: string, enabled = true) {
  return useQuery({
    queryKey: [...GLOSSARY_SECTORS_KEY, 'by-id', id],
    queryFn: () => getGlossarySectorById(id),
    enabled: !!id && enabled,
  });
}

// ============================================
// Mutation Hooks
// ============================================

export function useCreateGlossarySector() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateSectorRequest) => createGlossarySector(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GLOSSARY_SECTORS_KEY });
    },
  });
}

export function useUpdateGlossarySector() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateSectorRequest }) =>
      updateGlossarySector(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GLOSSARY_SECTORS_KEY });
    },
  });
}

export function useCreateGlossaryTerm() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ sectorId, data }: { sectorId: string; data: CreateTermRequest }) =>
      createGlossaryTerm(sectorId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GLOSSARY_SECTORS_KEY });
    },
  });
}

export function useUpdateGlossaryTerm() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ termId, data }: { termId: string; data: UpdateTermRequest }) =>
      updateGlossaryTerm(termId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GLOSSARY_SECTORS_KEY });
    },
  });
}

export function useDeleteGlossaryTerm() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (termId: string) => deleteGlossaryTerm(termId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GLOSSARY_SECTORS_KEY });
    },
  });
}

export function useImportGlossaryTerms(glossaryId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => importGlossaryTerms(glossaryId, file),
    onSuccess: async () => {
      // Refetch both: sectors list (term count) and sector detail (term list)
      await Promise.all([
        queryClient.refetchQueries({ queryKey: GLOSSARY_SECTORS_KEY, exact: true }),
        queryClient.refetchQueries({ queryKey: [...GLOSSARY_SECTORS_KEY, 'by-id', glossaryId], exact: true }),
      ]);
    },
  });
}
