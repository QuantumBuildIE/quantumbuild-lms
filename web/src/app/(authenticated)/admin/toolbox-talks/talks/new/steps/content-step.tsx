'use client';

import { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Upload,
  Link,
  Video,
  FileText,
  Check,
  X,
  Loader2,
  AlertCircle,
  Info,
} from 'lucide-react';
import { toast } from 'sonner';
import type { ToolboxTalkWizardData } from '../page';
import {
  uploadToolboxTalkVideo,
  setToolboxTalkVideoUrl,
  uploadToolboxTalkPdf,
  deleteToolboxTalkVideo,
  deleteToolboxTalkPdf,
} from '@/lib/api/toolbox-talks';

interface ContentStepProps {
  data: ToolboxTalkWizardData;
  updateData: (updates: Partial<ToolboxTalkWizardData>) => void;
  onNext: () => void;
  onBack: () => void;
}

export function ContentStep({ data, updateData, onNext, onBack }: ContentStepProps) {
  // DEBUG: Log on mount and data changes
  useEffect(() => {
    console.log('📁 ContentStep mounted/updated');
    console.log('📁 data.id:', data.id);
    console.log('📁 Full data:', data);
  }, [data]);

  // Video state
  const [videoTab, setVideoTab] = useState<'upload' | 'url'>('upload');
  const [videoUrl, setVideoUrl] = useState(data.videoUrl || '');
  const [videoFile, setVideoFile] = useState<File | null>(null);
  const [videoUploading, setVideoUploading] = useState(false);
  const [videoUploadProgress, setVideoUploadProgress] = useState(0);
  const [videoError, setVideoError] = useState<string | null>(null);
  const [videoUploaded, setVideoUploaded] = useState(!!data.videoUrl);

  // PDF state
  const [pdfFile, setPdfFile] = useState<File | null>(null);
  const [pdfUploading, setPdfUploading] = useState(false);
  const [pdfUploadProgress, setPdfUploadProgress] = useState(0);
  const [pdfError, setPdfError] = useState<string | null>(null);
  const [pdfUploaded, setPdfUploaded] = useState(!!data.pdfUrl);

  const [generalError, setGeneralError] = useState<string | null>(null);

  // Video file selection
  const handleVideoFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate file type
    const allowedTypes = ['video/mp4', 'video/quicktime', 'video/x-msvideo', 'video/webm'];
    if (!allowedTypes.includes(file.type)) {
      setVideoError('Please upload a valid video file (MP4, MOV, AVI, or WebM)');
      return;
    }

    // Validate file size (500MB max)
    if (file.size > 500 * 1024 * 1024) {
      setVideoError('Video file must be less than 500MB');
      return;
    }

    setVideoFile(file);
    setVideoError(null);
  };

  // Upload video file
  const handleVideoUpload = async () => {
    console.log('📹 handleVideoUpload called');
    console.log('📹 data.id:', data.id);
    console.log('📹 videoFile:', videoFile?.name);

    if (!videoFile || !data.id) {
      console.error('📹 Missing required data:', { hasFile: !!videoFile, id: data.id });
      setVideoError('Please select a video file first');
      return;
    }

    console.log('📹 Starting upload for ID:', data.id);
    setVideoUploading(true);
    setVideoError(null);
    setVideoUploadProgress(0);

    try {
      const result = await uploadToolboxTalkVideo(
        data.id,
        videoFile,
        (progress) => setVideoUploadProgress(progress)
      );
      console.log('📹 Upload result:', result);

      updateData({
        videoSource: 'DirectUrl',
        videoUrl: result.videoUrl,
        videoFileName: result.fileName,
      });
      setVideoUploaded(true);
      setVideoFile(null);
      toast.success('Video uploaded successfully');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to upload video';
      setVideoError(message);
      toast.error('Upload failed', { description: message });
    } finally {
      setVideoUploading(false);
      setVideoUploadProgress(0);
    }
  };

  // Set external video URL
  const handleSetVideoUrl = async () => {
    if (!videoUrl || !data.id) {
      setVideoError('Please enter a video URL');
      return;
    }

    // Basic URL validation
    try {
      new URL(videoUrl);
    } catch {
      setVideoError('Please enter a valid URL');
      return;
    }

    setVideoUploading(true);
    setVideoError(null);

    try {
      const result = await setToolboxTalkVideoUrl(data.id, videoUrl);
      updateData({
        videoSource: result.source as ToolboxTalkWizardData['videoSource'],
        videoUrl: result.videoUrl,
        videoFileName: null,
      });
      setVideoUploaded(true);
      toast.success('Video URL set successfully');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to set video URL';
      setVideoError(message);
      toast.error('Failed to set URL', { description: message });
    } finally {
      setVideoUploading(false);
    }
  };

  // PDF file selection
  const handlePdfFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate file type
    if (file.type !== 'application/pdf') {
      setPdfError('Please upload a PDF file');
      return;
    }

    // Validate file size (50MB max)
    if (file.size > 50 * 1024 * 1024) {
      setPdfError('PDF file must be less than 50MB');
      return;
    }

    setPdfFile(file);
    setPdfError(null);
  };

  // Upload PDF file
  const handlePdfUpload = async () => {
    console.log('📄 handlePdfUpload called');
    console.log('📄 data.id:', data.id);
    console.log('📄 pdfFile:', pdfFile?.name);

    if (!pdfFile || !data.id) {
      console.error('📄 Missing required data:', { hasFile: !!pdfFile, id: data.id });
      setPdfError('Please select a PDF file first');
      return;
    }

    console.log('📄 Starting upload for ID:', data.id);
    setPdfUploading(true);
    setPdfError(null);
    setPdfUploadProgress(0);

    try {
      const result = await uploadToolboxTalkPdf(
        data.id,
        pdfFile,
        (progress) => setPdfUploadProgress(progress)
      );
      console.log('📄 Upload result:', result);

      updateData({
        pdfUrl: result.pdfUrl,
        pdfFileName: result.fileName,
      });
      setPdfUploaded(true);
      setPdfFile(null);
      toast.success('PDF uploaded successfully');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to upload PDF';
      setPdfError(message);
      toast.error('Upload failed', { description: message });
    } finally {
      setPdfUploading(false);
      setPdfUploadProgress(0);
    }
  };

  // Clear video
  const handleClearVideo = async () => {
    if (!data.id) return;

    try {
      await deleteToolboxTalkVideo(data.id);
      setVideoUrl('');
      setVideoFile(null);
      setVideoUploaded(false);
      setVideoError(null);
      updateData({
        videoSource: null,
        videoUrl: null,
        videoFileName: null,
      });
      toast.success('Video removed');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to remove video';
      toast.error('Failed to remove video', { description: message });
    }
  };

  // Clear PDF
  const handleClearPdf = async () => {
    if (!data.id) return;

    try {
      await deleteToolboxTalkPdf(data.id);
      setPdfFile(null);
      setPdfUploaded(false);
      setPdfError(null);
      updateData({
        pdfUrl: null,
        pdfFileName: null,
      });
      toast.success('PDF removed');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to remove PDF';
      toast.error('Failed to remove PDF', { description: message });
    }
  };

  // Validate and proceed
  const handleNext = () => {
    setGeneralError(null);

    if (!videoUploaded && !pdfUploaded) {
      setGeneralError('Please upload at least one content source (video or PDF)');
      return;
    }

    updateData({
      generateFromVideo: videoUploaded,
      generateFromPdf: pdfUploaded,
    });

    onNext();
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-lg font-semibold">Add Content</h2>
        <p className="text-sm text-muted-foreground">
          Upload a training video and/or RAMS PDF document. At least one is required.
        </p>
      </div>

      {generalError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{generalError}</AlertDescription>
        </Alert>
      )}

      {/* Video Section */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center gap-2 mb-4">
            <Video className="h-5 w-5 text-primary" />
            <h3 className="font-medium">Training Video</h3>
            {videoUploaded && (
              <span className="ml-auto flex items-center text-sm text-green-600">
                <Check className="h-4 w-4 mr-1" />
                Added
              </span>
            )}
          </div>

          {!videoUploaded ? (
            <div className="space-y-4">
              <Tabs value={videoTab} onValueChange={(v) => setVideoTab(v as 'upload' | 'url')}>
                <TabsList className="grid w-full grid-cols-2">
                  <TabsTrigger value="upload" className="flex items-center gap-2">
                    <Upload className="h-4 w-4" />
                    Upload File
                  </TabsTrigger>
                  <TabsTrigger value="url" className="flex items-center gap-2">
                    <Link className="h-4 w-4" />
                    Enter URL
                  </TabsTrigger>
                </TabsList>

                <TabsContent value="upload" className="space-y-3 mt-4">
                  <div>
                    <Label htmlFor="video-file">Select video file</Label>
                    <Input
                      id="video-file"
                      type="file"
                      accept="video/mp4,video/quicktime,video/x-msvideo,video/webm"
                      onChange={handleVideoFileChange}
                      disabled={videoUploading}
                      className="mt-1"
                    />
                    <p className="text-xs text-muted-foreground mt-1">
                      MP4, MOV, AVI, or WebM. Maximum 500MB.
                    </p>
                  </div>

                  {videoFile && (
                    <div className="flex items-center justify-between p-2 bg-muted rounded">
                      <span className="text-sm truncate max-w-[200px]">{videoFile.name}</span>
                      <span className="text-xs text-muted-foreground">
                        {formatFileSize(videoFile.size)}
                      </span>
                    </div>
                  )}

                  {videoUploading && (
                    <div className="space-y-1">
                      <Progress value={videoUploadProgress} className="h-2" />
                      <p className="text-xs text-muted-foreground text-center">
                        Uploading... {videoUploadProgress}%
                      </p>
                    </div>
                  )}

                  <Button
                    onClick={handleVideoUpload}
                    disabled={!videoFile || videoUploading}
                    className="w-full sm:w-auto"
                  >
                    {videoUploading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Upload Video
                  </Button>
                </TabsContent>

                <TabsContent value="url" className="space-y-3 mt-4">
                  <div>
                    <Label htmlFor="video-url">Video URL</Label>
                    <Input
                      id="video-url"
                      type="url"
                      placeholder="https://example.com/video.mp4"
                      value={videoUrl}
                      onChange={(e) => setVideoUrl(e.target.value)}
                      disabled={videoUploading}
                      className="mt-1"
                    />
                    <p className="text-xs text-muted-foreground mt-1">
                      Direct link to MP4 file or YouTube/Vimeo URL
                    </p>
                  </div>

                  <Button
                    onClick={handleSetVideoUrl}
                    disabled={!videoUrl || videoUploading}
                    className="w-full sm:w-auto"
                  >
                    {videoUploading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Set Video URL
                  </Button>
                </TabsContent>
              </Tabs>

              {videoError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{videoError}</AlertDescription>
                </Alert>
              )}
            </div>
          ) : (
            <div className="flex items-center justify-between p-3 bg-green-50 dark:bg-green-950 rounded border border-green-200 dark:border-green-800">
              <div className="flex items-center gap-2 min-w-0">
                <Check className="h-4 w-4 text-green-600 flex-shrink-0" />
                <span className="text-sm truncate">
                  {data.videoFileName || 'External video URL set'}
                </span>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleClearVideo}
                className="flex-shrink-0 ml-2"
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* PDF Section */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center gap-2 mb-4">
            <FileText className="h-5 w-5 text-primary" />
            <h3 className="font-medium">RAMS Document (PDF)</h3>
            {pdfUploaded && (
              <span className="ml-auto flex items-center text-sm text-green-600">
                <Check className="h-4 w-4 mr-1" />
                Added
              </span>
            )}
          </div>

          {!pdfUploaded ? (
            <div className="space-y-4">
              <div>
                <Label htmlFor="pdf-file">Select PDF file</Label>
                <Input
                  id="pdf-file"
                  type="file"
                  accept="application/pdf"
                  onChange={handlePdfFileChange}
                  disabled={pdfUploading}
                  className="mt-1"
                />
                <p className="text-xs text-muted-foreground mt-1">
                  PDF format. Maximum 50MB.
                </p>
              </div>

              {pdfFile && (
                <div className="flex items-center justify-between p-2 bg-muted rounded">
                  <span className="text-sm truncate max-w-[200px]">{pdfFile.name}</span>
                  <span className="text-xs text-muted-foreground">
                    {formatFileSize(pdfFile.size)}
                  </span>
                </div>
              )}

              {pdfUploading && (
                <div className="space-y-1">
                  <Progress value={pdfUploadProgress} className="h-2" />
                  <p className="text-xs text-muted-foreground text-center">
                    Uploading... {pdfUploadProgress}%
                  </p>
                </div>
              )}

              <Button
                onClick={handlePdfUpload}
                disabled={!pdfFile || pdfUploading}
                className="w-full sm:w-auto"
              >
                {pdfUploading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Upload PDF
              </Button>

              {pdfError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{pdfError}</AlertDescription>
                </Alert>
              )}
            </div>
          ) : (
            <div className="flex items-center justify-between p-3 bg-green-50 dark:bg-green-950 rounded border border-green-200 dark:border-green-800">
              <div className="flex items-center gap-2 min-w-0">
                <Check className="h-4 w-4 text-green-600 flex-shrink-0" />
                <span className="text-sm truncate">{data.pdfFileName}</span>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleClearPdf}
                className="flex-shrink-0 ml-2"
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Info about subtitle generation */}
      {videoUploaded && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            <strong>Note:</strong> Subtitles can be generated after completing the
            wizard. Go to Learnings → Actions → Generate Subtitles.
          </AlertDescription>
        </Alert>
      )}

      <div className="flex justify-between pt-4 border-t">
        <Button type="button" variant="outline" onClick={onBack}>
          Back
        </Button>
        <Button onClick={handleNext}>Next: Generate Content</Button>
      </div>
    </div>
  );
}
