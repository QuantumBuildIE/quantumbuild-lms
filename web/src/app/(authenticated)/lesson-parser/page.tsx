'use client';

import { useState, useRef, useCallback, useEffect } from 'react';
import Link from 'next/link';
import { format } from 'date-fns';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Progress } from '@/components/ui/progress';
import {
  Loader2,
  CheckCircle,
  AlertCircle,
  FileText,
  Globe,
  Type,
} from 'lucide-react';
import { DataTable } from '@/components/shared/data-table';
import type { Column } from '@/components/shared/data-table';
import { lessonParserApi } from '@/lib/api/lesson-parser';
import { useLessonParserHub } from '@/features/lesson-parser/hooks/use-lesson-parser-hub';
import { useParseJobs, lessonParserKeys } from '@/features/lesson-parser/hooks/use-parse-jobs';
import { ParseStatusBadge } from '@/features/lesson-parser/parse-status-badge';
import { InputTypeBadge } from '@/features/lesson-parser/input-type-badge';
import { RetryButton } from '@/features/lesson-parser/retry-button';
import type { ParseJob } from '@/types/lesson-parser';

type FormState = 'idle' | 'submitting' | 'processing' | 'completed' | 'error';

export default function LessonParserPage() {
  const queryClient = useQueryClient();
  const { connectionId, progress, result, error: hubError, isConnected, reset } = useLessonParserHub();

  // Form state
  const [formState, setFormState] = useState<FormState>('idle');
  const [activeTab, setActiveTab] = useState('document');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Input state
  const [file, setFile] = useState<File | null>(null);
  const [url, setUrl] = useState('');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');

  // File input ref for resetting
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Parse history
  const [page, setPage] = useState(1);
  const { data: jobsData, isLoading: jobsLoading } = useParseJobs(page);

  // Sync hub events with form state
  useEffect(() => {
    if (formState === 'processing' && result) {
      setFormState('completed');
      queryClient.invalidateQueries({ queryKey: lessonParserKeys.jobs() });
    }
  }, [formState, result, queryClient]);

  useEffect(() => {
    if (formState === 'processing' && hubError) {
      setFormState('error');
      setErrorMessage(hubError);
      queryClient.invalidateQueries({ queryKey: lessonParserKeys.jobs() });
    }
  }, [formState, hubError, queryClient]);

  const resetForm = useCallback(() => {
    setFormState('idle');
    setErrorMessage(null);
    setFile(null);
    setUrl('');
    setTitle('');
    setContent('');
    reset();
    if (fileInputRef.current) fileInputRef.current.value = '';
  }, [reset]);

  const handleTabChange = (tab: string) => {
    setActiveTab(tab);
    if (formState === 'idle') {
      setFile(null);
      setUrl('');
      setTitle('');
      setContent('');
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
    reset();
  };

  const handleSubmitDocument = async () => {
    if (!file || !connectionId) return;
    setFormState('submitting');
    setErrorMessage(null);
    try {
      await lessonParserApi.submitDocument(file, connectionId);
      setFormState('processing');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to upload document';
      toast.error(message);
      setFormState('idle');
    }
  };

  const handleSubmitUrl = async () => {
    if (!url || !connectionId) return;
    setFormState('submitting');
    setErrorMessage(null);
    try {
      await lessonParserApi.submitUrl(url, connectionId);
      setFormState('processing');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to fetch URL';
      toast.error(message);
      setFormState('idle');
    }
  };

  const handleSubmitText = async () => {
    if (content.length < 100 || !title || !connectionId) return;
    setFormState('submitting');
    setErrorMessage(null);
    try {
      await lessonParserApi.submitText(content, title, connectionId);
      setFormState('processing');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to submit text';
      toast.error(message);
      setFormState('idle');
    }
  };

  const isSubmitting = formState === 'submitting';
  const submitDisabled = !isConnected || isSubmitting;

  // Parse history columns
  const columns: Column<ParseJob>[] = [
    {
      key: 'createdAt',
      header: 'Date',
      render: (item) => format(new Date(item.createdAt), 'dd MMM yyyy HH:mm'),
    },
    {
      key: 'inputReference',
      header: 'Source',
      render: (item) => (
        <div className="max-w-[200px] truncate" title={item.inputReference}>
          {item.inputReference}
        </div>
      ),
    },
    {
      key: 'inputType',
      header: 'Type',
      render: (item) => <InputTypeBadge type={item.inputType} />,
    },
    {
      key: 'status',
      header: 'Status',
      render: (item) => <ParseStatusBadge status={item.status} />,
    },
    {
      key: 'talksGenerated',
      header: 'Talks',
      render: (item) => (item.talksGenerated > 0 ? item.talksGenerated : '\u2014'),
    },
    {
      key: 'generatedCourseId',
      header: 'Course',
      render: (item) =>
        item.generatedCourseId ? (
          <Link
            href={`/admin/toolbox-talks/courses/${item.generatedCourseId}/edit`}
            className="text-primary hover:underline text-sm"
          >
            {item.generatedCourseTitle ?? 'View Course'}
          </Link>
        ) : (
          '\u2014'
        ),
    },
    {
      key: 'actions',
      header: 'Actions',
      render: (item) =>
        item.status === 'Failed' ? <RetryButton jobId={item.id} /> : null,
    },
  ];

  return (
    <div className="space-y-8">
      {/* Page header */}
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Lesson Parser</h1>
        <p className="text-muted-foreground">
          Convert existing training documents into Learnings automatically
        </p>
      </div>

      {/* Connection status */}
      {formState === 'idle' && (
        <div className="flex items-center gap-2 text-sm">
          {isConnected ? (
            <>
              <div className="h-2 w-2 rounded-full bg-green-500" />
              <span className="text-muted-foreground">Ready</span>
            </>
          ) : (
            <>
              <Loader2 className="h-3 w-3 animate-spin text-muted-foreground" />
              <span className="text-muted-foreground">Connecting...</span>
            </>
          )}
        </div>
      )}

      {/* Section 1 — New Parse */}
      {formState === 'idle' || formState === 'submitting' ? (
        <Card>
          <CardContent className="pt-6">
            <Tabs value={activeTab} onValueChange={handleTabChange}>
              <TabsList className="grid w-full grid-cols-3">
                <TabsTrigger value="document" className="gap-1.5">
                  <FileText className="h-4 w-4" />
                  Document
                </TabsTrigger>
                <TabsTrigger value="url" className="gap-1.5">
                  <Globe className="h-4 w-4" />
                  URL
                </TabsTrigger>
                <TabsTrigger value="text" className="gap-1.5">
                  <Type className="h-4 w-4" />
                  Text
                </TabsTrigger>
              </TabsList>

              {/* Document Tab */}
              <TabsContent value="document" className="space-y-4 pt-4">
                <div className="space-y-2">
                  <Label>Upload Document</Label>
                  <Input
                    ref={fileInputRef}
                    type="file"
                    accept=".pdf,.docx"
                    onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  />
                  <p className="text-sm text-muted-foreground">
                    Supported formats: PDF (.pdf) and Word (.docx). Maximum file size: 50MB
                  </p>
                </div>
                <Button
                  onClick={handleSubmitDocument}
                  disabled={!file || submitDisabled}
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Uploading...
                    </>
                  ) : (
                    'Parse Document'
                  )}
                </Button>
              </TabsContent>

              {/* URL Tab */}
              <TabsContent value="url" className="space-y-4 pt-4">
                <div className="space-y-2">
                  <Label>Web Page URL</Label>
                  <Input
                    type="url"
                    placeholder="https://example.com/training-document"
                    value={url}
                    onChange={(e) => setUrl(e.target.value)}
                  />
                  <p className="text-sm text-muted-foreground">
                    The page content will be extracted and analysed
                  </p>
                </div>
                <Button
                  onClick={handleSubmitUrl}
                  disabled={!url || submitDisabled}
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Fetching...
                    </>
                  ) : (
                    'Parse URL'
                  )}
                </Button>
              </TabsContent>

              {/* Text Tab */}
              <TabsContent value="text" className="space-y-4 pt-4">
                <div className="space-y-2">
                  <Label>Document Title</Label>
                  <Input
                    placeholder="e.g. Fire Safety Procedures"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Document Content</Label>
                  <Textarea
                    placeholder="Paste your training document content here..."
                    rows={12}
                    value={content}
                    onChange={(e) => setContent(e.target.value)}
                  />
                  <p className="text-sm text-muted-foreground">
                    Minimum 100 characters required. Currently: {content.length} characters
                  </p>
                </div>
                <Button
                  onClick={handleSubmitText}
                  disabled={content.length < 100 || !title || submitDisabled}
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Submitting...
                    </>
                  ) : (
                    'Parse Document'
                  )}
                </Button>
              </TabsContent>
            </Tabs>
          </CardContent>
        </Card>
      ) : formState === 'processing' ? (
        /* Processing State UI */
        <Card>
          <CardContent className="py-8">
            <div className="space-y-4">
              <div className="flex items-center gap-3">
                <Loader2 className="h-5 w-5 animate-spin text-primary" />
                <span className="font-medium">{progress?.stage ?? 'Initialising...'}</span>
              </div>
              <Progress value={progress?.percentComplete ?? 0} className="h-2" />
              <p className="text-sm text-muted-foreground text-center">
                {progress?.percentComplete ?? 0}% complete
                {(progress?.totalTalks ?? 0) > 0 &&
                  ` \u00B7 Talk ${progress?.currentTalk} of ${progress?.totalTalks}`}
              </p>
            </div>
          </CardContent>
        </Card>
      ) : formState === 'completed' && result ? (
        /* Success State UI */
        <Card className="border-green-200 bg-green-50">
          <CardContent className="py-6">
            <div className="flex items-start gap-4">
              <CheckCircle className="h-8 w-8 text-green-600 mt-1 shrink-0" />
              <div className="space-y-1 flex-1">
                <h3 className="font-semibold text-green-900">Course Created Successfully</h3>
                <p className="text-green-700">
                  &ldquo;{result.courseTitle}&rdquo; &mdash; {result.talksGenerated} talks generated
                </p>
                <div className="flex gap-3 mt-4">
                  <Button asChild variant="default" size="sm">
                    <Link href={`/admin/toolbox-talks/courses/${result.courseId}/edit`}>
                      View Course
                    </Link>
                  </Button>
                  <Button variant="outline" size="sm" onClick={resetForm}>
                    Parse Another Document
                  </Button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : formState === 'error' ? (
        /* Error State UI */
        <Card className="border-red-200 bg-red-50">
          <CardContent className="py-6">
            <div className="flex items-start gap-4">
              <AlertCircle className="h-8 w-8 text-red-600 mt-1 shrink-0" />
              <div className="space-y-1 flex-1">
                <h3 className="font-semibold text-red-900">Processing Failed</h3>
                <p className="text-sm text-red-700">{errorMessage}</p>
                <Button variant="outline" size="sm" onClick={resetForm} className="mt-3">
                  Try Again
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {/* Section 2 — Parse History */}
      <div className="space-y-4">
        <h2 className="text-lg font-semibold">Parse History</h2>
        <DataTable<ParseJob>
          columns={columns}
          data={jobsData?.items ?? []}
          isLoading={jobsLoading}
          emptyMessage="No parse jobs yet. Submit a document above to get started."
          keyExtractor={(item) => item.id}
          pagination={
            jobsData
              ? {
                  pageNumber: jobsData.pageNumber,
                  pageSize: jobsData.pageSize,
                  totalCount: jobsData.totalCount,
                  totalPages: jobsData.totalPages,
                }
              : undefined
          }
          onPageChange={setPage}
        />
      </div>
    </div>
  );
}
