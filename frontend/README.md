# CivicSignal Frontend

Next.js PWA shell for citizen incident reporting, staff operations, review, analytics, data sources, Model Lab, and AI evaluation.

## Commands

```bash
npm install
npm run dev:web
npm run lint
npm run build
```

The app uses same-origin backend routes by default:

```env
NEXT_PUBLIC_API_BASE_URL=
CIVIC_PROXY_API_BASE_URL=http://localhost:5020
```

`NEXT_PUBLIC_API_BASE_URL` should usually stay blank so phones, ngrok, and deployed frontends call `/api`, `/hubs`, `/media`, and `/health` on the same origin. `CIVIC_PROXY_API_BASE_URL` tells the Next.js server where the backend API is. Copy `.env.example` to `.env.local` when you need local overrides.

To test from another device on the same Wi-Fi:

```bash
npm run dev:web
```

Open `http://<laptop-ip>:3000` on the other device.

## Current Scope

- Mobile-first citizen report form, address search, reverse geocoding, and public status tracker.
- Staff operations dashboard with map, queue, review, and incident detail workflows.
- Controlled agent workflow panel for live backend incidents.
- Data Sources page for NYC 311 import jobs.
- Dashboard, Settings, Analytics, Model Lab, and AI Evaluation pages backed by CivicSignal API endpoints.
- PWA manifest and install metadata.
- Backend API base URL wiring.

The AI Evaluation page is available at `/admin/ai-evaluation` and reads `GET /api/ai-evaluations/baselines`.

The Model Lab pages are available at `/public/model-lab` and `/admin/model-lab`. They call `POST /api/model-lab/analyze` to visualize tokenization, embedding features, class probabilities, and routing decisions.

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.
