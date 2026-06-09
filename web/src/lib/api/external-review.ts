import type {
  SubmitExternalReviewRequest,
  DeclineExternalReviewRequest,
} from "@/types/external-review";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5222/api";

export async function getExternalReviewPortal(token: string) {
  const res = await fetch(`${API_BASE}/external-review/${token}`);
  return {
    status: res.status,
    body: res.ok || res.status === 410 ? await res.json() : null,
  };
}

export async function submitExternalReview(
  token: string,
  payload: SubmitExternalReviewRequest
) {
  const res = await fetch(`${API_BASE}/external-review/${token}/submit`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return {
    status: res.status,
    body: res.ok ? await res.json() : await res.json().catch(() => null),
  };
}

export async function declineExternalReview(
  token: string,
  payload: DeclineExternalReviewRequest
) {
  const res = await fetch(`${API_BASE}/external-review/${token}/decline`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return {
    status: res.status,
    body: res.ok ? await res.json() : await res.json().catch(() => null),
  };
}
