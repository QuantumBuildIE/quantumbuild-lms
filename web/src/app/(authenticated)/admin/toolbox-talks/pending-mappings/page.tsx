import { redirect } from "next/navigation";

export default function LegacyPendingMappingsPage() {
  redirect("/admin/regulatory/mappings");
}
