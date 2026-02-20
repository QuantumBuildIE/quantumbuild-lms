'use client';

import Link from 'next/link';
import { AlertTriangle, CheckCircle, BarChart3, Grid3X3 } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';

const reportCards = [
  {
    title: 'Compliance Report',
    description: 'Overall compliance metrics with department and talk breakdowns',
    icon: BarChart3,
    href: '/admin/toolbox-talks/reports/compliance',
    color: 'text-blue-600',
  },
  {
    title: 'Overdue Report',
    description: 'View and manage overdue learning assignments',
    icon: AlertTriangle,
    href: '/admin/toolbox-talks/reports/overdue',
    color: 'text-red-600',
  },
  {
    title: 'Completions Report',
    description: 'Detailed completion records with quiz scores and timing',
    icon: CheckCircle,
    href: '/admin/toolbox-talks/reports/completions',
    color: 'text-green-600',
  },
  {
    title: 'Skills Matrix',
    description: 'Employee-by-learning grid showing completion status across the organisation',
    icon: Grid3X3,
    href: '/admin/toolbox-talks/reports/skills-matrix',
    color: 'text-purple-600',
  },
];

export default function AdminToolboxTalksReportsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Learnings Reports</h1>
        <p className="text-muted-foreground">
          Compliance reports and analytics for learning training
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {reportCards.map((card) => {
          const Icon = card.icon;
          return (
            <Link key={card.href} href={card.href}>
              <Card className="h-full transition-colors hover:bg-muted/50 cursor-pointer">
                <CardHeader>
                  <div className="flex items-center gap-3">
                    <div className={`p-2 rounded-lg bg-muted ${card.color}`}>
                      <Icon className="h-5 w-5" />
                    </div>
                    <CardTitle className="text-lg">{card.title}</CardTitle>
                  </div>
                </CardHeader>
                <CardContent>
                  <CardDescription>{card.description}</CardDescription>
                </CardContent>
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
