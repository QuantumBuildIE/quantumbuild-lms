"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { usePermission } from "@/lib/auth/use-auth";

export default function LessonParserLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const hasPermission = usePermission("LessonParser.Use");

  useEffect(() => {
    if (hasPermission === false) {
      router.replace("/toolbox-talks");
    }
  }, [hasPermission, router]);

  if (!hasPermission) {
    return null;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Link href="/admin" className="hover:text-foreground">
          Administration
        </Link>
        <span>/</span>
        <span className="text-foreground">Lesson Parser</span>
      </div>
      <div>{children}</div>
    </div>
  );
}
