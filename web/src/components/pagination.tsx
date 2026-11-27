import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { fmt } from "@/lib/format";

export function Pagination({
  page,
  totalPages,
  totalCount,
  onPage,
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  onPage: (page: number) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-2 border-t px-4 py-3 text-sm text-muted-foreground">
      <span>{fmt.number(totalCount)} kayıt</span>
      <div className="flex items-center gap-2">
        <span>
          Sayfa {page} / {Math.max(totalPages, 1)}
        </span>
        <Button variant="outline" size="icon-sm" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Önceki sayfa">
          <ChevronLeft />
        </Button>
        <Button
          variant="outline"
          size="icon-sm"
          disabled={page >= totalPages}
          onClick={() => onPage(page + 1)}
          aria-label="Sonraki sayfa"
        >
          <ChevronRight />
        </Button>
      </div>
    </div>
  );
}
