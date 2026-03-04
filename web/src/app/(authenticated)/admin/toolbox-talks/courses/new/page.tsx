import { redirect } from 'next/navigation';

export default function LegacyNewCoursePage() {
  redirect('/admin/toolbox-talks/create');
}
