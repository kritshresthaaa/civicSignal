import { redirect } from "next/navigation";

export default function IncidentsRedirectPage() {
  redirect("/admin/incidents");
}
