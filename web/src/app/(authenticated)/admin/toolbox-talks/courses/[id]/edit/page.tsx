'use client';

import { use } from 'react';
import { useRouter } from 'next/navigation';
import { CourseForm } from '@/features/toolbox-talks/components/CourseForm';
import { useToolboxTalkCourse } from '@/lib/api/toolbox-talks/use-courses';
import { Skeleton } from '@/components/ui/skeleton';

export default function EditCoursePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const { data: course, isLoading, error } = useToolboxTalkCourse(id);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-4 w-96" />
        <div className="space-y-4 rounded-lg border p-6">
          <Skeleton className="h-6 w-32" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
        <div className="space-y-4 rounded-lg border p-6">
          <Skeleton className="h-6 w-24" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
        </div>
      </div>
    );
  }

  if (error || !course) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
        <p className="text-destructive">
          {error ? `Error loading course: ${error instanceof Error ? error.message : 'Unknown error'}` : 'Course not found'}
        </p>
      </div>
    );
  }

  return <CourseForm course={course} />;
}
