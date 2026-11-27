import { AlertTriangle } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api";

export function TableSkeleton({ rows = 8 }: { rows?: number }) {
  return (
    <div className="space-y-2 p-4">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-8 w-full" />
      ))}
    </div>
  );
}

export function ErrorState({ error }: { error: unknown }) {
  const message =
    error instanceof ApiError
      ? error.status === 403
        ? "Bu veriyi görme yetkiniz yok."
        : error.status === 404
          ? "Kayıt bulunamadı veya erişiminiz yok."
          : error.message
      : "Beklenmeyen bir hata oluştu.";
  return (
    <div className="flex items-center gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
      <AlertTriangle className="size-4" />
      {message}
    </div>
  );
}

export function EmptyRow({ colSpan, children = "Kayıt yok." }: { colSpan: number; children?: React.ReactNode }) {
  return (
    <tr>
      <td colSpan={colSpan} className="p-8 text-center text-sm text-muted-foreground">
        {children}
      </td>
    </tr>
  );
}
