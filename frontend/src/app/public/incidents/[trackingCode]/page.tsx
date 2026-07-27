import { notFound } from "next/navigation";
import { PublicIncidentDetail } from "@/components/public-incident-detail";

type PublicIncidentDetailPageProps = {
  params: Promise<{
    trackingCode: string;
  }>;
};

export default async function PublicIncidentDetailPage({ params }: PublicIncidentDetailPageProps) {
  const { trackingCode } = await params;
  const normalizedTrackingCode = decodeURIComponent(trackingCode).trim().toUpperCase();

  if (!/^[A-Z0-9-]{6,40}$/.test(normalizedTrackingCode)) {
    notFound();
  }

  return <PublicIncidentDetail trackingCode={normalizedTrackingCode} />;
}
