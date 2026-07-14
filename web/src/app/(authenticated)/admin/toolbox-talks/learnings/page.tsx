import { redirect } from "next/navigation";

export default function LearningsIndexPage() {
  redirect("/admin/toolbox-talks/learnings/drafts");
}
