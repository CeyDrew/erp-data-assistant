"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { ErrorState, TableSkeleton } from "@/components/query-state";
import { StatusBadge } from "@/components/status-badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api";
import { useUser } from "@/lib/auth";
import { fmt } from "@/lib/format";
import type { OrderDetail } from "@/lib/types";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-medium">{children}</dd>
    </div>
  );
}

export default function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const user = useUser();
  const { data, error, isPending } = useQuery({
    queryKey: ["order", id],
    queryFn: () => api<OrderDetail>(`/api/v1/orders/${id}`),
  });

  return (
    <>
      <Link href="/orders" className={buttonVariants({ variant: "ghost", size: "sm", className: "mb-2 -ml-2" })}>
        <ArrowLeft /> Siparişler
      </Link>
      {error ? (
        <ErrorState error={error} />
      ) : isPending ? (
        <TableSkeleton />
      ) : (
        <>
          <PageHeader title={data.orderNumber} description={data.customerName} actions={<StatusBadge status={data.status} />} />
          <Card className="mb-4">
            <CardContent>
              <dl className="grid grid-cols-2 gap-4 md:grid-cols-5">
                <Field label="Müşteri">
                  <Link href={`/customers/${data.customerId}`} className="hover:underline">
                    {data.customerName}
                  </Link>
                </Field>
                <Field label="Temsilci">{data.salesRepresentative}</Field>
                <Field label="Sipariş tarihi">{fmt.dateTime(data.orderDate)}</Field>
                <Field label="Sevk">{data.shippedAt ? `${fmt.date(data.shippedAt)} · ${data.warehouseCode}` : data.warehouseCode}</Field>
                {user.canSeeFinancials && <Field label="Fatura">{data.invoiceNumber ?? "—"}</Field>}
              </dl>
            </CardContent>
          </Card>
          <Card className="gap-0 pb-0">
            <CardHeader className="pb-4">
              <CardTitle>Kalemler</CardTitle>
            </CardHeader>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-4">Ürün</TableHead>
                  <TableHead className="text-right">Miktar</TableHead>
                  {user.canSeeFinancials && (
                    <>
                      <TableHead className="hidden text-right sm:table-cell">Birim fiyat</TableHead>
                      <TableHead className="hidden text-right sm:table-cell">İskonto</TableHead>
                      <TableHead className="pr-4 text-right">Tutar</TableHead>
                    </>
                  )}
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.lines.map((l) => (
                  <TableRow key={l.productId}>
                    <TableCell className="pl-4">
                      <Link href={`/products/${l.productId}`} className="font-medium hover:underline">
                        {l.productName}
                      </Link>
                      <span className="block text-xs text-muted-foreground">{l.sku}</span>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {fmt.number(l.quantity)} {l.unit}
                    </TableCell>
                    {user.canSeeFinancials && (
                      <>
                        <TableCell className="hidden text-right tabular-nums sm:table-cell">{fmt.moneyExact(l.unitPrice)}</TableCell>
                        <TableCell className="hidden text-right tabular-nums sm:table-cell">
                          {l.discountRate ? fmt.percent(l.discountRate) : "—"}
                        </TableCell>
                        <TableCell className="pr-4 text-right tabular-nums">{fmt.moneyExact(l.lineTotal)}</TableCell>
                      </>
                    )}
                  </TableRow>
                ))}
              </TableBody>
              {user.canSeeFinancials && (
                <TableFooter>
                  <TableRow>
                    <TableCell className="pl-4" colSpan={4}>
                      Toplam (KDV hariç)
                    </TableCell>
                    <TableCell className="pr-4 text-right tabular-nums">{fmt.moneyExact(data.totalAmount)}</TableCell>
                  </TableRow>
                </TableFooter>
              )}
            </Table>
          </Card>
        </>
      )}
    </>
  );
}
