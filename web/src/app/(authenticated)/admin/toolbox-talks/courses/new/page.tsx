'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { CourseForm } from '@/features/toolbox-talks/components/CourseForm';
import { useCoursePreference } from '@/features/toolbox-talks/hooks/useCoursePreference';

export default function NewCoursePage() {
  const router = useRouter();
  const useNewCourse = useCoursePreference();

  useEffect(() => {
    if (!useNewCourse) {
      router.replace('/admin/toolbox-talks/create');
    }
  }, [useNewCourse, router]);

  if (!useNewCourse) {
    return null;
  }

  return <CourseForm />;
}
