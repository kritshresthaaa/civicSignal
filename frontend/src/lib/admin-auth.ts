"use client";

import { useCallback, useEffect, useState } from "react";
import {
  CivicApiError,
  getCurrentUser,
  login,
  logout,
  refreshAuth,
  type AuthTokenResponse,
  type AuthUserResponse,
} from "@/lib/civic-api";

export const adminTokenStorageKey = "civicsignal-admin-token";
export const adminTokenExpiresAtStorageKey = "civicsignal-admin-token-expires-at";
const adminRefreshTokenStorageKey = "civicsignal-admin-refresh-token";
const adminRefreshTokenExpiresAtStorageKey = "civicsignal-admin-refresh-token-expires-at";
const adminUserStorageKey = "civicsignal-admin-user";
const adminSessionExpiresAtStorageKey = "civicsignal-admin-session-expires-at";
const tokenRefreshSkewMs = 60_000;

export type AdminRole = "Administrator" | "Operator" | "Reviewer" | "Reporter";

export type AdminSession = {
  accessToken?: string;
  expiresAt: number;
  user: AuthUserResponse;
};

export type AdminSessionState = "loading" | "authenticated" | "anonymous" | "error";

export function isStaffUser(user: AuthUserResponse) {
  return hasAnyRole(user, ["Administrator", "Operator", "Reviewer"]);
}

export function hasAnyRole(user: AuthUserResponse, roles: AdminRole[]) {
  return roles.some((role) => user.roles.includes(role));
}

export function allowedRolesForAdminPath(pathname: string): AdminRole[] {
  if (pathname.startsWith("/admin/data-sources")) {
    return ["Administrator", "Operator"];
  }

  if (pathname.startsWith("/admin/review")) {
    return ["Administrator", "Operator", "Reviewer"];
  }

  return ["Administrator", "Operator", "Reviewer"];
}

export function canAccessAdminPath(pathname: string, user: AuthUserResponse) {
  return hasAnyRole(user, allowedRolesForAdminPath(pathname));
}

export function readStoredAdminSession(): AdminSession | null {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const expiresAt = readStoredTimestamp(adminRefreshTokenExpiresAtStorageKey)
      ?? readStoredTimestamp(adminSessionExpiresAtStorageKey);
    const userRaw = window.localStorage.getItem(adminUserStorageKey);
    const user = userRaw ? (JSON.parse(userRaw) as AuthUserResponse) : null;

    if (!expiresAt || expiresAt <= Date.now() + tokenRefreshSkewMs || !isAuthUser(user)) {
      return null;
    }

    return { accessToken: readStoredAdminAccessToken() ?? undefined, expiresAt, user };
  } catch {
    return null;
  }
}

export function readStoredAdminAccessToken() {
  if (typeof window === "undefined") {
    return null;
  }

  const accessToken = window.localStorage.getItem(adminTokenStorageKey);
  const expiresAt = readStoredTimestamp(adminTokenExpiresAtStorageKey);

  if (!accessToken || !expiresAt || expiresAt <= Date.now() + tokenRefreshSkewMs) {
    return null;
  }

  return accessToken;
}

export function storeAdminSession(token: AuthTokenResponse, user: AuthUserResponse) {
  if (typeof window === "undefined") {
    return;
  }

  const accessTokenExpiresAt = toValidTimestamp(token.accessTokenExpiresAt, token.expiresIn);
  const refreshTokenExpiresAt = toValidTimestamp(token.refreshTokenExpiresAt, token.refreshTokenExpiresIn);

  window.localStorage.setItem(adminTokenStorageKey, token.accessToken);
  window.localStorage.setItem(adminTokenExpiresAtStorageKey, String(accessTokenExpiresAt));
  window.localStorage.setItem(adminRefreshTokenStorageKey, token.refreshToken);
  window.localStorage.setItem(adminRefreshTokenExpiresAtStorageKey, String(refreshTokenExpiresAt));
  window.localStorage.setItem(adminSessionExpiresAtStorageKey, String(refreshTokenExpiresAt));
  window.localStorage.setItem(adminUserStorageKey, JSON.stringify(user));
}

export function clearAdminSession() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(adminTokenStorageKey);
  window.localStorage.removeItem(adminTokenExpiresAtStorageKey);
  window.localStorage.removeItem(adminRefreshTokenStorageKey);
  window.localStorage.removeItem(adminRefreshTokenExpiresAtStorageKey);
  window.localStorage.removeItem(adminSessionExpiresAtStorageKey);
  window.localStorage.removeItem(adminUserStorageKey);
}

