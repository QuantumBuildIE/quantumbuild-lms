'use client';

import { useState, useCallback } from 'react';
import axios from 'axios';
import { apiClient } from '@/lib/api/client';

interface UploadSourceUrlResponse {
  uploadUrl: string;
  publicUrl: string;
  key: string;
}

interface UploadResult {
  publicUrl: string;
  fileName: string;
  contentType: string;
}

interface UseUploadSourceFileReturn {
  upload: (file: File) => Promise<UploadResult>;
  progress: number;
  isUploading: boolean;
  error: string | null;
  reset: () => void;
}

async function getPresignedUrl(fileName: string, contentType: string): Promise<UploadSourceUrlResponse> {
  const response = await apiClient.post<UploadSourceUrlResponse>(
    '/toolbox-talks/learning-wizard/upload-source-url',
    { fileName, contentType }
  );
  return response.data;
}

export function useUploadSourceFile(): UseUploadSourceFileReturn {
  const [progress, setProgress] = useState(0);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reset = useCallback(() => {
    setProgress(0);
    setIsUploading(false);
    setError(null);
  }, []);

  const upload = useCallback(async (file: File): Promise<UploadResult> => {
    setIsUploading(true);
    setProgress(0);
    setError(null);

    try {
      // Step 1: get presigned PUT URL
      const { uploadUrl, publicUrl } = await getPresignedUrl(file.name, file.type);

      // Step 2: PUT directly to R2 using presigned URL (no auth header — it's in the URL)
      await axios.put(uploadUrl, file, {
        headers: { 'Content-Type': file.type },
        onUploadProgress: (progressEvent) => {
          if (progressEvent.total) {
            const pct = Math.round((progressEvent.loaded * 100) / progressEvent.total);
            setProgress(pct);
          }
        },
      });

      setProgress(100);
      return { publicUrl, fileName: file.name, contentType: file.type };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Upload failed';
      setError(message);
      throw err;
    } finally {
      setIsUploading(false);
    }
  }, []);

  return { upload, progress, isUploading, error, reset };
}
