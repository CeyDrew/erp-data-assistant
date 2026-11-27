"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Copy, KeyRound, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { PageHeader } from "@/components/page-header";
import { EmptyRow, ErrorState, TableSkeleton } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, ApiError } from "@/lib/api";
import { fmt } from "@/lib/format";

interface TokenItem {
  id: number;
  name: string;
  hint: string;
  createdAt: string;
  expiresAt: string;
  lastUsedAt: string | null;
  active: boolean;
}

interface CreatedToken {
  id: number;
  name: string;
  token: string;
  expiresAt: string;
}

const MCP_URL = process.env.NEXT_PUBLIC_MCP_URL ?? "http://localhost:5090/mcp";

export default function TokensPage() {
  const client = useQueryClient();
  const [name, setName] = useState("claude-code");
  const [created, setCreated] = useState<CreatedToken | null>(null);
  const { data, error, isPending } = useQuery({ queryKey: ["tokens"], queryFn: () => api<TokenItem[]>("/api/v1/tokens") });

  const create = useMutation({
    mutationFn: () => api<CreatedToken>("/api/v1/tokens", { method: "POST", body: JSON.stringify({ name, days: 30 }) }),
    onSuccess: (t) => {
      setCreated(t);
      client.invalidateQueries({ queryKey: ["tokens"] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Token oluşturulamadı"),
  });

  const revoke = useMutation({
    mutationFn: (id: number) => api(`/api/v1/tokens/${id}`, { method: "DELETE" }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["tokens"] }),
  });

  const command = created
    ? `claude mcp add --transport http erp ${MCP_URL} --header "Authorization: Bearer ${created.token}"`
    : "";

  return (
    <>
      <PageHeader
        title="Erişim token'ları"
        description="Claude Code gibi MCP istemcileri bu token'la sizin adınıza ve sizin yetkinizle bağlanır. Araçlar salt okunurdur."
      />

      <Card className="mb-4">
        <CardContent className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex-1 space-y-1.5">
            <label htmlFor="token-name" className="text-sm font-medium">
              Token adı
            </label>
            <Input id="token-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={64} />
          </div>
          <Button onClick={() => create.mutate()} disabled={!name.trim() || create.isPending}>
            <Plus /> Oluştur (30 gün)
          </Button>
        </CardContent>
      </Card>

      {created && (
        <Card className="mb-4 border-emerald-300 bg-emerald-50/50 dark:bg-emerald-950/20">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <KeyRound className="size-4" /> Token oluşturuldu
            </CardTitle>
            <CardDescription>Bu değer bir daha gösterilmeyecek. Claude Code&apos;a eklemek için:</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <div className="flex gap-2">
              <code className="flex-1 overflow-x-auto rounded-md border bg-background px-3 py-2 font-mono text-xs whitespace-nowrap">
                {command}
              </code>
              <Button
                variant="outline"
                size="icon"
                aria-label="Kopyala"
                onClick={() => navigator.clipboard.writeText(command).then(() => toast.success("Kopyalandı"))}
              >
                <Copy />
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {error ? (
        <ErrorState error={error} />
      ) : (
        <Card className="gap-0 py-0">
          {isPending ? (
            <TableSkeleton rows={3} />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-4">Ad</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead className="hidden sm:table-cell">Son kullanım</TableHead>
                  <TableHead className="hidden md:table-cell">Bitiş</TableHead>
                  <TableHead className="pr-4 text-right" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.length === 0 && <EmptyRow colSpan={5}>Henüz token yok.</EmptyRow>}
                {data.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell className="pl-4">
                      <p className="font-medium">{t.name}</p>
                      <p className="font-mono text-xs text-muted-foreground">{t.hint}…</p>
                    </TableCell>
                    <TableCell>
                      <Badge variant={t.active ? "secondary" : "outline"}>{t.active ? "Etkin" : "İptal / süresi doldu"}</Badge>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">{t.lastUsedAt ? fmt.dateTime(t.lastUsedAt) : "—"}</TableCell>
                    <TableCell className="hidden md:table-cell">{fmt.date(t.expiresAt)}</TableCell>
                    <TableCell className="pr-4 text-right">
                      {t.active && (
                        <Button variant="ghost" size="icon-sm" aria-label="İptal et" onClick={() => revoke.mutate(t.id)}>
                          <Trash2 />
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Card>
      )}
    </>
  );
}
