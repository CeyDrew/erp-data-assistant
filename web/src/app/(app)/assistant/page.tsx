"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowUp, Bot, FileText, RotateCcw, Square, User } from "lucide-react";
import { Markdown } from "@/components/chat/markdown";
import { ToolCallCard } from "@/components/chat/tool-call-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import { useUser } from "@/lib/auth";
import { streamChat, type AssistantMessage, type Message, type ToolCall } from "@/lib/chat";

const suggestions: Record<string, string[]> = {
  Yonetici: [
    "Geçen ay en çok satan 5 ürün hangileri?",
    "Bu yılın aylık cirosunu göster",
    "60 günden fazla gecikmiş tahsilatlar",
    "Garanti şartnamesine göre iade süresi kaç gün?",
  ],
  SatisTemsilcisi: [
    "Bu ay müşterilerime göre satışlarım",
    "Vadesi geçmiş faturalarım hangileri?",
    "Geçen ay en çok sattığım 5 ürün",
    "Garanti şartnamesine göre iade süresi kaç gün?",
  ],
  DepoSorumlusu: [
    "Kritik stok seviyesinin altındaki ürünler",
    "İzmir deposunda kritik kalemler",
    "Geçen ay miktar olarak en çok satılan 5 ürün",
    "Depo sevkiyat prosedüründe palet yüksekliği sınırı nedir?",
  ],
};

interface ModelInfo {
  provider: string;
  chatModel: string;
}

