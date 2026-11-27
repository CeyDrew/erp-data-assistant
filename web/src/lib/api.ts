import type { AuthResponse, ProblemDetails } from "./types";

export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

const REFRESH_KEY = "erp.refresh";

export class ApiError extends Error {
  constructor(
    public status: number,
    public problem: ProblemDetails | null,
  ) {
    super(problem?.detail ?? problem?.title ?? `HTTP ${status}`);
  }
}

type Listener = (auth: AuthResponse | null) => void;

/**
 * Token yönetimi: access token yalnızca bellekte, refresh token localStorage'da durur.
 * 401 gelirse tek bir yenileme isteği yapılır (eşzamanlı istekler aynı promise'i bekler)
 * ve istek bir kez tekrarlanır.
 */
class TokenStore {
  private access: string | null = null;
  private refreshing: Promise<AuthResponse | null> | null = null;
  private listeners = new Set<Listener>();

  get accessToken() {
    return this.access;
  }

  get refreshToken() {
    try {
      return localStorage.getItem(REFRESH_KEY);
    } catch {
      return null;
    }
  }

  set(auth: AuthResponse | null) {
    this.access = auth?.accessToken ?? null;
    try {
      if (auth) localStorage.setItem(REFRESH_KEY, auth.refreshToken);
      else localStorage.removeItem(REFRESH_KEY);
    } catch {
      /* gizli pencere vb. */
    }
    this.listeners.forEach((l) => l(auth));
  }

  subscribe(listener: Listener) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  refresh(): Promise<AuthResponse | null> {
    const token = this.refreshToken;
    if (!token) return Promise.resolve(null);
    this.refreshing ??= fetch(`${API_URL}/api/v1/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: token }),
    })
      .then(async (r) => (r.ok ? ((await r.json()) as AuthResponse) : null))
      .catch(() => null)
      .then((auth) => {
        this.set(auth);
        return auth;
      })
      .finally(() => {
        this.refreshing = null;
      });
    return this.refreshing;
  }
}

export const tokens = new TokenStore();

export async function apiFetch(path: string, init: RequestInit = {}, retry = true): Promise<Response> {
  const headers = new Headers(init.headers);
  if (tokens.accessToken) headers.set("Authorization", `Bearer ${tokens.accessToken}`);
  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type"))
    headers.set("Content-Type", "application/json");

  const response = await fetch(`${API_URL}${path}`, { ...init, headers });

  if (response.status === 401 && retry && tokens.refreshToken) {
    const auth = await tokens.refresh();
    if (auth) return apiFetch(path, init, false);
  }
  return response;
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiFetch(path, init);
  if (!response.ok) {
    let problem: ProblemDetails | null = null;
    try {
      problem = await response.json();
    } catch {
      /* gövdesiz hata */
    }
    throw new ApiError(response.status, problem);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export function qs(params: Record<string, string | number | boolean | null | undefined>) {
  const search = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === "" || v === false) continue;
    search.set(k, String(v));
  }
  const s = search.toString();
  return s ? `?${s}` : "";
}
