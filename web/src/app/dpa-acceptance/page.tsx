"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useAuth } from "@/lib/auth/use-auth";
import { apiClient } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { toast } from "sonner";
import { Download, Info, ChevronDown } from "lucide-react";

const SIGNATORY_ROLES = [
  "Director",
  "CEO",
  "CFO",
  "HR Manager",
  "Compliance Officer",
  "Operations Manager",
  "Other",
] as const;

const COUNTRIES = [
  "Ireland",
  "United Kingdom",
  "United States",
  "Germany",
  "France",
  "Netherlands",
  "Belgium",
  "Spain",
  "Italy",
  "Portugal",
  "Austria",
  "Switzerland",
  "Sweden",
  "Norway",
  "Denmark",
  "Finland",
  "Poland",
  "Czech Republic",
  "Australia",
  "Canada",
  "New Zealand",
  "South Africa",
  "United Arab Emirates",
  "Saudi Arabia",
  "India",
  "Singapore",
  "Japan",
  "Other",
] as const;

export default function DpaAcceptancePage() {
  const router = useRouter();
  const { isAuthenticated, isLoading, dpaAccepted, user, refreshDpaStatus } =
    useAuth();

  // Form state
  const [organisationLegalName, setOrganisationLegalName] = useState("");
  const [signatoryFullName, setSignatoryFullName] = useState("");
  const [signatoryRole, setSignatoryRole] = useState("");
  const [companyRegistrationNo, setCompanyRegistrationNo] = useState("");
  const [country, setCountry] = useState("Ireland");

  // Checkbox state
  const [readUnderstood, setReadUnderstood] = useState(false);
  const [authorisedToSign, setAuthorisedToSign] = useState(false);
  const [acknowledgeSubProcessors, setAcknowledgeSubProcessors] =
    useState(false);

  // Scroll tracking
  const [hasScrolledToBottom, setHasScrolledToBottom] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  // Submission
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Current timestamp for audit block
  const [timestamp, setTimestamp] = useState("");
  useEffect(() => {
    const update = () => setTimestamp(new Date().toISOString().replace("T", " ").slice(0, 19) + " UTC");
    update();
    const interval = setInterval(update, 1000);
    return () => clearInterval(interval);
  }, []);

  // Auth guards
  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push("/login");
    }
  }, [isLoading, isAuthenticated, router]);

  useEffect(() => {
    if (!isLoading && isAuthenticated && dpaAccepted) {
      router.push("/toolbox-talks");
    }
  }, [isLoading, isAuthenticated, dpaAccepted, router]);

  // Scroll handler
  const handleScroll = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 30;
    if (atBottom) setHasScrolledToBottom(true);
  }, []);

  const formComplete =
    organisationLegalName.trim() !== "" &&
    signatoryFullName.trim() !== "" &&
    signatoryRole !== "" &&
    country !== "";

  const canSubmit =
    formComplete &&
    hasScrolledToBottom &&
    readUnderstood &&
    authorisedToSign &&
    acknowledgeSubProcessors;

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setIsSubmitting(true);
    try {
      await apiClient.post("/dpa/accept", {
        organisationLegalName: organisationLegalName.trim(),
        signatoryFullName: signatoryFullName.trim(),
        signatoryRole,
        companyRegistrationNo: companyRegistrationNo.trim() || null,
        country,
      });
      await refreshDpaStatus();
      toast.success("DPA accepted successfully");
      router.push("/toolbox-talks");
    } catch {
      toast.error("Failed to record DPA acceptance. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-950">
        <div className="animate-pulse text-slate-500">Loading...</div>
      </div>
    );
  }

  if (!isAuthenticated || dpaAccepted) {
    return null;
  }

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950">
      {/* Header */}
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
          <span className="text-sm text-muted-foreground">
            {user?.email}
          </span>
        </div>
      </header>

      <main className="container mx-auto max-w-4xl px-4 py-8">
        <div className="space-y-6">
          {/* Page title */}
          <div className="space-y-2">
            <h1 className="text-2xl font-bold tracking-tight">
              Data Processing Agreement
            </h1>
            <p className="text-sm text-muted-foreground">
              Before using CertifiedIQ, your organisation must accept the Data
              Processing Agreement (DPA). This is a one-time requirement.
            </p>
          </div>

          <Card>
            <CardContent className="pt-6 space-y-6">
              {/* Organisation details form */}
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="orgName">
                    Organisation Legal Name <span className="text-red-500">*</span>
                  </Label>
                  <Input
                    id="orgName"
                    placeholder="e.g. Acme Construction Ltd"
                    value={organisationLegalName}
                    onChange={(e) => setOrganisationLegalName(e.target.value)}
                  />
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="sigName">
                      Your Full Name <span className="text-red-500">*</span>
                    </Label>
                    <Input
                      id="sigName"
                      placeholder="e.g. John Smith"
                      value={signatoryFullName}
                      onChange={(e) => setSignatoryFullName(e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="sigRole">
                      Your Role / Title <span className="text-red-500">*</span>
                    </Label>
                    <Select value={signatoryRole} onValueChange={setSignatoryRole}>
                      <SelectTrigger id="sigRole">
                        <SelectValue placeholder="Select role" />
                      </SelectTrigger>
                      <SelectContent>
                        {SIGNATORY_ROLES.map((role) => (
                          <SelectItem key={role} value={role}>
                            {role}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="regNo">Company Registration No.</Label>
                    <Input
                      id="regNo"
                      placeholder="Optional"
                      value={companyRegistrationNo}
                      onChange={(e) => setCompanyRegistrationNo(e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="country">
                      Country <span className="text-red-500">*</span>
                    </Label>
                    <Select value={country} onValueChange={setCountry}>
                      <SelectTrigger id="country">
                        <SelectValue placeholder="Select country" />
                      </SelectTrigger>
                      <SelectContent>
                        {COUNTRIES.map((c) => (
                          <SelectItem key={c} value={c}>
                            {c}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                <Alert className="border-blue-200 bg-blue-50 dark:border-blue-800 dark:bg-blue-950/50">
                  <Info className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                  <AlertDescription className="text-sm text-blue-800 dark:text-blue-300">
                    By accepting, you confirm you are authorised to sign legal
                    agreements on behalf of your organisation. The acceptance is
                    logged with a timestamp and your IP address as a legally
                    binding record.
                  </AlertDescription>
                </Alert>
              </div>

              <Separator />

              {/* DPA scrollable text */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <Label className="text-base font-semibold">
                    Data Processing Agreement — v1.0, March 2026
                  </Label>
                  {!hasScrolledToBottom && (
                    <span className="flex items-center gap-1 text-xs text-muted-foreground animate-pulse">
                      <ChevronDown className="h-3 w-3" />
                      Scroll to read
                    </span>
                  )}
                </div>
                <div
                  ref={scrollRef}
                  onScroll={handleScroll}
                  className="h-80 overflow-y-auto rounded-md border bg-muted/30 p-4 text-sm leading-relaxed space-y-4"
                >
                  <p className="font-semibold text-base">
                    DATA PROCESSING AGREEMENT
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Between the Controller (the organisation accepting this
                    agreement) and the Processor (Richmond IT Services Ltd,
                    trading as CertifiedIQ).
                  </p>

                  <p>[Full DPA text will be rendered here]</p>

                  <div className="pt-4 border-t text-xs text-muted-foreground">
                    <p>
                      For the full legally-binding document, please download the
                      PDF version using the button below.
                    </p>
                  </div>
                </div>
              </div>

              {/* Checkboxes */}
              <div className="space-y-3">
                <div className="flex items-start space-x-3">
                  <Checkbox
                    id="cb1"
                    checked={readUnderstood}
                    onCheckedChange={(v) => setReadUnderstood(v === true)}
                    disabled={!hasScrolledToBottom}
                  />
                  <Label
                    htmlFor="cb1"
                    className={`text-sm font-normal leading-relaxed cursor-pointer ${
                      !hasScrolledToBottom ? "text-muted-foreground" : ""
                    }`}
                  >
                    I confirm I have read and understood the Data Processing
                    Agreement in full.
                  </Label>
                </div>
                <div className="flex items-start space-x-3">
                  <Checkbox
                    id="cb2"
                    checked={authorisedToSign}
                    onCheckedChange={(v) => setAuthorisedToSign(v === true)}
                    disabled={!hasScrolledToBottom}
                  />
                  <Label
                    htmlFor="cb2"
                    className={`text-sm font-normal leading-relaxed cursor-pointer ${
                      !hasScrolledToBottom ? "text-muted-foreground" : ""
                    }`}
                  >
                    I confirm I am authorised to sign legal agreements on behalf
                    of the organisation named above, and I accept this DPA on
                    its behalf.
                  </Label>
                </div>
                <div className="flex items-start space-x-3">
                  <Checkbox
                    id="cb3"
                    checked={acknowledgeSubProcessors}
                    onCheckedChange={(v) =>
                      setAcknowledgeSubProcessors(v === true)
                    }
                    disabled={!hasScrolledToBottom}
                  />
                  <Label
                    htmlFor="cb3"
                    className={`text-sm font-normal leading-relaxed cursor-pointer ${
                      !hasScrolledToBottom ? "text-muted-foreground" : ""
                    }`}
                  >
                    I acknowledge and authorise the use of the Sub-Processors
                    listed in this Agreement, including processors located in
                    the United States under Standard Contractual Clauses.
                  </Label>
                </div>
              </div>

              {/* Audit block */}
              <div className="rounded-md border bg-slate-100 dark:bg-slate-900 p-3 font-mono text-xs space-y-1 text-muted-foreground">
                <p>
                  Timestamp: {timestamp} &nbsp;&nbsp; IP: recorded server-side
                </p>
                <p>
                  DPA Version: v1.0 &nbsp;&nbsp; Processor: Richmond IT Services
                  Ltd
                </p>
              </div>

              {/* Footer buttons */}
              <div className="flex items-center justify-between pt-2">
                <Button variant="outline" asChild>
                  <a href="/documents/dpa-v1.pdf" download>
                    <Download className="mr-2 h-4 w-4" />
                    Download DPA (PDF)
                  </a>
                </Button>
                <Button
                  onClick={handleSubmit}
                  disabled={!canSubmit || isSubmitting}
                >
                  {isSubmitting ? "Submitting..." : "Accept & Continue →"}
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}
