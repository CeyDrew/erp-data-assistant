"use client";

import { useRef, useState } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, CheckCircle2, Clock, FileText, Loader2, RefreshCw, Trash2, Upload } from "lucide-react";
import { toast } from "sonner";
import { NativeSelect } from "@/components/filters";
import { PageHeader } from "@/components/page-header";
import { Pagination } from "@/components/pagination";
import { EmptyRow, ErrorState, TableSkeleton } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, ApiError, apiFetch, qs } from "@/lib/api";
import { useUser } from "@/lib/auth";
import { fmt } from "@/lib/format";
import type { Paged } from "@/lib/types";
import { cn } from "@/lib/utils";

type Status = "Pending" | "Processing" | "Ready" | "Failed";

interface DocumentItem {
  id: number;
  fileName: string;
  sizeBytes: number;
  departmentCode: string;
  departmentName: string;
  status: Status;
  chunkCount: number;
  attempts: number;
  error: string | null;
  uploadedBy: string | null;
  uploadedAt: string;
  processedAt: string | null;
}

interface DepartmentItem {
  code: string;
  name: string;
  canUpload: boolean;
}

const statusView: Record<Status, { label: string; icon: typeof Clock; className: string }> = {
  Pending: { label: "Kuyrukta", icon: Clock, className: "bg-slate-100 text-slate-700" },
  Processing: { label: "İşleniyor", icon: Loader2, className: "bg-blue-100 text-blue-700" },
  Ready: { label: "Hazır", icon: CheckCircle2, className: "bg-emerald-100 text-emerald-700" },
  Failed: { label: "Hata", icon: AlertCircle, className: "bg-rose-100 text-rose-700" },
};

function size(bytes: number) {
  return bytes > 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export default function DocumentsPage() {
  const user = useUser();
  const isAdmin = user.roles.includes("Yonetici");
  const client = useQueryClient();
  const fileRef = useRef<HTMLInputElement>(null);
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState("");
  const [target, setTarget] = useState("");

  const departments = useQuery({
    queryKey: ["doc-departments"],
    queryFn: () => api<DepartmentItem[]>("/api/v1/documents/departments"),
    staleTime: Infinity,
  });
  const uploadable = departments.data?.filter((d) => d.canUpload) ?? [];
  const uploadDept = target || uploadable[0]?.code || "";

  const params = { page, pageSize: 20, department: filter };
  const { data, error, isPending } = useQuery({
    queryKey: ["documents", params],
    queryFn: () => api<Paged<DocumentItem>>(`/api/v1/documents${qs(params)}`),
    placeholderData: keepPreviousData,
    // İşlenmekte olan doküman varsa durumu canlı takip et.
    refetchInterval: (q) => (q.state.data?.items.some((d) => d.status === "Pending" || d.status === "Processing") ? 1500 : false),
  });

  const upload = useMutation({
    mutationFn: async (file: File) => {
      const form = new FormData();
      form.append("file", file);
      form.append("department", uploadDept);
      const response = await apiFetch("/api/v1/documents", { method: "POST", body: form });
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        throw new ApiError(response.status, problem);
      }
    },
    onSuccess: () => {
      toast.success("Doküman kuyruğa alındı");
      client.invalidateQueries({ queryKey: ["documents"] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Yükleme başarısız"),
  });

  const action = useMutation({
    mutationFn: ({ id, kind }: { id: number; kind: "reprocess" | "delete" }) =>
      kind === "delete"
        ? api(`/api/v1/documents/${id}`, { method: "DELETE" })
        : api(`/api/v1/documents/${id}/reprocess`, { method: "POST" }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["documents"] }),
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "İşlem başarısız"),
  });

  return (
    <>
      <PageHeader
        title="Dokümanlar"
        description="Yüklenen dosyalar kuyrukta işlenir: metin ayıklanır, parçalanır, embedding'i çıkarılıp vektör veritabanına yazılır."
      />

      {uploadable.length > 0 && (
        <Card className="mb-4">
          <CardContent className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <div className="flex-1">
              <p className="text-sm font-medium">Doküman yükle</p>
              <p className="text-xs text-muted-foreground">PDF, DOCX, XLSX, TXT veya MD · en fazla 20 MB</p>
            </div>
            <NativeSelect
              aria-label="Departman"
              value={uploadDept}
              onChange={setTarget}
              options={uploadable.map((d) => ({ value: d.code, label: d.name }))}
            />
            <input
              ref={fileRef}
              type="file"
              accept=".pdf,.docx,.xlsx,.txt,.md"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) upload.mutate(file);
                e.target.value = "";
              }}
            />
            <Button onClick={() => fileRef.current?.click()} disabled={upload.isPending}>
              {upload.isPending ? <Loader2 className="animate-spin" /> : <Upload />} Dosya seç
            </Button>
          </CardContent>
        </Card>
      )}

      <div className="mb-4">
        <NativeSelect
          aria-label="Departman filtresi"
          value={filter}
          onChange={(v) => {
            setFilter(v);
            setPage(1);
          }}
          options={[{ value: "", label: "Tüm departmanlar" }, ...(departments.data ?? []).map((d) => ({ value: d.code, label: d.name }))]}
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
                    <TableHead className="pl-4">Doküman</TableHead>
                    <TableHead>Departman</TableHead>
                    <TableHead>Durum</TableHead>
                    <TableHead className="hidden text-right sm:table-cell">Parça</TableHead>
                    <TableHead className="hidden md:table-cell">Yükleme</TableHead>
                    {isAdmin && <TableHead className="pr-4 text-right">İşlem</TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.length === 0 && <EmptyRow colSpan={6}>Erişebildiğiniz doküman yok.</EmptyRow>}
                  {data.items.map((d) => {
                    const view = statusView[d.status];
                    return (
                      <TableRow key={d.id}>
                        <TableCell className="pl-4">
                          <div className="flex items-center gap-2">
                            <FileText className="size-4 shrink-0 text-muted-foreground" />
                            <div className="min-w-0">
                              <p className="truncate font-medium">{d.fileName}</p>
                              <p className="text-xs text-muted-foreground">{size(d.sizeBytes)}</p>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell>{d.departmentName}</TableCell>
                        <TableCell>
                          <Badge className={cn("gap-1 border-0", view.className)} title={d.error ?? undefined}>
                            <view.icon className={cn("size-3", d.status === "Processing" && "animate-spin")} />
                            {view.label}
                            {d.status === "Failed" && ` (${d.attempts} deneme)`}
                          </Badge>
                          {d.error && <p className="mt-1 max-w-xs truncate text-xs text-rose-600">{d.error}</p>}
                        </TableCell>
                        <TableCell className="hidden text-right tabular-nums sm:table-cell">{d.chunkCount || "—"}</TableCell>
                        <TableCell className="hidden md:table-cell">
                          <p>{fmt.dateTime(d.uploadedAt)}</p>
                          <p className="text-xs text-muted-foreground">{d.uploadedBy}</p>
                        </TableCell>
                        {isAdmin && (
                          <TableCell className="pr-4 text-right">
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              aria-label="Yeniden işle"
                              onClick={() => action.mutate({ id: d.id, kind: "reprocess" })}
                            >
                              <RefreshCw />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              aria-label="Sil"
                              onClick={() => {
                                if (confirm(`${d.fileName} silinsin mi? Vektörleri de silinir.`)) action.mutate({ id: d.id, kind: "delete" });
                              }}
                            >
                              <Trash2 />
                            </Button>
                          </TableCell>
                        )}
                      </TableRow>
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
