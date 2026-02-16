'use client';

import { AssignmentsList } from '@/features/toolbox-talks/components/AssignmentsList';

export default function AssignmentsListPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Assignments</h1>
        <p className="text-muted-foreground">
          Track all employee toolbox talk assignments
        </p>
      </div>

      <AssignmentsList />
    </div>
  );
}
