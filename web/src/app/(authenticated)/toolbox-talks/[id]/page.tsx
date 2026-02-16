'use client';

import { useParams } from 'next/navigation';
import { TalkViewer } from '@/features/toolbox-talks/components/TalkViewer';

export default function ViewMyToolboxTalkPage() {
  const params = useParams();
  const scheduledTalkId = params.id as string;

  return <TalkViewer scheduledTalkId={scheduledTalkId} />;
}
