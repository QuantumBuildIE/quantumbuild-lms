import { redirect } from 'next/navigation';

export default function LegacyNewTalkPage() {
  redirect('/admin/toolbox-talks/create');
}
