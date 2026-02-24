"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth, usePermission } from "@/lib/auth/use-auth";

export default function LessonParserLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const { user } = useAuth();
  const hasPermission = usePermission("LessonParser.Use");

  useEffect(() => {
    if (user && !hasPermission) {
      router.replace("/toolbox-talks");
    }
  }, [user, hasPermission, router]);

  if (!hasPermission) {
    return null;
  }

  return <div className="space-y-6">{children}</div>;
}
