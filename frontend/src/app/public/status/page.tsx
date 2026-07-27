import { PublicStatusTracker } from "@/components/public-status-tracker";

type PublicStatusPageProps = {
  searchParams?: Promise<{
    code?: string;
    incidentId?: string;
  }>;
};

export default async function PublicStatusPage({ searchParams }: PublicStatusPageProps) {
  const params = await searchParams;

  return <PublicStatusTracker initialCode={params?.code ?? params?.incidentId} />;
}
