"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { api, tokens } from "./api";
import type { AuthResponse, UserInfo } from "./types";

interface AuthState {
  user: UserInfo | null;
  status: "loading" | "authenticated" | "anonymous";
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [status, setStatus] = useState<AuthState["status"]>("loading");

  useEffect(() => {
    const unsubscribe = tokens.subscribe((auth) => {
      setUser(auth?.user ?? null);
      setStatus(auth ? "authenticated" : "anonymous");
    });
    // Sayfa yenilendiğinde access token bellekten gider; refresh token ile oturumu geri kur.
    tokens.refresh().then((auth) => {
      if (!auth) setStatus("anonymous");
    });
    return () => {
      unsubscribe();
    };
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const auth = await api<AuthResponse>("/api/v1/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    });
    tokens.set(auth);
  }, []);

  const logout = useCallback(async () => {
    const refreshToken = tokens.refreshToken;
    if (refreshToken) {
      await api("/api/v1/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken }) }).catch(() => {});
    }
    tokens.set(null);
  }, []);

  const value = useMemo(() => ({ user, status, login, logout }), [user, status, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth, AuthProvider içinde kullanılmalı");
  return ctx;
}

export function useUser() {
  const { user } = useAuth();
  if (!user) throw new Error("Oturum yok");
  return user;
}
