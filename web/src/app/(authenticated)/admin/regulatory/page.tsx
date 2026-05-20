"use client";

import Link from "next/link";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { usePermission, useIsSuperUser } from "@/lib/auth/use-auth";
import {
  BookOpen,
  ShieldCheck,
  GitMerge,
  Layers,
  Settings2,
  ArrowRight,
} from "lucide-react";

const tenantCards = [
  {
    href: "/admin/regulatory/regulations",
    icon: BookOpen,
    title: "Regulations",
    description: "Browse approved regulatory requirements applicable to your sectors.",
  },
  {
    href: "/admin/regulatory/compliance",
    icon: ShieldCheck,
    title: "Compliance",
    description: "View your compliance coverage and requirement gaps across learning content.",
  },
  {
    href: "/admin/regulatory/mappings",
    icon: GitMerge,
    title: "Mappings",
    description: "Review and confirm AI-suggested mappings between content and requirements.",
  },
  {
    href: "/admin/regulatory/my-sectors",
    icon: Layers,
    title: "My Sectors",
    description: "View your assigned regulatory sectors and add new ones.",
  },
];

const superUserCard = {
  href: "/admin/regulatory/system",
  icon: Settings2,
  title: "System Administration",
  description: "Manage regulatory documents and AI-powered requirement ingestion.",
};

export default function RegulatoryLandingPage() {
  const hasLearningsAdmin = usePermission("Learnings.Admin");
  const isSuperUser = useIsSuperUser();

  if (!hasLearningsAdmin && !isSuperUser) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to access Regulatory.
      </div>
    );
  }

  const cards = isSuperUser
    ? [...tenantCards, superUserCard]
    : tenantCards;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Regulatory</h1>
        <p className="text-muted-foreground">
          Manage regulatory requirements and compliance for your organisation.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {cards.map((card) => {
          const Icon = card.icon;
          return (
            <Card key={card.href} className="hover:shadow-md transition-shadow">
              <CardHeader>
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
                    <Icon className="h-5 w-5" />
                  </div>
                  <div>
                    <CardTitle className="text-lg">{card.title}</CardTitle>
                    <CardDescription>{card.description}</CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <Link
                  href={card.href}
                  className="flex items-center gap-2 text-sm text-primary hover:underline"
                >
                  Open
                  <ArrowRight className="h-4 w-4" />
                </Link>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
