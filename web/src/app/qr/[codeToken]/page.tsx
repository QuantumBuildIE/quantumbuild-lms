"use client";

import { useEffect, useRef, useState } from "react";
import { useParams } from "next/navigation";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5222/api";

const SUPPORTED_LANGUAGES = [
  { code: "en", label: "English" },
  { code: "es", label: "Español" },
  { code: "fr", label: "Français" },
  { code: "pl", label: "Polski" },
  { code: "ro", label: "Română" },
  { code: "uk", label: "Українська" },
  { code: "pt", label: "Português" },
  { code: "lt", label: "Lietuvių" },
  { code: "de", label: "Deutsch" },
  { code: "lv", label: "Latviešu" },
];

function detectBrowserLanguage(): string {
  if (typeof navigator === "undefined") return "en";
  const lang = (navigator.language ?? "en").slice(0, 2).toLowerCase();
  return SUPPORTED_LANGUAGES.some((l) => l.code === lang) ? lang : "en";
}

// ── Types ─────────────────────────────────────────────────────────────────────

interface CodeInfo {
  codeToken: string;
  locationName: string;
  talkId: string | null;
  talkTitle: string | null;
  contentMode: string;
}

interface SessionSection {
  sectionId: string;
  sectionNumber: number;
  title: string;
  content: string;
  requiresAcknowledgment: boolean;
}

interface SessionQuestion {
  questionId: string;
  questionNumber: number;
  questionText: string;
  questionType: string;
  options: string[] | null;
  correctOptionIndex: number | null;
  points: number;
}

interface SessionTalk {
  id: string;
  title: string;
  description: string | null;
  videoUrl: string | null;
  requiresQuiz: boolean;
  passingScore: number;
  sections: SessionSection[];
  questions: SessionQuestion[];
}

interface SessionData {
  sessionToken: string;
  status: string;
  contentMode: string;
  language: string;
  startedAt: string;
  employeeName: string;
  locationName: string;
  talk: SessionTalk | null;
}

// ── PIN Input ─────────────────────────────────────────────────────────────────

function PinInput({
  onSubmit,
  disabled,
}: {
  onSubmit: (pin: string) => void;
  disabled: boolean;
}) {
  const [digits, setDigits] = useState<string[]>(Array(6).fill(""));
  const refs = useRef<Array<HTMLInputElement | null>>(Array(6).fill(null));

  const handleChange = (index: number, value: string) => {
    const digit = value.replace(/\D/g, "").slice(-1);
    const next = [...digits];
    next[index] = digit;
    setDigits(next);
    if (digit && index < 5) {
      refs.current[index + 1]?.focus();
    }
  };

  const handleKeyDown = (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Backspace" && !digits[index] && index > 0) {
      refs.current[index - 1]?.focus();
    }
    if (e.key === "Enter") {
      const pin = digits.join("");
      if (pin.length === 6) onSubmit(pin);
    }
  };

  const handlePaste = (e: React.ClipboardEvent) => {
    const text = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6);
    if (!text) return;
    e.preventDefault();
    const next = Array(6).fill("");
    for (let i = 0; i < text.length; i++) next[i] = text[i];
    setDigits(next);
    refs.current[Math.min(text.length, 5)]?.focus();
  };

  const pin = digits.join("");

  return (
    <div className="flex flex-col items-center gap-6">
      <div className="flex gap-3" onPaste={handlePaste}>
        {digits.map((d, i) => (
          <input
            key={i}
            ref={(el) => { refs.current[i] = el; }}
            type="text"
            inputMode="numeric"
            maxLength={1}
            value={d}
            onChange={(e) => handleChange(i, e.target.value)}
            onKeyDown={(e) => handleKeyDown(i, e)}
            disabled={disabled}
            className="w-12 h-14 text-center text-2xl font-bold border-2 rounded-lg border-gray-300 focus:border-blue-500 focus:outline-none disabled:opacity-50 bg-white"
          />
        ))}
      </div>
      <button
        onClick={() => onSubmit(pin)}
        disabled={disabled || pin.length !== 6}
        className="w-full max-w-xs py-3 bg-blue-600 text-white rounded-lg font-semibold disabled:opacity-50 hover:bg-blue-700 transition-colors"
      >
        {disabled ? "Verifying..." : "Enter"}
      </button>
    </div>
  );
}

// ── Quiz Component ────────────────────────────────────────────────────────────

