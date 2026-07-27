FROM node:24-alpine AS deps
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app

ARG CIVIC_PROXY_API_BASE_URL=http://localhost:5020
ARG CIVIC_PROXY_OBJECT_STORAGE_BASE_URL=http://localhost:9000
ARG NEXT_PUBLIC_API_BASE_URL=
ARG NEXT_PUBLIC_API_PORT=5020
ENV CIVIC_PROXY_API_BASE_URL=$CIVIC_PROXY_API_BASE_URL \
    CIVIC_PROXY_OBJECT_STORAGE_BASE_URL=$CIVIC_PROXY_OBJECT_STORAGE_BASE_URL \
    NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL \
    NEXT_PUBLIC_API_PORT=$NEXT_PUBLIC_API_PORT \
    NEXT_TELEMETRY_DISABLED=1

COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:24-alpine AS runner
WORKDIR /app

ENV CIVIC_PROXY_API_BASE_URL=http://localhost:5020 \
    CIVIC_PROXY_OBJECT_STORAGE_BASE_URL=http://localhost:9000 \
    HOSTNAME=0.0.0.0 \
    NEXT_TELEMETRY_DISABLED=1 \
    NODE_ENV=production \
    PORT=3000

COPY --from=build /app/package.json ./package.json
COPY --from=build /app/package-lock.json ./package-lock.json
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/public ./public
COPY --from=build /app/.next ./.next

EXPOSE 3000

CMD ["npm", "run", "start"]
