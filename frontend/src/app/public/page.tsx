import { redirect } from "next/navigation";

export default function PublicHomePage() {
  redirect("/public/incidents");
}
