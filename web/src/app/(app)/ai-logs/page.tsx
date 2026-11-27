"use client";

import { Fragment, useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { ChevronRight } from "lucide-react";
import { NativeSelect } from "@/components/filters";
import { PageHeader } from "@/components/page-header";
import { Pagination } from "@/components/pagination";
import { EmptyRow, ErrorState, TableSkeleton } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, qs } from "@/lib/api";
import { toolLabels } from "@/lib/chat";
import { fmt } from "@/lib/format";
import type { Paged } from "@/lib/types";
import { cn } from "@/lib/utils";

interface AiLogItem {
  id: number;
  createdAt: string;
  userName: string;
  channel: string;
  question: string;
  answer: string | null;
  toolCallsJson: string;
  totalRowCount: number;
  durationMs: number;
  inputTokens: number | null;
  outputTokens: number | null;
  model: string | null;
  error: string | null;
}

interface ToolRecord {
  name: string;
  arguments: string;
  rowCount: number;
  durationMs: number;
  error: string | null;
}

export default function AiLogsPage() {
  const [page, setPage] = useState(1);
  const [channel, setChannel] = useState("");
  const [open, setOpen] = useState<number | null>(null);
  const params = { page, pageSize: 20, channel };
  const { data, error, isPending } = useQuery({
    queryKey: ["ai-logs", params],
    queryFn: () => api<Paged<AiLogItem>>(`/api/v1/ailogs${qs(params)}`),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader title="AI sorgu günlüğü" description="Her soru; kullanıcı, çağrılan araçlar, parametreler, dönen satır, süre ve token ile kaydedilir." />
      <div className="mb-4">
        <NativeSelect
          aria-label="Kanal"
          value={channel}
          onChange={(v) => {
            setChannel(v);
            setPage(1);
          }}
          options={[
            { value: "", label: "Tüm kanallar" },
            { value: "web", label: "Web paneli" },
            { value: "mcp", label: "MCP" },
          ]}
        />
      </div>
      {error ? (
        <ErrorState error={error} />
      ) : (
        <Card className="gap-0 py-0">
          {isPending ? (
            <TableSkeleton />
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-8 pl-4" />
                    <TableHead>Soru</TableHead>
                    <TableHead className="hidden md:table-cell">Kullanıcı</TableHead>
                    <TableHead>Araçlar</TableHead>
                    <TableHead className="hidden text-right sm:table-cell">Satır</TableHead>
                    <TableHead className="hidden text-right sm:table-cell">Süre</TableHead>
                    <TableHead className="hidden pr-4 text-right lg:table-cell">Token</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.length === 0 && <EmptyRow colSpan={7} />}
                  {data.items.map((l) => {
                    const tools: ToolRecord[] = JSON.parse(l.toolCallsJson || "[]");
                    const expanded = open === l.id;
                    return (
                      <Fragment key={l.id}>
                        <TableRow className="cursor-pointer" onClick={() => setOpen(expanded ? null : l.id)}>
                          <TableCell className="pl-4">
                            <ChevronRight className={cn("size-4 text-muted-foreground transition-transform", expanded && "rotate-90")} />
                          </TableCell>
                          <TableCell className="max-w-md">
                            <p className="truncate font-medium">{l.question}</p>
                            <p className="text-xs text-muted-foreground">
                              {fmt.dateTime(l.createdAt)} · {l.channel}
                              {l.error && <span className="text-destructive"> · hata</span>}
                            </p>
                          </TableCell>
                          <TableCell className="hidden md:table-cell">{l.userName}</TableCell>
                          <TableCell>
                            <div className="flex flex-wrap gap-1">
                              {tools.length === 0 && <span className="text-xs text-muted-foreground">—</span>}
                              {tools.map((t, i) => (
                                <Badge key={i} variant={t.error ? "outline" : "secondary"} className={cn(t.error && "border-amber-400 text-amber-700")}>
                                  {toolLabels[t.name] ?? t.name}
                                </Badge>
                              ))}
                            </div>
                          </TableCell>
                          <TableCell className="hidden text-right tabular-nums sm:table-cell">{fmt.number(l.totalRowCount)}</TableCell>
                          <TableCell className="hidden text-right tabular-nums sm:table-cell">{(l.durationMs / 1000).toFixed(1)} sn</TableCell>
                          <TableCell className="hidden pr-4 text-right tabular-nums lg:table-cell">
                            {l.inputTokens != null ? fmt.number(l.inputTokens + (l.outputTokens ?? 0)) : "—"}
                          </TableCell>
                        </TableRow>
                        {expanded && (
                          <TableRow className="bg-muted/30 hover:bg-muted/30">
                            <TableCell colSpan={7} className="space-y-3 px-4 py-3 whitespace-normal">
                              {tools.map((t, i) => (
                                <div key={i} className="rounded-lg border bg-background p-2 text-xs">
                                  <p className="font-mono font-medium">
                                    {t.name} · {t.rowCount} satır · {t.durationMs} ms
                                  </p>
                                  <pre className="mt-1 overflow-x-auto font-mono text-muted-foreground">{t.arguments}</pre>
                                  {t.error && <p className="mt-1 text-amber-700">{t.error}</p>}
                                </div>
                              ))}
                              {l.answer && <p className="text-sm whitespace-pre-wrap">{l.answer}</p>}
                              {l.error && <p className="text-sm text-destructive">{l.error}</p>}
                              <p className="text-xs text-muted-foreground">{l.model}</p>
                            </TableCell>
                          </TableRow>
                        )}
                      </Fragment>
                    );
                  })}
                </TableBody>
              </Table>
              <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />
            </>
          )}
        </Card>
      )}
    </>
  );
}
