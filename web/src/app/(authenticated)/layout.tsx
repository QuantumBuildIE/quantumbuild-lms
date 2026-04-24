"use client";

import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import { useAuth } from "@/lib/auth/use-auth";
import { TopNav } from "@/components/layout/top-nav";
import { PendingTrainingBanner } from "@/components/shared/pending-training-banner";

export default function AuthenticatedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const { isAuthenticated, isLoading, dpaAccepted, user } = useAuth();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push("/login");
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (!isLoading && isAuthenticated && !dpaAccepted && !user?.isSuperUser) {
      router.push("/dpa-acceptance");
    }
  }, [isLoading, isAuthenticated, dpaAccepted, user?.isSuperUser, router, pathname]);

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-pulse text-slate-500">Loading...</div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  if (!dpaAccepted && !user?.isSuperUser) {
    return null;
  }

  return (
    <div className="grid-bg min-h-screen bg-slate-50 dark:bg-slate-950">
      <TopNav />
      <PendingTrainingBanner />
      <main className="container mx-auto px-4 py-6">{children}</main>
    </div>
  );
}
