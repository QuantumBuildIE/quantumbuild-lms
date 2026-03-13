import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import {
  CheckCircle,
  Download,
  ShieldCheck,
  Bot,
  BookOpen,
  FileText,
} from "lucide-react";

export const metadata = {
  title: "CertifiedIQ — AI System Card",
  description:
    "EU AI Act Compliance Document for CertifiedIQ AI System (Regulation (EU) 2024/1689)",
};

const systemOverview = [
  { label: "System Name", value: "CertifiedIQ" },
  { label: "Provider", value: "Quantum Build AI (Ireland)" },
  { label: "Version", value: "1.0" },
  {
    label: "Intended Purpose",
    value:
      "AI-assisted translation, transcription, and training delivery for workplace safety compliance",
  },
  {
    label: "Target Sectors",
    value: "Construction, Mining, Manufacturing, Energy, Logistics",
  },
  { label: "Deployment Model", value: "Cloud-hosted SaaS (multi-tenant)" },
  {
    label: "AI Models Used",
    value:
      "Claude (Anthropic) for translation & content generation, ElevenLabs for transcription, DeepL / Gemini / DeepSeek for back-translation validation",
  },
];

const doesItems = [
  "Translates safety training content into multiple languages",
  "Produces quality scores for translations via back-translation consensus",
  "Transcribes audio/video into subtitle files",
  "Delivers structured training with quizzes and certificates",
  "Generates a full audit trail for compliance reporting",
];

const doesNotItems = [
  "Make autonomous safety decisions or replace human judgement",
  "Profile, score, or categorise individual workers",
  "Operate machinery, control physical systems, or interact with the real world",
  "Provide legal, medical, or engineering advice",
  "Process biometric data or perform facial recognition",
];

const complianceItems = [
  {
    label: "Risk Classification",
    description:
      "Classified as Limited Risk under Article 50 — not high-risk, not prohibited",
  },
  {
    label: "Transparency Obligation",
    description:
      "Users are clearly informed that content is AI-generated at every interaction point",
  },
  {
    label: "Human Oversight",
    description:
      "All AI outputs are subject to human review, editing, and approval before use",
  },
  {
    label: "Data Protection",
    description:
      "GDPR-compliant processing; no personal data used for model training; tenant-isolated",
  },
  {
    label: "Audit Trail",
    description:
      "Complete validation history with back-translation scores, reviewer decisions, and timestamped reports",
  },
  {
    label: "Safety-Critical Content Handling",
    description:
      "Glossary verification and elevated thresholds for safety-critical sections (prohibition, emergency, hazard)",
  },
  {
    label: "Provider Documentation",
    description:
      "This AI System Card, technical documentation, and risk assessment maintained and versioned",
  },
];

const timelineEntries = [
  {
    date: "2 February 2025",
    label: "Prohibited AI Practices",
    description: "Prohibitions on unacceptable-risk AI systems took effect",
    status: "Compliant — no prohibited practices used",
  },
  {
    date: "2 August 2025",
    label: "General-Purpose AI & Governance",
    description:
      "Obligations for GPAI providers and national authority designation",
    status: "Compliant — transparency obligations met",
  },
  {
    date: "2 August 2026",
    label: "Full Enforcement",
    description:
      "All remaining provisions including high-risk system requirements",
    status: "On track — limited-risk classification confirmed",
  },
];

