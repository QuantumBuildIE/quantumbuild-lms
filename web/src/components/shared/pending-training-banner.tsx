"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { AlertTriangle, Info, X, ArrowRight } from "lucide-react";
import { useMyTrainingSummary } from "@/lib/api/toolbox-talks/use-my-toolbox-talks";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const BANNER_DISMISSED_KEY = "pending-training-banner-dismissed";
const BANNER_DISMISSED_EXPIRY = 24 * 60 * 60 * 1000; // 24 hours

interface DismissedState {
  timestamp: number;
  count: number;
}

export function PendingTrainingBanner() {
  const { data: trainingSummary, isLoading } = useMyTrainingSummary();
  const [isDismissed, setIsDismissed] = useState(true); // Start dismissed to prevent flash

  useEffect(() => {
    // Check if banner was previously dismissed
    const dismissed = localStorage.getItem(BANNER_DISMISSED_KEY);
    if (dismissed) {
      try {
        const state: DismissedState = JSON.parse(dismissed);
        const isExpired = Date.now() - state.timestamp > BANNER_DISMISSED_EXPIRY;
        // Show again if expired or if count has changed (new training assigned)
        if (
          isExpired ||
          (trainingSummary && state.count !== trainingSummary.totalCount)
        ) {
          localStorage.removeItem(BANNER_DISMISSED_KEY);
          setIsDismissed(false);
        } else {
          setIsDismissed(true);
        }
      } catch {
        localStorage.removeItem(BANNER_DISMISSED_KEY);
        setIsDismissed(false);
      }
    } else {
      setIsDismissed(false);
    }
  }, [trainingSummary]);

  const handleDismiss = () => {
    const state: DismissedState = {
      timestamp: Date.now(),
      count: trainingSummary?.totalCount ?? 0,
    };
    localStorage.setItem(BANNER_DISMISSED_KEY, JSON.stringify(state));
    setIsDismissed(true);
  };

  // Don't show if loading, dismissed, or no pending training
  if (isLoading || isDismissed) {
    return null;
  }

  const totalCount = trainingSummary?.totalCount ?? 0;
  const overdueCount = trainingSummary?.overdueCount ?? 0;
  const pendingCount = trainingSummary?.pendingCount ?? 0;
  const inProgressCount = trainingSummary?.inProgressCount ?? 0;

  if (totalCount === 0) {
    return null;
  }

  const hasOverdue = overdueCount > 0;
  const Icon = hasOverdue ? AlertTriangle : Info;

  // Build the message based on what types of training are pending
  let message = "";
  if (hasOverdue) {
    message = `You have ${overdueCount} overdue Toolbox Talk${overdueCount !== 1 ? "s" : ""} requiring immediate attention`;
    if (pendingCount > 0 || inProgressCount > 0) {
      message += ` and ${pendingCount + inProgressCount} more pending`;
    }
    message += ".";
  } else {
    message = `You have ${totalCount} pending Toolbox Talk${totalCount !== 1 ? "s" : ""} to complete.`;
  }

  return (
    <div
      className={cn(
        "relative w-full border-b px-4 py-3 transition-all",
        hasOverdue
          ? "bg-amber-50 border-amber-200 dark:bg-amber-950/30 dark:border-amber-900"
          : "bg-blue-50 border-blue-200 dark:bg-blue-950/30 dark:border-blue-900"
      )}
    >
      <div className="container flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <Icon
            className={cn(
              "h-5 w-5 shrink-0",
              hasOverdue
                ? "text-amber-600 dark:text-amber-400"
                : "text-blue-600 dark:text-blue-400"
            )}
          />
          <p
            className={cn(
              "text-sm font-medium",
              hasOverdue
                ? "text-amber-800 dark:text-amber-200"
                : "text-blue-800 dark:text-blue-200"
            )}
          >
            {message}
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Button
            asChild
            size="sm"
            variant={hasOverdue ? "default" : "outline"}
            className={cn(
              hasOverdue &&
                "bg-amber-600 hover:bg-amber-700 text-white border-amber-600"
            )}
          >
            <Link href="/toolbox-talks" className="flex items-center gap-1">
              View Training
              <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </Button>

          <Button
            variant="ghost"
            size="icon"
            className={cn(
              "h-7 w-7",
              hasOverdue
                ? "text-amber-600 hover:text-amber-800 hover:bg-amber-100 dark:text-amber-400 dark:hover:text-amber-200 dark:hover:bg-amber-900/50"
                : "text-blue-600 hover:text-blue-800 hover:bg-blue-100 dark:text-blue-400 dark:hover:text-blue-200 dark:hover:bg-blue-900/50"
            )}
            onClick={handleDismiss}
            aria-label="Dismiss"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
