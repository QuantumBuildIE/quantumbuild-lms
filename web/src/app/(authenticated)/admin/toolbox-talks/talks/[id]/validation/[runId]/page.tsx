'use client';

import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { ValidationRunDetailView } from '@/features/toolbox-talks/components/ValidationRunDetailView';

export default function ValidationRunDetailPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const talkId = params.id as string;
  const runId = params.runId as string;
  const fromWizard = searchParams.get('from') === 'wizard';

  return (
    <div className="space-y-6">
      <ValidationRunDetailView
        talkId={talkId}
        runId={runId}
        onBack={() =>
          router.push(
            fromWizard
              ? `/admin/toolbox-talks/learnings/${talkId}/validate`
              : `/admin/toolbox-talks/talks/${talkId}`
          )
        }
      />
    </div>
  );
}
