"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { format } from "date-fns";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { FlaggedText } from "@/features/toolbox-talks/components/create-wizard/steps/validate/FlaggedText";
import { FlagSeverity } from "@/types/content-creation";
import type { TranslationFlag } from "@/types/content-creation";
import { DeclineConfirmationDialog } from "@/features/external-review/components/DeclineConfirmationDialog";
import {
  getExternalReviewPortal,
  submitExternalReview,
  declineExternalReview,
} from "@/lib/api/external-review";
import type {
  ExternalReviewPortalDto,
  ExternalReviewFlagDto,
  ExternalReviewEditedSectionDto,
} from "@/types/external-review";

// ── Types ─────────────────────────────────────────────────────────────────────

type Step =
  | "loading"
  | "active"
  | "used"
  | "revoked"
  | "expired"
  | "not-found"
  | "submitted"
  | "declined"
  | "error";

// ── Helpers ───────────────────────────────────────────────────────────────────

function mapPortalStatus(portalStatus: string): Step {
  switch (portalStatus.toLowerCase()) {
    case "active":  return "active";
    case "used":    return "used";
    case "revoked": return "revoked";
    case "expired": return "expired";
    default:        return "error";
  }
}

function adaptFlags(flags: ExternalReviewFlagDto[]): TranslationFlag[] {
  return flags.map((f, i) => ({
    id: `external-${i}`,
    startOffset: f.startOffset,
    endOffset: f.endOffset,
    severity: f.severity as FlagSeverity,
    reason: f.reason,
    createdAt: new Date().toISOString(),
  }));
}

