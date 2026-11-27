import { apiFetch } from "./api";

export interface ToolCall {
  callId: string;
  name: string;
  arguments: string;
  status: "running" | "done" | "error";
  rowCount?: number;
  durationMs?: number;
  error?: string | null;
}

export interface Source {
  documentId: number;
  fileName: string;
  pageNumber: number;
  score: number;
}

export interface AssistantMessage {
  role: "assistant";
  content: string;
  tools: ToolCall[];
  sources: Source[];
  meta?: { logId: number; durationMs: number; inputTokens: number | null; outputTokens: number | null; model: string };
  error?: string;
  pending: boolean;
}

export interface UserMessage {
  role: "user";
  content: string;
}

export type Message = UserMessage | AssistantMessage;

type Handler = (type: string, data: Record<string, unknown>) => void;

/**
 * POST + fetch ile SSE okur (EventSource yalnızca GET destekler ve başlık gönderemez).
 * Olaylar "\n\n" ile ayrılır; her blokta "event:" ve "data:" satırları bulunur.
 */
export async function streamChat(
  message: string,
  history: { role: string; content: string }[],
  onEvent: Handler,
  signal: AbortSignal,
) {
  const response = await apiFetch("/api/v1/chat", {
    method: "POST",
    body: JSON.stringify({ message, history }),
    headers: { Accept: "text/event-stream" },
    signal,
  });
  if (!response.ok || !response.body) {
    let detail = `HTTP ${response.status}`;
    try {
      const p = await response.json();
      detail = p.detail ?? p.title ?? detail;
    } catch {
      /* gövde yok */
    }
    throw new Error(response.status === 429 ? "Çok fazla istek; biraz bekleyin." : detail);
  }

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
  let buffer = "";
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += value.replace(/\r\n/g, "\n");
    let idx;
    while ((idx = buffer.indexOf("\n\n")) >= 0) {
      const block = buffer.slice(0, idx);
      buffer = buffer.slice(idx + 2);
      let type = "message";
      const data: string[] = [];
      for (const line of block.split("\n")) {
        if (line.startsWith("event:")) type = line.slice(6).trim();
        else if (line.startsWith("data:")) data.push(line.slice(5).trimStart());
      }
      if (data.length) onEvent(type, JSON.parse(data.join("\n")));
    }
  }
}

export const toolLabels: Record<string, string> = {
  satis_ozeti: "Satış özeti",
  en_cok_satanlar: "En çok satanlar",
  musteri_ara: "Müşteri arama",
  musteri_siparisleri: "Müşteri siparişleri",
  stok_durumu: "Stok durumu",
  geciken_tahsilatlar: "Geciken tahsilatlar",
  dokumanda_ara: "Doküman araması",
};
