import { MyCoursesList } from '@/features/toolbox-talks/components/MyCoursesList';
import { MyTalksList } from '@/features/toolbox-talks/components/MyTalksList';

export default function MyToolboxTalksPage() {
  return (
    <div className="space-y-8">
      <MyCoursesList />
      <MyTalksList />
    </div>
  );
}