function LoadingSpinner({ className }: { className?: string }) {
  return (
    <svg
      className={`animate-spin ${className ?? ""}`}
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function ExternalReviewPage() {
  const params = useParams();
  const token = params.token as string;

  const [step, setStep] = useState<Step>("loading");
  const [portalData, setPortalData] = useState<ExternalReviewPortalDto | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [editedSections, setEditedSections] = useState<Record<number, string>>({});
  const [submitDialogOpen, setSubmitDialogOpen] = useState(false);
  const [declineDialogOpen, setDeclineDialogOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    async function loadPortal() {
      try {
        const { status, body } = await getExternalReviewPortal(token);
        if (status === 200 || status === 410) {
          setPortalData(body);
          setStep(mapPortalStatus(body.portalStatus));
          if (body.portalStatus === "Active") {
            const initial: Record<number, string> = {};
            for (const section of body.sections) {
              initial[section.sectionIndex] = section.translatedText;
            }
            setEditedSections(initial);
          }
        } else if (status === 404) {
          setStep("not-found");
        } else {
          setErrorMsg(
            "Something went wrong. Please try again later or contact the person who invited you."
          );
          setStep("error");
        }
      } catch {
        setErrorMsg("Unable to load. Check your connection and try again.");
        setStep("error");
      }
    }
    loadPortal();
  }, [token]);

  async function handleSubmit() {
    if (!portalData) return;
    setIsSubmitting(true);
    try {
      const editedArray: ExternalReviewEditedSectionDto[] = Object.entries(editedSections).map(
        ([idx, text]) => ({
          sectionIndex: parseInt(idx, 10),
          translatedText: text,
        })
      );
      const editedContent = JSON.stringify(editedArray);
      const { status } = await submitExternalReview(token, { accepted: true, editedContent });
      if (status === 200) {
        setStep("submitted");
        setSubmitDialogOpen(false);
      } else {
        toast.error("Failed to submit. Please try again.");
      }
    } catch {
      toast.error("Network error. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDecline(reason: string) {
    setIsSubmitting(true);
    try {
      const { status } = await declineExternalReview(token, { reason });
      if (status === 200) {
        setStep("declined");
        setDeclineDialogOpen(false);
      } else if (status === 400) {
        toast.error("Reason is required.");
      } else {
        toast.error("Failed to decline. Please try again.");
      }
    } catch {
      toast.error("Network error. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">

      {/* Header */}
      <header className="bg-white border-b px-4 py-3 flex items-center gap-3">
        <svg viewBox="0 0 46 46" fill="none" className="w-8 h-8 shrink-0">
          <circle
            cx="23" cy="23" r="21"
            fill="#4d8eff" fillOpacity="0.1"
            stroke="#4d8eff" strokeWidth="1.5" strokeOpacity="0.3"
          />
          <path
            d="M23 10V23L30 30"
            stroke="#4d8eff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"
          />
          <circle cx="23" cy="23" r="3" fill="#4d8eff" />
        </svg>
        <span className="font-bold text-gray-900">
          Certified<span className="text-blue-600">IQ</span>
        </span>
        <span className="ml-auto text-sm text-gray-500">Translation Review</span>
      </header>

      <main className="flex-1 p-4 pt-8 pb-12">
        <div className="w-full max-w-5xl mx-auto">

          {/* ── Loading ── */}
          {step === "loading" && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center text-gray-500">
                Loading...
              </div>
            </div>
          )}

          {/* ── Error ── */}
          {step === "error" && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center">
                <div className="text-4xl mb-4">⚠️</div>
                <p className="text-gray-700">
                  {errorMsg ??
                    "Something went wrong. Please try again later or contact the person who invited you."}
                </p>
              </div>
            </div>
          )}

          {/* ── Not Found ── */}
          {step === "not-found" && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center">
                <div className="text-4xl mb-4">🔗</div>
                <h2 className="text-lg font-semibold text-gray-900 mb-2">
                  This review link is no longer valid.
                </h2>
                <p className="text-sm text-gray-500">
                  The link may have expired, been revoked, or never existed. Please contact the
                  person who sent it.
                </p>
              </div>
            </div>
          )}

          {/* ── Used ── */}
          {step === "used" && portalData && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center">
                <div className="text-4xl mb-4">✅</div>
                <h2 className="text-lg font-semibold text-gray-900 mb-2">
                  You&apos;ve already submitted this review.
                </h2>
                <p className="text-gray-600 mb-1">{portalData.talkTitle}</p>
                <p className="text-sm text-gray-500">
                  Your review has been recorded. Thank you for your contribution.
                </p>
              </div>
            </div>
          )}

          {/* ── Revoked ── */}
          {step === "revoked" && portalData && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center">
                <div className="text-4xl mb-4">🚫</div>
                <h2 className="text-lg font-semibold text-gray-900 mb-2">
                  This review invitation has been cancelled.
                </h2>
                <p className="text-gray-600">{portalData.talkTitle}</p>
              </div>
            </div>
          )}

          {/* ── Expired ── */}
          {step === "expired" && portalData && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center">
                <div className="text-4xl mb-4">⏰</div>
                <h2 className="text-lg font-semibold text-gray-900 mb-2">
                  This review invitation has expired.
                </h2>
                <p className="text-gray-600 mb-1">{portalData.talkTitle}</p>
                <p className="text-sm text-gray-500">
                  Expired on {format(new Date(portalData.expiresAt), "dd MMM yyyy")}
                </p>
              </div>
            </div>
          )}

          {/* ── Submitted ── */}
          {step === "submitted" && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center">
                <div className="text-5xl mb-4">✅</div>
                <h2 className="text-xl font-bold text-green-700 mb-2">
                  Thank you. Your review has been submitted.
                </h2>
                <p className="text-sm text-gray-500">
                  The team will be notified and your feedback will be applied.
                </p>
              </div>
            </div>
          )}

          {/* ── Declined ── */}
          {step === "declined" && (
            <div className="max-w-lg mx-auto">
              <div className="bg-white rounded-xl shadow p-8 text-center">
                <div className="text-5xl mb-4">👋</div>
                <h2 className="text-xl font-bold text-gray-800 mb-2">
                  You declined this review. Thank you for letting us know.
                </h2>
                <p className="text-sm text-gray-500">The requester has been notified.</p>
              </div>
            </div>
          )}

          {/* ── Active ── */}
          {step === "active" && portalData && (
            <div className="space-y-6">

              {/* Header card */}
              <div className="bg-white rounded-xl shadow p-6">
                <h1 className="text-2xl font-bold text-gray-900 mb-1">{portalData.talkTitle}</h1>
                <p className="text-sm text-gray-500">Translation: {portalData.languageName}</p>
                <p className="text-xs text-gray-400 mt-1">
                  This invitation expires on{" "}
                  {format(new Date(portalData.expiresAt), "dd MMM yyyy")}
                </p>
              </div>

              {/* Sections */}
              {portalData.sections.map((section) => (
                <div key={section.sectionIndex} className="bg-white rounded-xl shadow p-6">
                  <h2 className="text-base font-semibold text-gray-900 mb-4">
                    {section.sectionTitle}
                  </h2>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {/* Source panel — read-only with flag highlighting */}
                    <div>
                      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">
                        Source
                      </p>
                      <div className="text-sm text-gray-700 leading-relaxed border rounded-md p-3 bg-gray-50 min-h-[150px]">
                        <FlaggedText
                          text={section.originalText}
                          flags={adaptFlags(section.flags)}
                        />
                      </div>
                    </div>
                    {/* Translation panel — editable */}
                    <div>
                      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">
                        Translation
                      </p>
                      <Textarea
                        value={editedSections[section.sectionIndex] ?? ""}
                        onChange={(e) =>
                          setEditedSections((prev) => ({
                            ...prev,
                            [section.sectionIndex]: e.target.value,
                          }))
                        }
                        className="min-h-[150px] text-sm"
                      />
                    </div>
                  </div>
                </div>
              ))}

              {/* Action buttons */}
              <div className="flex flex-col sm:flex-row gap-3 justify-end pb-8">
                <Button
                  variant="outline"
                  size="lg"
                  onClick={() => setDeclineDialogOpen(true)}
                  disabled={isSubmitting}
                >
                  Decline
                </Button>
                <Button
                  size="lg"
                  onClick={() => setSubmitDialogOpen(true)}
                  disabled={isSubmitting}
                >
                  Submit
                </Button>
              </div>

            </div>
          )}

        </div>
      </main>

      {/* ── Submit confirmation dialog (inlined — single use site) ── */}
      <Dialog open={submitDialogOpen} onOpenChange={setSubmitDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Submit your review</DialogTitle>
            <DialogDescription>
              You&apos;re approving the translation as it appears. Once submitted, this can&apos;t
              be changed.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setSubmitDialogOpen(false)}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button type="button" onClick={handleSubmit} disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <LoadingSpinner className="mr-2 h-4 w-4" />
                  Submitting...
                </>
              ) : (
                "Yes, submit"
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Decline confirmation dialog ── */}
      <DeclineConfirmationDialog
        open={declineDialogOpen}
        onOpenChange={setDeclineDialogOpen}
        onConfirm={handleDecline}
        isLoading={isSubmitting}
      />

    </div>
  );
}
