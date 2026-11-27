"use client";

import { useState } from "react";
import { AlertCircle, CheckCircle2, ChevronRight, Loader2, Wrench } from "lucide-react";
import { toolLabels, type ToolCall } from "@/lib/chat";
import { cn } from "@/lib/utils";

export function ToolCallCard({ call }: { call: ToolCall }) {
  const [open, setOpen] = useState(false);
  let args = call.arguments;
  try {
    args = JSON.stringify(JSON.parse(call.arguments), null, 2);
  } catch {
    /* ham metin */
  }

  return (
    <div className={cn("rounded-lg border bg-background text-xs", call.status === "error" && "border-amber-300")}>
      <button type="button" onClick={() => setOpen(!open)} className="flex w-full items-center gap-2 px-3 py-2 text-left">
        <ChevronRight className={cn("size-3.5 shrink-0 text-muted-foreground transition-transform", open && "rotate-90")} />
        <Wrench className="size-3.5 shrink-0 text-muted-foreground" />
        <span className="font-medium">{toolLabels[call.name] ?? call.name}</span>
        <code className="hidden font-mono text-muted-foreground sm:inline">{call.name}</code>
        <span className="ml-auto flex items-center gap-1.5 text-muted-foreground">
          {call.status === "running" && <Loader2 className="size-3.5 animate-spin" />}
          {call.status === "done" && (
            <>
              <span className="tabular-nums">
                {call.rowCount} satır · {call.durationMs} ms
              </span>
              <CheckCircle2 className="size-3.5 text-emerald-600" />
            </>
          )}
          {call.status === "error" && (
            <>
              <span className="max-w-60 truncate text-amber-700">{call.error}</span>
              <AlertCircle className="size-3.5 text-amber-600" />
            </>
          )}
        </span>
      </button>
      {open && (
        <div className="border-t px-3 py-2">
          <p className="mb-1 text-muted-foreground">Parametreler</p>
          <pre className="overflow-x-auto rounded bg-muted p-2 font-mono text-[11px]">{args}</pre>
          {call.error && <p className="mt-2 text-amber-700">Sunucu doğrulaması: {call.error}</p>}
        </div>
      )}
    </div>
  );
}
