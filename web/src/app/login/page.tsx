"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Bot, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";

const demoUsers = [
  { email: "yonetici@erp.local", role: "Yönetici", hint: "Her şeyi görür" },
  { email: "temsilci@erp.local", role: "Satış temsilcisi", hint: "Yalnızca kendi müşterileri" },
  { email: "depo@erp.local", role: "Depo sorumlusu", hint: "Stok ve sevkiyat, fiyat yok" },
];

export default function LoginPage() {
  const { login, status } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState(demoUsers[0].email);
  const [password, setPassword] = useState("Demo123!");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    if (status === "authenticated") router.replace("/dashboard");
  }, [status, router]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setPending(true);
    setError(null);
    try {
      await login(email, password);
      router.replace("/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Sunucuya ulaşılamadı.");
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-md space-y-6">
        <div className="flex items-center justify-center gap-2 text-lg font-semibold">
          <span className="flex size-9 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <Bot className="size-5" />
          </span>
          ERP Veri Asistanı
        </div>
        <Card>
          <CardHeader>
            <CardTitle>Giriş yap</CardTitle>
            <CardDescription>Demo hesaplarından birini seçebilirsiniz; şifre hepsinde aynı.</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={submit} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="email">E-posta</Label>
                <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">Şifre</Label>
                <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
              </div>
              {error && <p className="text-sm text-destructive">{error}</p>}
              <Button type="submit" className="w-full" size="lg" disabled={pending}>
                {pending && <Loader2 className="animate-spin" />} Giriş yap
              </Button>
            </form>
            <div className="mt-6 space-y-2">
              <p className="text-xs font-medium text-muted-foreground">Demo hesapları</p>
              {demoUsers.map((u) => (
                <button
                  key={u.email}
                  type="button"
                  onClick={() => {
                    setEmail(u.email);
                    setPassword("Demo123!");
                  }}
                  className={`flex w-full items-center justify-between rounded-lg border px-3 py-2 text-left text-sm transition-colors hover:bg-muted ${
                    email === u.email ? "border-primary bg-muted" : ""
                  }`}
                >
                  <span>
                    <span className="font-medium">{u.role}</span>
                    <span className="block text-xs text-muted-foreground">{u.email}</span>
                  </span>
                  <span className="text-xs text-muted-foreground">{u.hint}</span>
                </button>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