export default function AssistantPage() {
  const user = useUser();
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const model = useQuery({ queryKey: ["chat-model"], queryFn: () => api<ModelInfo>("/api/v1/chat/model"), staleTime: Infinity });

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages]);

  function updateLast(fn: (m: AssistantMessage) => AssistantMessage) {
    setMessages((prev) => {
      const copy = [...prev];
      copy[copy.length - 1] = fn(copy[copy.length - 1] as AssistantMessage);
      return copy;
    });
  }

  async function send(text: string) {
    const question = text.trim();
    if (!question || busy) return;
    const history = messages
      .filter((m) => m.role === "user" || (m.role === "assistant" && m.content))
      .slice(-10)
      .map((m) => ({ role: m.role, content: m.content }));

    setInput("");
    setBusy(true);
    setMessages((prev) => [
      ...prev,
      { role: "user", content: question },
      { role: "assistant", content: "", tools: [], sources: [], pending: true },
    ]);

    const controller = new AbortController();
    abortRef.current = controller;
    try {
      await streamChat(
        question,
        history,
        (type, data) => {
          switch (type) {
            case "tool_call":
              updateLast((m) => ({
                ...m,
                tools: [...m.tools, { callId: data.callId as string, name: data.name as string, arguments: data.arguments as string, status: "running" }],
              }));
              break;
            case "tool_result":
              updateLast((m) => ({
                ...m,
                tools: m.tools.map((t): ToolCall =>
                  t.callId === data.callId
                    ? {
                        ...t,
                        status: data.error ? "error" : "done",
                        rowCount: data.rowCount as number,
                        durationMs: data.durationMs as number,
                        error: data.error as string | null,
                      }
                    : t,
                ),
              }));
              break;
            case "delta":
              updateLast((m) => ({ ...m, content: m.content + (data.text as string) }));
              break;
            case "sources":
              updateLast((m) => ({ ...m, sources: data.sources as AssistantMessage["sources"] }));
              break;
            case "done":
              updateLast((m) => ({ ...m, pending: false, meta: data as unknown as AssistantMessage["meta"] }));
              break;
            case "error":
              updateLast((m) => ({ ...m, pending: false, error: data.message as string }));
              break;
          }
        },
        controller.signal,
      );
    } catch (err) {
      if (!controller.signal.aborted) updateLast((m) => ({ ...m, error: (err as Error).message }));
    } finally {
      updateLast((m) => ({ ...m, pending: false }));
      setBusy(false);
      abortRef.current = null;
    }
  }

  const empty = messages.length === 0;
  const roleKey = user.roles[0] ?? "Yonetici";

  return (
    <div className="flex h-[calc(100vh-5rem)] flex-col md:h-[calc(100vh-3rem)]">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Asistan</h1>
          <p className="text-sm text-muted-foreground">
            Sorular yalnızca beyaz listedeki araçlarla ve sizin yetkinizle yanıtlanır.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {model.data && (
            <Badge variant="outline" className="hidden font-mono sm:inline-flex">
              {model.data.provider}/{model.data.chatModel}
            </Badge>
          )}
          {!empty && (
            <Button variant="outline" size="sm" onClick={() => setMessages([])} disabled={busy}>
              <RotateCcw /> Yeni sohbet
            </Button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto rounded-xl border bg-background">
        {empty ? (
          <div className="flex h-full flex-col items-center justify-center gap-6 p-6 text-center">
            <span className="flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
              <Bot className="size-6" />
            </span>
            <div>
              <p className="text-lg font-medium">Şirket verinize sorun</p>
              <p className="mt-1 max-w-md text-sm text-muted-foreground">
                Satış, stok, müşteri ve tahsilat verisi ile yüklenen dokümanlar üzerinden cevap verir; hangi aracı hangi
                parametrelerle çağırdığını gösterir.
              </p>
            </div>
            <div className="grid w-full max-w-2xl gap-2 sm:grid-cols-2">
              {(suggestions[roleKey] ?? suggestions.Yonetici).map((s) => (
                <button
                  key={s}
                  onClick={() => send(s)}
                  className="rounded-lg border px-3 py-2.5 text-left text-sm transition-colors hover:bg-muted"
                >
                  {s}
                </button>
              ))}
            </div>
          </div>
        ) : (
          <div className="mx-auto max-w-3xl space-y-6 p-4 md:p-6">
            {messages.map((m, i) =>
              m.role === "user" ? (
                <div key={i} className="flex justify-end gap-3">
                  <div className="max-w-[85%] rounded-2xl rounded-tr-sm bg-primary px-4 py-2.5 text-sm text-primary-foreground">
                    {m.content}
                  </div>
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-muted">
                    <User className="size-4" />
                  </span>
                </div>
              ) : (
                <div key={i} className="flex gap-3">
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
                    <Bot className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1 space-y-3">
                    {m.tools.length > 0 && (
                      <div className="space-y-1.5">
                        {m.tools.map((t) => (
                          <ToolCallCard key={t.callId} call={t} />
                        ))}
                      </div>
                    )}
                    {m.content && <Markdown>{m.content}</Markdown>}
                    {m.pending && !m.content && (
                      <div className="flex items-center gap-1 py-2">
                        {[0, 1, 2].map((d) => (
                          <span
                            key={d}
                            className="size-1.5 animate-bounce rounded-full bg-muted-foreground/60"
                            style={{ animationDelay: `${d * 150}ms` }}
                          />
                        ))}
                      </div>
                    )}
                    {m.error && (
                      <p className="rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">
                        {m.error}
                      </p>
                    )}
                    {m.sources.length > 0 && (
                      <div className="flex flex-wrap gap-1.5">
                        {m.sources.map((s) => (
                          <Badge key={`${s.documentId}-${s.pageNumber}`} variant="secondary" className="gap-1">
                            <FileText className="size-3" />
                            {s.fileName} · s. {s.pageNumber}
                          </Badge>
                        ))}
                      </div>
                    )}
                    {m.meta && (
                      <p className="text-xs text-muted-foreground tabular-nums">
                        {(m.meta.durationMs / 1000).toFixed(1)} sn
                        {m.meta.inputTokens != null && ` · ${m.meta.inputTokens + (m.meta.outputTokens ?? 0)} token`} · kayıt #{m.meta.logId}
                      </p>
                    )}
                  </div>
                </div>
              ),
            )}
            <div ref={bottomRef} />
          </div>
        )}
      </div>

      <form
        className="mt-3 flex items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          send(input);
        }}
      >
        <Textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              send(input);
            }
          }}
          placeholder="Örn. Geçen ay en çok satan 5 ürün hangileri?"
          rows={1}
          maxLength={1000}
          className="max-h-40 min-h-11 resize-none bg-background"
        />
        {busy ? (
          <Button type="button" size="icon-lg" variant="outline" onClick={() => abortRef.current?.abort()} aria-label="Durdur">
            <Square />
          </Button>
        ) : (
          <Button type="submit" size="icon-lg" disabled={!input.trim()} aria-label="Gönder">
            <ArrowUp />
          </Button>
        )}
      </form>
    </div>
  );
}