function Quiz({
  questions,
  passingScore,
  contentMode,
  onPass,
}: {
  questions: SessionQuestion[];
  passingScore: number;
  contentMode: string;
  onPass: (score: number) => void;
}) {
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [submitted, setSubmitted] = useState(false);
  const [score, setScore] = useState(0);

  const handleSubmit = () => {
    let correct = 0;
    let total = 0;
    for (const q of questions) {
      total += q.points;
      if (q.correctOptionIndex !== null && answers[q.questionId] === q.correctOptionIndex) {
        correct += q.points;
      }
    }
    const pct = total > 0 ? Math.round((correct / total) * 100) : 0;
    setScore(pct);
    setSubmitted(true);
    if (contentMode !== "Induction" || pct >= passingScore) {
      onPass(pct);
    }
  };

  const handleRetry = () => {
    setAnswers({});
    setSubmitted(false);
    setScore(0);
  };

  const allAnswered = questions.every((q) => answers[q.questionId] !== undefined);

  if (submitted && contentMode === "Induction" && score < passingScore) {
    return (
      <div className="text-center py-8">
        <div className="text-4xl mb-4">❌</div>
        <p className="text-lg font-semibold text-red-600 mb-2">
          Score: {score}% — Pass required: {passingScore}%
        </p>
        <p className="text-gray-600 mb-6">Please review the content and try again.</p>
        <button
          onClick={handleRetry}
          className="px-6 py-3 bg-blue-600 text-white rounded-lg font-semibold hover:bg-blue-700 transition-colors"
        >
          Try Again
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {questions.map((q, qi) => (
        <div key={q.questionId} className="bg-white border rounded-lg p-5">
          <p className="font-medium mb-4">
            {qi + 1}. {q.questionText}
          </p>
          {q.options?.map((opt, oi) => (
            <label
              key={oi}
              className={`flex items-center gap-3 p-3 rounded-lg mb-2 cursor-pointer border transition-colors ${
                answers[q.questionId] === oi
                  ? "border-blue-500 bg-blue-50"
                  : "border-gray-200 hover:bg-gray-50"
              } ${submitted ? "cursor-default" : ""}`}
            >
              <input
                type="radio"
                name={q.questionId}
                value={oi}
                checked={answers[q.questionId] === oi}
                onChange={() => !submitted && setAnswers((a) => ({ ...a, [q.questionId]: oi }))}
                disabled={submitted}
                className="accent-blue-600"
              />
              <span>{opt}</span>
              {submitted && q.correctOptionIndex === oi && (
                <span className="ml-auto text-green-600 font-bold">✓</span>
              )}
            </label>
          ))}
        </div>
      ))}

      {!submitted ? (
        <button
          onClick={handleSubmit}
          disabled={!allAnswered}
          className="w-full py-3 bg-blue-600 text-white rounded-lg font-semibold disabled:opacity-50 hover:bg-blue-700 transition-colors"
        >
          Submit Quiz
        </button>
      ) : (
        <div className="text-center py-4">
          <p className="text-lg font-semibold text-green-600">Score: {score}%</p>
        </div>
      )}
    </div>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

type Step = "loading" | "pin" | "content" | "complete" | "error";
type ContentPhase = "sections" | "quiz" | "signoff";

export default function QrScanPage() {
  const params = useParams();
  const codeToken = params.codeToken as string;

  const [step, setStep] = useState<Step>("loading");
  const [codeInfo, setCodeInfo] = useState<CodeInfo | null>(null);
  const [language, setLanguage] = useState("en");
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [sessionData, setSessionData] = useState<SessionData | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [lockedUntil, setLockedUntil] = useState<Date | null>(null);
  const [pinError, setPinError] = useState<string | null>(null);
  const [pinLoading, setPinLoading] = useState(false);
  const [contentPhase, setContentPhase] = useState<ContentPhase>("sections");
  const [quizScore, setQuizScore] = useState<number | null>(null);
  const [completedAt, setCompletedAt] = useState<Date | null>(null);

  useEffect(() => {
    setLanguage(detectBrowserLanguage());
    loadCodeInfo();
  }, [codeToken]);

  async function loadCodeInfo() {
    try {
      const res = await fetch(`${API_BASE}/qr-locations/codes/${codeToken}`);
      if (!res.ok) {
        setErrorMsg("QR code not found or inactive.");
        setStep("error");
        return;
      }
      const data: CodeInfo = await res.json();
      setCodeInfo(data);
      setStep("pin");
    } catch {
      setErrorMsg("Unable to load QR code. Check your connection.");
      setStep("error");
    }
  }

  async function handlePinSubmit(pin: string) {
    setPinLoading(true);
    setPinError(null);
    setLockedUntil(null);

    try {
      const res = await fetch(`${API_BASE}/qr/${codeToken}/verify-pin`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ employeePin: pin }),
      });

      if (res.status === 401) {
        const body = await res.json();
        setPinError(
          `Incorrect PIN.${body.attemptsRemaining > 0 ? ` ${body.attemptsRemaining} attempt(s) remaining.` : ""}`
        );
        return;
      }

      if (res.status === 423) {
        const body = await res.json();
        setLockedUntil(body.lockedUntil ? new Date(body.lockedUntil) : null);
        setPinError("Account locked due to too many failed attempts.");
        return;
      }

      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        setPinError(body.message ?? "Verification failed. Please try again.");
        return;
      }

      const data = await res.json();
      setSessionToken(data.sessionToken);

      // Load full session with talk content using the session's language
      const sessionRes = await fetch(`${API_BASE}/qr/session/${data.sessionToken}`);
      if (!sessionRes.ok) {
        setPinError("Failed to load training content.");
        return;
      }
      const session: SessionData = await sessionRes.json();
      setSessionData(session);
      setContentPhase("sections");
      setStep("content");
    } catch {
      setPinError("Network error. Please try again.");
    } finally {
      setPinLoading(false);
    }
  }

  async function handleSignOff() {
    if (!sessionToken) return;
    try {
      await fetch(`${API_BASE}/qr/session/${sessionToken}/complete`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ score: quizScore }),
      });
      setCompletedAt(new Date());
      setStep("complete");
    } catch {
      // still advance — don't block completion on network error
      setCompletedAt(new Date());
      setStep("complete");
    }
  }

  function handleQuizPassed(score: number) {
    setQuizScore(score);
    setContentPhase("signoff");
  }

  function handleDone() {
    setStep("pin");
    setSessionToken(null);
    setSessionData(null);
    setPinError(null);
    setLockedUntil(null);
    setContentPhase("sections");
    setQuizScore(null);
    setCompletedAt(null);
  }

  const needsQuiz =
    sessionData?.talk?.requiresQuiz &&
    (sessionData.contentMode === "Training" || sessionData.contentMode === "Induction");

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      {/* Header */}
      <header className="bg-white border-b px-4 py-3 flex items-center gap-3">
        <svg viewBox="0 0 46 46" fill="none" className="w-8 h-8 shrink-0">
          <circle cx="23" cy="23" r="21" fill="#4d8eff" fillOpacity="0.1" stroke="#4d8eff" strokeWidth="1.5" strokeOpacity="0.3" />
          <path d="M23 10V23L30 30" stroke="#4d8eff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
          <circle cx="23" cy="23" r="3" fill="#4d8eff" />
        </svg>
        <span className="font-bold text-gray-900">
          Certified<span className="text-blue-600">IQ</span>
        </span>
        {codeInfo && (
          <span className="ml-auto text-sm text-gray-500">{codeInfo.locationName}</span>
        )}
      </header>

      <main className="flex-1 flex items-start justify-center p-4 pt-8">
        <div className="w-full max-w-lg">

          {/* ── Loading ── */}
          {step === "loading" && (
            <div className="text-center py-16 text-gray-500">Loading...</div>
          )}

          {/* ── Error ── */}
          {step === "error" && (
            <div className="bg-white rounded-xl shadow p-8 text-center">
              <div className="text-4xl mb-4">⚠️</div>
              <p className="text-red-600 font-semibold">{errorMsg}</p>
            </div>
          )}

          {/* ── Step 1: PIN Entry ── */}
          {step === "pin" && codeInfo && (
            <div className="bg-white rounded-xl shadow p-8">
              <h1 className="text-xl font-bold text-center mb-1">{codeInfo.locationName}</h1>
              {codeInfo.talkTitle && (
                <p className="text-center text-gray-500 text-sm mb-6">{codeInfo.talkTitle}</p>
              )}

              {/* Language selector */}
              <div className="mb-6">
                <label className="block text-sm font-medium text-gray-700 mb-1">Language</label>
                <select
                  value={language}
                  onChange={(e) => setLanguage(e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500"
                >
                  {SUPPORTED_LANGUAGES.map((l) => (
                    <option key={l.code} value={l.code}>{l.label}</option>
                  ))}
                </select>
              </div>

              <p className="text-center text-sm text-gray-600 mb-4 font-medium">Enter your 6-digit PIN</p>

              <PinInput onSubmit={handlePinSubmit} disabled={pinLoading} />

              {pinError && (
                <div className="mt-4 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700 text-center">
                  {pinError}
                  {lockedUntil && (
                    <p className="mt-1 text-xs">
                      Try again after {lockedUntil.toLocaleTimeString()}
                    </p>
                  )}
                </div>
              )}
            </div>
          )}

          {/* ── Step 2: Content ── */}
          {step === "content" && sessionData?.talk && (
            <div className="space-y-4">
              {/* Talk header */}
              <div className="bg-white rounded-xl shadow p-5">
                <p className="text-xs text-blue-600 font-medium uppercase tracking-wide mb-1">
                  {sessionData.contentMode} — {sessionData.employeeName}
                </p>
                <h2 className="text-xl font-bold text-gray-900">{sessionData.talk.title}</h2>
                {sessionData.talk.description && (
                  <p className="text-sm text-gray-500 mt-1">{sessionData.talk.description}</p>
                )}
              </div>

              {/* Sections phase */}
              {contentPhase === "sections" && (
                <>
                  {sessionData.talk.sections.map((s) => (
                    <div key={s.sectionId} className="bg-white rounded-xl shadow p-5">
                      <h3 className="font-semibold text-gray-800 mb-3">{s.title}</h3>
                      <div
                        className="prose prose-sm max-w-none text-gray-700"
                        dangerouslySetInnerHTML={{ __html: s.content }}
                      />
                    </div>
                  ))}

                  <button
                    onClick={() => {
                      if (needsQuiz) {
                        setContentPhase("quiz");
                      } else {
                        setContentPhase("signoff");
                      }
                    }}
                    className="w-full py-3 bg-blue-600 text-white rounded-xl font-semibold hover:bg-blue-700 transition-colors shadow"
                  >
                    {needsQuiz ? "Continue to Quiz" : "Sign Off"}
                  </button>
                </>
              )}

              {/* Quiz phase */}
              {contentPhase === "quiz" && sessionData.talk.questions.length > 0 && (
                <>
                  <div className="bg-white rounded-xl shadow p-5">
                    <h3 className="font-semibold text-gray-800 mb-4">Knowledge Check</h3>
                    <Quiz
                      questions={sessionData.talk.questions}
                      passingScore={sessionData.talk.passingScore}
                      contentMode={sessionData.contentMode}
                      onPass={handleQuizPassed}
                    />
                  </div>
                </>
              )}

              {/* Sign-off phase */}
              {contentPhase === "signoff" && (
                <div className="bg-white rounded-xl shadow p-8 text-center">
                  {quizScore !== null && (
                    <p className="text-sm text-gray-500 mb-4">Quiz score: {quizScore}%</p>
                  )}
                  <p className="text-gray-700 mb-6">
                    By tapping <strong>Sign Off</strong> you confirm you have completed this training.
                  </p>
                  <button
                    onClick={handleSignOff}
                    className="w-full py-4 bg-green-600 text-white rounded-xl font-bold text-lg hover:bg-green-700 transition-colors shadow"
                  >
                    Sign Off
                  </button>
                </div>
              )}
            </div>
          )}

          {/* ── Step 3: Completion ── */}
          {step === "complete" && sessionData && (
            <div className="bg-white rounded-xl shadow p-8 text-center">
              <div className="text-5xl mb-4">✅</div>
              <h2 className="text-2xl font-bold text-green-700 mb-2">Training Complete</h2>
              <p className="text-gray-700 font-medium mb-1">{sessionData.employeeName}</p>
              <p className="text-gray-500 text-sm mb-1">{sessionData.talk?.title}</p>
              {completedAt && (
                <p className="text-gray-400 text-xs mb-6">
                  {completedAt.toLocaleString()}
                </p>
              )}
              <button
                onClick={handleDone}
                className="w-full py-3 bg-blue-600 text-white rounded-xl font-semibold hover:bg-blue-700 transition-colors"
              >
                Done
              </button>
            </div>
          )}

        </div>
      </main>
    </div>
  );
}
