"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Banknote, CalendarClock, ShoppingCart, Wallet } from "lucide-react";
import { KpiCard } from "@/components/kpi-card";
import { PageHeader } from "@/components/page-header";
import { ErrorState, TableSkeleton } from "@/components/query-state";
import { StatusBadge } from "@/components/status-badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, qs } from "@/lib/api";
import { useUser } from "@/lib/auth";
import { fmt } from "@/lib/format";
import type { CustomerDetail, OrderListItem, Paged } from "@/lib/types";

export default function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const user = useUser();
  const customer = useQuery({ queryKey: ["customer", id], queryFn: () => api<CustomerDetail>(`/api/v1/customers/${id}`) });
  const orders = useQuery({
    queryKey: ["orders", { customerId: id, pageSize: 10 }],
    queryFn: () => api<Paged<OrderListItem>>(`/api/v1/orders${qs({ customerId: id, pageSize: 10 })}`),
    enabled: customer.isSuccess,
  });

  return (
    <>
      <Link href="/customers" className={buttonVariants({ variant: "ghost", size: "sm", className: "mb-2 -ml-2" })}>
        <ArrowLeft /> Müşteriler
      </Link>
      {customer.error ? (
        <ErrorState error={customer.error} />
      ) : customer.isPending ? (
        <TableSkeleton />
      ) : (
        <>
          <PageHeader
            title={customer.data.name}
            description={`${customer.data.code} · ${customer.data.city} · Temsilci: ${customer.data.salesRepresentative}`}
          />
          <div className="mb-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <KpiCard label="Sipariş" value={fmt.number(customer.data.orderCount)} hint={`${customer.data.openOrderCount} açık`} icon={ShoppingCart} />
            {user.canSeeFinancials && (
              <>
                <KpiCard label="Toplam ciro" value={fmt.money(customer.data.totalRevenue)} icon={Wallet} />
                <KpiCard
                  label="Açık bakiye"
                  value={fmt.money(customer.data.outstanding)}
                  hint={`Kredi limiti ${fmt.money(customer.data.creditLimit)}`}
                  icon={Banknote}
                  tone={customer.data.outstanding > customer.data.creditLimit ? "danger" : undefined}
                />
              </>
            )}
            <KpiCard label="Son sipariş" value={fmt.date(customer.data.lastOrderDate)} icon={CalendarClock} />
          </div>
          <Card className="gap-0 pb-0">
            <CardHeader className="pb-4">
              <CardTitle>Son siparişler</CardTitle>
            </CardHeader>
            {orders.isPending ? (
              <TableSkeleton rows={4} />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="pl-4">Sipariş</TableHead>
                    <TableHead>Tarih</TableHead>
                    <TableHead>Durum</TableHead>
                    {user.canSeeFinancials && <TableHead className="pr-4 text-right">Tutar</TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {orders.data?.items.map((o) => (
                    <TableRow key={o.id}>
                      <TableCell className="pl-4">
                        <Link href={`/orders/${o.id}`} className="font-medium hover:underline">
                          {o.orderNumber}
                        </Link>
                      </TableCell>
                      <TableCell>{fmt.date(o.orderDate)}</TableCell>
                      <TableCell>
                        <StatusBadge status={o.status} />
                      </TableCell>
                      {user.canSeeFinancials && <TableCell className="pr-4 text-right tabular-nums">{fmt.money(o.totalAmount)}</TableCell>}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Card>
        </>
      )}
    </>
  );
}