export function useAdminSession() {
  const [state, setState] = useState<AdminSessionState>("loading");
  const [session, setSession] = useState<AdminSession | null>(null);
  const [message, setMessage] = useState("Sign in with a staff account to open the operations console.");

  const signOut = useCallback(async () => {
    await logout(readStoredAdminRefreshToken() ?? undefined).catch(() => undefined);
    clearAdminSession();
    setSession(null);
    setState("anonymous");
    setMessage("Signed out.");
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    setState("loading");
    setMessage("Signing in...");

    try {
      const token = await login(email, password);
      const user = await getCurrentUser(token.accessToken);

      if (!isStaffUser(user)) {
        await logout(token.refreshToken).catch(() => undefined);
        clearAdminSession();
        setSession(null);
        setState("error");
        setMessage("This account is not assigned to an admin, operator, or reviewer role.");
        return null;
      }

      const nextSession = {
        accessToken: token.accessToken,
        expiresAt: toValidTimestamp(token.refreshTokenExpiresAt, token.refreshTokenExpiresIn),
        user,
      };

      storeAdminSession(token, user);
      setSession(nextSession);
      setState("authenticated");
      setMessage(`${user.displayName || user.email} signed in.`);
      return nextSession;
    } catch (error) {
      clearAdminSession();
      setSession(null);
      setState("error");
      setMessage(error instanceof CivicApiError ? error.message : "Could not sign in.");
      return null;
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(async () => {
      const storedSession = readStoredAdminSession();
      const storedAccessToken = storedSession?.accessToken ?? readStoredAdminAccessToken() ?? undefined;
      const storedRefreshToken = readStoredAdminRefreshToken() ?? undefined;

      if (!storedAccessToken && !storedRefreshToken) {
        clearAdminSession();
        setSession(null);
        setState("anonymous");
        return;
      }

      try {
        let refreshedToken: AuthTokenResponse | null = null;
        let accessToken = storedAccessToken;
        let expiresAt = storedSession?.expiresAt ?? Date.now() + tokenRefreshSkewMs;

        if (!accessToken) {
          refreshedToken = await refreshAuth(storedRefreshToken);
          accessToken = refreshedToken.accessToken;
          expiresAt = toValidTimestamp(refreshedToken.refreshTokenExpiresAt, refreshedToken.refreshTokenExpiresIn);
        }

        let user = await getCurrentUser(accessToken);

        if (!isStaffUser(user)) {
          await logout(refreshedToken?.refreshToken ?? storedRefreshToken).catch(() => undefined);
          clearAdminSession();
          setSession(null);
          setState("anonymous");
          return;
        }

        refreshedToken ??= await refreshIfAccessTokenIsNearExpiry();
        if (refreshedToken) {
          user = await getCurrentUser(refreshedToken.accessToken);
          expiresAt = toValidTimestamp(refreshedToken.refreshTokenExpiresAt, refreshedToken.refreshTokenExpiresIn);
          storeAdminSession(refreshedToken, user);
        }

        setSession({ accessToken: refreshedToken?.accessToken ?? accessToken, expiresAt, user });
        setState("authenticated");
        setMessage(`${user.displayName || user.email} signed in.`);
      } catch {
        try {
          if (!storedRefreshToken) {
            clearAdminSession();
            setSession(null);
            setState("anonymous");
            return;
          }

          const token = await refreshAuth(storedRefreshToken);
          const user = await getCurrentUser(token.accessToken);

          if (!isStaffUser(user)) {
            await logout(token.refreshToken).catch(() => undefined);
            clearAdminSession();
            setSession(null);
            setState("anonymous");
            return;
          }

          const nextSession = {
            accessToken: token.accessToken,
            expiresAt: toValidTimestamp(token.refreshTokenExpiresAt, token.refreshTokenExpiresIn),
            user,
          };

          storeAdminSession(token, user);
          setSession(nextSession);
          setState("authenticated");
          setMessage(`${user.displayName || user.email} signed in.`);
        } catch {
          clearAdminSession();
          setSession(null);
          setState("anonymous");
        }
      }
    }, 0);

    return () => window.clearTimeout(timer);
  }, []);

  return {
    message,
    session,
    signIn,
    signOut,
    state,
  };
}

async function refreshIfAccessTokenIsNearExpiry() {
  const accessToken = typeof window === "undefined"
    ? null
    : window.localStorage.getItem(adminTokenStorageKey);
  const accessTokenExpiresAt = readStoredTimestamp(adminTokenExpiresAtStorageKey);

  if (!accessToken) {
    return null;
  }

  if (!accessTokenExpiresAt || accessTokenExpiresAt <= Date.now() + tokenRefreshSkewMs) {
    return refreshAuth(readStoredAdminRefreshToken() ?? undefined);
  }

  return null;
}

function readStoredAdminRefreshToken() {
  if (typeof window === "undefined") {
    return null;
  }

  const refreshToken = window.localStorage.getItem(adminRefreshTokenStorageKey);
  const expiresAt = readStoredTimestamp(adminRefreshTokenExpiresAtStorageKey)
    ?? readStoredTimestamp(adminSessionExpiresAtStorageKey);

  if (!refreshToken || !expiresAt || expiresAt <= Date.now() + tokenRefreshSkewMs) {
    return null;
  }

  return refreshToken;
}

function readStoredTimestamp(key: string) {
  if (typeof window === "undefined") {
    return null;
  }

  const value = Number(window.localStorage.getItem(key));
  return Number.isFinite(value) ? value : null;
}

function toValidTimestamp(value: string, fallbackSeconds: number) {
  const timestamp = new Date(value).getTime();

  if (Number.isFinite(timestamp)) {
    return timestamp;
  }

  return Date.now() + Math.max(60, fallbackSeconds) * 1000;
}

function isAuthUser(value: unknown): value is AuthUserResponse {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as AuthUserResponse;

  return (
    typeof candidate.id === "string" &&
    typeof candidate.email === "string" &&
    Array.isArray(candidate.roles) &&
    candidate.roles.every((role) => typeof role === "string")
  );
}
