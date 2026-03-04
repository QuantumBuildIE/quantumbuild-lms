'use client';

import { useParams, useRouter } from 'next/navigation';
import { ValidationRunDetailView } from '@/features/toolbox-talks/components/ValidationRunDetailView';

export default function ValidationRunDetailPage() {
  const params = useParams();
  const router = useRouter();
  const talkId = params.id as string;
  const runId = params.runId as string;

  return (
    <div className="space-y-6">
      <ValidationRunDetailView
        talkId={talkId}
        runId={runId}
        onBack={() => router.push(`/admin/toolbox-talks/talks/${talkId}`)}
      />
    </div>
  );
}
