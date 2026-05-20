import { redirect } from "next/navigation";

export default function LegacyRegulatoryDocumentPage({
  params,
}: {
  params: { documentId: string };
}) {
  redirect(`/admin/regulatory/system/${params.documentId}`);
}
