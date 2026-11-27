"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Banknote, PackageX, ShoppingCart, TrendingDown, TrendingUp } from "lucide-react";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { KpiCard } from "@/components/kpi-card";
import { PageHeader } from "@/components/page-header";
import { ErrorState } from "@/components/query-state";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api";
import { useUser } from "@/lib/auth";
import { fmt } from "@/lib/format";
import type { DashboardSummary } from "@/lib/types";

function ChartTooltip({ active, payload, label }: { active?: boolean; payload?: { value: number }[]; label?: string }) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border bg-background px-3 py-2 text-sm shadow-md">
      <p className="text-muted-foreground">{label}</p>
      <p className="font-medium tabular-nums">{fmt.money(payload[0].value)}</p>
    </div>
  );
}

export default function DashboardPage() {
  const user = useUser();
  const { data, error, isPending } = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api<DashboardSummary>("/api/v1/reports/dashboard"),
  });

  if (error) return <ErrorState error={error} />;

  const change =
    data && data.revenueLastMonth > 0 ? (data.revenueThisMonth - data.revenueLastMonth) / data.revenueLastMonth : null;
  const monthly = data?.monthlyRevenue.map((r) => ({ ...r, short: r.label.split(" ")[0].slice(0, 3) })) ?? [];

  return (
    <>
      <PageHeader
        title={`Merhaba, ${user.fullName.split(" ")[0]}`}
        description={
          user.roles.includes("SatisTemsilcisi")
            ? "Rakamlar yalnızca sizin müşterilerinizi kapsar."
            : user.canSeeFinancials
              ? "Şirket geneli özet."
              : "Stok ve sevkiyat özeti. Finansal veriler rolünüz için gizlidir."
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {isPending ? (
          Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-28" />)
        ) : user.canSeeFinancials ? (
          <>
            <KpiCard
              label="Bu ay ciro"
              value={fmt.money(data.revenueThisMonth)}
              icon={change != null && change < 0 ? TrendingDown : TrendingUp}
              hint={change != null ? `Geçen ay: ${fmt.money(data.revenueLastMonth)}` : undefined}
            />
            <KpiCard label="Bu ay sipariş" value={fmt.number(data.ordersThisMonth)} icon={ShoppingCart} />
            <KpiCard label="Vadesi geçmiş alacak" value={fmt.money(data.overdue)} icon={Banknote} tone="danger" />
            <KpiCard label="Kritik stok kalemi" value={fmt.number(data.criticalStockItems)} icon={PackageX} tone="warning" />
          </>
        ) : (
          <>
            <KpiCard label="Bu ay sipariş" value={fmt.number(data.ordersThisMonth)} icon={ShoppingCart} />
            <KpiCard label="Açık sipariş" value={fmt.number(data.openOrders)} icon={AlertTriangle} />
            <KpiCard label="Kritik stok kalemi" value={fmt.number(data.criticalStockItems)} icon={PackageX} tone="warning" />
          </>
        )}
      </div>

      {user.canSeeFinancials && (
        <div className="mt-4 grid gap-4 lg:grid-cols-5">
          <Card className="lg:col-span-3">
            <CardHeader>
              <CardTitle>Aylık ciro</CardTitle>
              <CardDescription>Son 12 ay, KDV hariç</CardDescription>
            </CardHeader>
            <CardContent className="h-72">
              {isPending ? (
                <Skeleton className="h-full" />
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={monthly} margin={{ left: 8, right: 8 }}>
                    <defs>
                      <linearGradient id="rev" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="var(--color-chart-1)" stopOpacity={0.35} />
                        <stop offset="100%" stopColor="var(--color-chart-1)" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid vertical={false} strokeDasharray="3 3" className="stroke-border" />
                    <XAxis dataKey="short" tickLine={false} axisLine={false} fontSize={12} />
                    <YAxis tickFormatter={fmt.compact} tickLine={false} axisLine={false} fontSize={12} width={48} />
                    <Tooltip content={<ChartTooltip />} labelFormatter={(_, p) => p?.[0]?.payload?.label} />
                    <Area type="monotone" dataKey="revenue" stroke="var(--color-chart-1)" strokeWidth={2} fill="url(#rev)" />
                  </AreaChart>
                </ResponsiveContainer>
              )}
            </CardContent>
          </Card>
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle>Kategoriye göre ciro</CardTitle>
              <CardDescription>Son 12 ay</CardDescription>
            </CardHeader>
            <CardContent className="h-72">
              {isPending ? (
                <Skeleton className="h-full" />
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={data.revenueByCategory.slice(0, 8)} layout="vertical" margin={{ left: 8, right: 8 }}>
                    <CartesianGrid horizontal={false} strokeDasharray="3 3" className="stroke-border" />
                    <XAxis type="number" tickFormatter={fmt.compact} tickLine={false} axisLine={false} fontSize={12} />
                    <YAxis type="category" dataKey="label" tickLine={false} axisLine={false} fontSize={12} width={110} />
                    <Tooltip content={<ChartTooltip />} cursor={{ className: "fill-muted" }} />
                    <Bar dataKey="revenue" fill="var(--color-chart-2)" radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>En çok satan ürünler</CardTitle>
          <CardDescription>Son 12 ay, {user.canSeeFinancials ? "ciroya" : "miktara"} göre</CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-10 pl-6">#</TableHead>
                <TableHead>Ürün</TableHead>
                <TableHead className="hidden sm:table-cell">Kategori</TableHead>
                <TableHead className="text-right">Miktar</TableHead>
                {user.canSeeFinancials && <TableHead className="pr-6 text-right">Ciro</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {data?.topProducts.map((p) => (
                <TableRow key={p.productId}>
                  <TableCell className="pl-6 text-muted-foreground">{p.rank}</TableCell>
                  <TableCell>
                    <Link href={`/products/${p.productId}`} className="font-medium hover:underline">
                      {p.name}
                    </Link>
                    <span className="block text-xs text-muted-foreground">{p.sku}</span>
                  </TableCell>
                  <TableCell className="hidden sm:table-cell">{p.category}</TableCell>
                  <TableCell className="text-right tabular-nums">{fmt.number(p.quantity)}</TableCell>
                  {user.canSeeFinancials && <TableCell className="pr-6 text-right tabular-nums">{fmt.money(p.revenue)}</TableCell>}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </>
  );
}
