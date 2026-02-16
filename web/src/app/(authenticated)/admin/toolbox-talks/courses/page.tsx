'use client';

import { Suspense } from 'react';
import { CourseList } from '@/features/toolbox-talks/components/CourseList';

export default function CoursesPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Courses</h1>
        <p className="text-muted-foreground">
          Group toolbox talks into structured courses with sequential completion and certificates.
        </p>
      </div>

      <Suspense fallback={<div className="text-muted-foreground">Loading...</div>}>
        <CourseList />
      </Suspense>
    </div>
  );
}
