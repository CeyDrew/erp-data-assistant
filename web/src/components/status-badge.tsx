import { Badge } from "@/components/ui/badge";
import { invoiceStatusLabel, orderStatusLabel } from "@/lib/format";
import { cn } from "@/lib/utils";

const tone: Record<string, string> = {
  Draft: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300",
  Confirmed: "bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300",
  Shipped: "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300",
  Delivered: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300",
  Cancelled: "bg-rose-100 text-rose-700 dark:bg-rose-950 dark:text-rose-300",
  Open: "bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300",
  PartiallyPaid: "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300",
  Paid: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300",
  Overdue: "bg-rose-100 text-rose-700 dark:bg-rose-950 dark:text-rose-300",
};

export function StatusBadge({ status, kind = "order" }: { status: string; kind?: "order" | "invoice" }) {
  const label = (kind === "order" ? orderStatusLabel : invoiceStatusLabel)[status] ?? status;
  return <Badge className={cn("border-0", tone[status])}>{label}</Badge>;
}

export function OverdueBadge({ days }: { days: number }) {
  if (days <= 0) return null;
  return <Badge className={cn("border-0", tone.Overdue)}>{days} gün gecikme</Badge>;
}
