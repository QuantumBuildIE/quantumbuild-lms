'use client';

import { useParams, useRouter } from 'next/navigation';
import { ValidationRunDetailView } from '@/features/toolbox-talks/components/ValidationRunDetailView';

export default function CourseValidationRunDetailPage() {
  const params = useParams();
  const router = useRouter();
  const courseId = params.id as string;
  const runId = params.runId as string;

  return (
    <div className="space-y-6">
      <ValidationRunDetailView
        courseId={courseId}
        runId={runId}
        onBack={() => router.push(`/admin/toolbox-talks/courses/${courseId}/edit`)}
      />
    </div>
  );
}
