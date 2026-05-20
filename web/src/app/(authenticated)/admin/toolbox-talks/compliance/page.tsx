import { redirect } from "next/navigation";

export default function LegacyCompliancePage() {
  redirect("/admin/regulatory/compliance");
}
