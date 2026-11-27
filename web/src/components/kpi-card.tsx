import type { LucideIcon } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export function KpiCard({
  label,
  value,
  hint,
  icon: Icon,
  tone,
}: {
  label: string;
  value: React.ReactNode;
  hint?: React.ReactNode;
  icon: LucideIcon;
  tone?: "warning" | "danger";
}) {
  return (
    <Card>
      <CardContent className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="mt-1 truncate text-2xl font-semibold tabular-nums">{value}</p>
          {hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
        </div>
        <span
          className={cn(
            "flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted",
            tone === "warning" && "bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300",
            tone === "danger" && "bg-rose-100 text-rose-700 dark:bg-rose-950 dark:text-rose-300",
          )}
        >
          <Icon className="size-4" />
        </span>
      </CardContent>
    </Card>
  );
}
