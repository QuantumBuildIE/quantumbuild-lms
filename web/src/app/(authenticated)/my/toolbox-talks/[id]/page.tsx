'use client';

import { useParams } from 'next/navigation';
import { TalkPreviewPage } from '@/features/toolbox-talks/components/TalkPreviewPage';

export default function TalkPreviewRoute() {
  const params = useParams();
  const talkId = params.id as string;

  return <TalkPreviewPage talkId={talkId} />;
}