export default function AISystemCardPage() {
  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950">
      {/* Header bar */}
      <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="container flex h-14 items-center justify-between px-4">
          <Link
            href="/"
            className="flex items-center gap-2 hover:opacity-80 transition-opacity"
          >
            <svg viewBox="0 0 46 46" fill="none" className="w-8 h-8">
              <circle
                cx="23"
                cy="23"
                r="21"
                fill="#4d8eff"
                fillOpacity="0.1"
                stroke="#4d8eff"
                strokeWidth="1.5"
                strokeOpacity="0.3"
              />
              <path
                d="M23 10V23L30 30"
                stroke="#4d8eff"
                strokeWidth="2.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle cx="23" cy="23" r="3" fill="#4d8eff" />
              <circle cx="23" cy="7" r="2" fill="#4d8eff" opacity="0.6" />
              <circle cx="39" cy="23" r="2" fill="#4d8eff" opacity="0.6" />
              <circle cx="23" cy="39" r="2" fill="#4d8eff" opacity="0.6" />
              <circle cx="7" cy="23" r="2" fill="#4d8eff" opacity="0.6" />
            </svg>
            <span className="text-lg font-bold tracking-tight">
              Certified<span className="text-primary font-extrabold">IQ</span>
            </span>
          </Link>
          <Button asChild variant="outline" size="sm">
            <a href="/documents/ciq-aisc-001.pdf" download>
              <Download className="mr-2 h-4 w-4" />
              Download PDF
            </a>
          </Button>
        </div>
      </header>

      <main className="container mx-auto max-w-4xl px-4 py-8 space-y-8">
        {/* Page Header */}
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <ShieldCheck className="h-8 w-8 text-primary" />
            <h1 className="text-3xl font-bold tracking-tight">
              CertifiedIQ — AI System Card
            </h1>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="secondary">Version 1.0</Badge>
            <Badge variant="secondary">March 2026</Badge>
            <Badge variant="secondary">CIQ-AISC-001</Badge>
            <Badge className="bg-blue-100 text-blue-800 hover:bg-blue-100 dark:bg-blue-900 dark:text-blue-200">
              LIMITED RISK
            </Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            EU AI Act Compliance Document · Regulation (EU) 2024/1689
          </p>
        </div>

        <Separator />

        {/* Section 1 — System Overview */}
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center gap-2">
              <Bot className="h-5 w-5 text-primary" />
              <h2 className="text-xl font-semibold">1. System Overview</h2>
            </div>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-4">
              {systemOverview.map((item) => (
                <div key={item.label} className="space-y-0.5">
                  <dt className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
                    {item.label}
                  </dt>
                  <dd className="text-sm">{item.value}</dd>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* Section 2 — What the AI Does and Does Not Do */}
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center gap-2">
              <BookOpen className="h-5 w-5 text-primary" />
              <h2 className="text-xl font-semibold">
                2. What the AI Does and Does Not Do
              </h2>
            </div>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="space-y-3">
                <h3 className="text-sm font-semibold text-green-700 dark:text-green-400 uppercase tracking-wider">
                  What It Does
                </h3>
                <ul className="space-y-2">
                  {doesItems.map((item) => (
                    <li key={item} className="flex items-start gap-2 text-sm">
                      <CheckCircle className="h-4 w-4 mt-0.5 shrink-0 text-green-600 dark:text-green-400" />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              </div>
              <div className="space-y-3">
                <h3 className="text-sm font-semibold text-red-700 dark:text-red-400 uppercase tracking-wider">
                  What It Does NOT Do
                </h3>
                <ul className="space-y-2">
                  {doesNotItems.map((item) => (
                    <li
                      key={item}
                      className="flex items-start gap-2 text-sm text-muted-foreground"
                    >
                      <span className="mt-0.5 shrink-0 h-4 w-4 flex items-center justify-center text-red-500 font-bold">
                        ✕
                      </span>
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Section 3 — EU AI Act Compliance Status */}
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center gap-2">
              <ShieldCheck className="h-5 w-5 text-primary" />
              <h2 className="text-xl font-semibold">
                3. EU AI Act Compliance Status
              </h2>
            </div>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {complianceItems.map((item) => (
                <div key={item.label} className="flex items-start gap-3">
                  <CheckCircle className="h-5 w-5 mt-0.5 shrink-0 text-green-600 dark:text-green-400" />
                  <div className="space-y-0.5">
                    <p className="text-sm font-medium">{item.label}</p>
                    <p className="text-sm text-muted-foreground">
                      {item.description}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* Section 4 — Transparency Statement */}
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center gap-2">
              <FileText className="h-5 w-5 text-primary" />
              <h2 className="text-xl font-semibold">
                4. Transparency Statement
              </h2>
            </div>
          </CardHeader>
          <CardContent>
            <div className="border-l-4 border-primary bg-muted/50 rounded-r-lg p-4 space-y-2">
              <p className="text-sm font-medium italic">
                &ldquo;In accordance with Article 50 of Regulation (EU)
                2024/1689 (the EU AI Act), we inform all users that content
                generated, translated, or transcribed within CertifiedIQ is
                produced with the assistance of artificial intelligence. All
                AI-generated outputs are clearly labelled and subject to human
                review before deployment in safety-critical training
                materials.&rdquo;
              </p>
              <p className="text-xs text-muted-foreground">
                — CertifiedIQ Transparency Disclosure, March 2026
              </p>
            </div>
          </CardContent>
        </Card>

        {/* Section 5 — Regulatory Timeline */}
        <Card>
          <CardHeader className="pb-3">
            <h2 className="text-xl font-semibold">5. Regulatory Timeline</h2>
          </CardHeader>
          <CardContent>
            <div className="relative space-y-0">
              {/* Vertical line */}
              <div className="absolute left-[7px] top-2 bottom-2 w-0.5 bg-border" />

              {timelineEntries.map((entry, idx) => (
                <div key={idx} className="relative pl-8 pb-6 last:pb-0">
                  <div className="absolute left-0 top-1.5 h-4 w-4 rounded-full border-2 border-primary bg-background" />
                  <div className="space-y-1">
                    <p className="text-xs font-semibold uppercase tracking-wider text-primary">
                      {entry.date}
                    </p>
                    <p className="text-sm font-medium">{entry.label}</p>
                    <p className="text-sm text-muted-foreground">
                      {entry.description}
                    </p>
                    <Badge
                      variant="outline"
                      className="text-xs text-green-700 border-green-300 dark:text-green-400 dark:border-green-700"
                    >
                      {entry.status}
                    </Badge>
                  </div>
                </div>
              ))}

              {/* Review date */}
              <div className="relative pl-8 pt-2">
                <div className="absolute left-0 top-3.5 h-4 w-4 rounded-full border-2 border-muted-foreground bg-background" />
                <div className="space-y-1">
                  <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                    Next Review
                  </p>
                  <p className="text-sm font-medium">September 2026</p>
                  <p className="text-sm text-muted-foreground">
                    Scheduled review of this AI System Card and compliance
                    posture
                  </p>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <Separator />

        {/* Footer */}
        <footer className="text-center space-y-2 pb-8">
          <p className="text-sm font-medium">Quantum Build AI</p>
          <p className="text-xs text-muted-foreground">
            certifiediq.ai · Donal Richmond, Founder · County Wexford, Ireland ·
            March 2026
          </p>
        </footer>
      </main>
    </div>
  );
}
