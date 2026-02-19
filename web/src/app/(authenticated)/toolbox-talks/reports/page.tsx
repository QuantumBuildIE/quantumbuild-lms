'use client';

import Link from 'next/link';
import { AlertTriangle, CheckCircle, BarChart3, Users, UserX } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useMyOperators } from '@/lib/api/admin/use-supervisor-assignments';

const reportCards = [
  {
    title: 'Compliance Report',
    description: 'Compliance metrics with department and learning breakdowns for your assigned operators',
    icon: BarChart3,
    href: '/toolbox-talks/reports/compliance',
    color: 'text-blue-600',
  },
  {
    title: 'Overdue Report',
    description: 'Overdue learning assignments for your assigned operators',
    icon: AlertTriangle,
    href: '/toolbox-talks/reports/overdue',
    color: 'text-red-600',
  },
  {
    title: 'Completions Report',
    description: 'Completion records with quiz scores and timing for your assigned operators',
    icon: CheckCircle,
    href: '/toolbox-talks/reports/completions',
    color: 'text-green-600',
  },
];

export default function ToolboxTalksReportsPage() {
  const { data: operators, isLoading: operatorsLoading } = useMyOperators();
  const operatorCount = operators?.length ?? 0;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Team Reports</h1>
        <p className="text-muted-foreground">
          Compliance reports and analytics for your assigned operators
        </p>
      </div>

      {/* Operator summary */}
      <Card>
        <CardContent className="flex items-center gap-3 py-4">
          <Users className="h-5 w-5 text-muted-foreground" />
          {operatorsLoading ? (
            <Skeleton className="h-5 w-48" />
          ) : (
            <span className="text-sm">
              You have <Badge variant="secondary">{operatorCount}</Badge> assigned operator{operatorCount !== 1 ? 's' : ''}
            </span>
          )}
        </CardContent>
      </Card>

      {!operatorsLoading && operatorCount === 0 ? (
        <Card className="p-8">
          <div className="flex flex-col items-center gap-3 text-center">
            <UserX className="h-10 w-10 text-muted-foreground" />
            <div>
              <h3 className="font-medium">No operators assigned</h3>
              <p className="text-sm text-muted-foreground mt-1">
                Contact your administrator to have operators assigned to you, or visit your employee profile to manage assignments.
              </p>
            </div>
          </div>
        </Card>
      ) : (
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
      )}
    </div>
  );
}
