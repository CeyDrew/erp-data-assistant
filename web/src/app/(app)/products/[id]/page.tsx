"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { ErrorState, TableSkeleton } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api";
import { fmt } from "@/lib/format";
import type { ProductDetail, StockMovementItem } from "@/lib/types";
import { cn } from "@/lib/utils";

const movementLabel: Record<string, string> = {
  Purchase: "Satın alma",
  Sale: "Satış",
  Adjustment: "Düzeltme",
  TransferIn: "Transfer giriş",
  TransferOut: "Transfer çıkış",
  Return: "İade",
};

export default function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const product = useQuery({ queryKey: ["product", id], queryFn: () => api<ProductDetail>(`/api/v1/products/${id}`) });
  const movements = useQuery({
    queryKey: ["product-movements", id],
    queryFn: () => api<StockMovementItem[]>(`/api/v1/products/${id}/movements?take=25`),
  });

  return (
    <>
      <Link href="/products" className={buttonVariants({ variant: "ghost", size: "sm", className: "mb-2 -ml-2" })}>
        <ArrowLeft /> Ürünler
      </Link>
      {product.error ? (
        <ErrorState error={product.error} />
      ) : product.isPending ? (
        <TableSkeleton />
      ) : (
        <>
          <PageHeader
            title={product.data.name}
            description={`${product.data.sku} · ${product.data.category}`}
            actions={!product.data.isActive && <Badge variant="outline">Pasif</Badge>}
          />
          <div className="grid gap-4 lg:grid-cols-3">
            <Card>
              <CardHeader>
                <CardTitle>Bilgiler</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <Row label="Birim" value={product.data.unit} />
                <Row label="Yeniden sipariş seviyesi" value={fmt.number(product.data.reorderLevel)} />
                {product.data.unitPrice != null && <Row label="Satış fiyatı" value={fmt.moneyExact(product.data.unitPrice)} />}
                {product.data.unitCost != null && <Row label="Maliyet" value={fmt.moneyExact(product.data.unitCost)} />}
                {product.data.unitPrice != null && product.data.unitCost != null && (
                  <Row label="Brüt marj" value={fmt.percent(1 - product.data.unitCost / product.data.unitPrice)} />
                )}
              </CardContent>
            </Card>
            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle>Depo bazında stok</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-3 sm:grid-cols-3">
                {product.data.stock.map((s) => {
                  const critical = s.quantity < product.data.reorderLevel;
                  return (
                    <div key={s.warehouseId} className={cn("rounded-lg border p-3", critical && "border-amber-400 bg-amber-50 dark:bg-amber-950/30")}>
                      <p className="text-xs text-muted-foreground">{s.warehouseName}</p>
                      <p className="mt-1 text-xl font-semibold tabular-nums">
                        {fmt.number(s.quantity)} <span className="text-sm font-normal text-muted-foreground">{product.data.unit}</span>
                      </p>
                      {critical && <p className="text-xs text-amber-700 dark:text-amber-300">Kritik seviyenin altında</p>}
                    </div>
                  );
                })}
              </CardContent>
            </Card>
          </div>
          <Card className="mt-4 gap-0 pb-0">
            <CardHeader className="pb-4">
              <CardTitle>Son stok hareketleri</CardTitle>
            </CardHeader>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-4">Tarih</TableHead>
                  <TableHead>Depo</TableHead>
                  <TableHead>Tür</TableHead>
                  <TableHead className="hidden sm:table-cell">Referans</TableHead>
                  <TableHead className="pr-4 text-right">Miktar</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {movements.data?.map((m, i) => (
                  <TableRow key={i}>
                    <TableCell className="pl-4">{fmt.dateTime(m.occurredAt)}</TableCell>
                    <TableCell>{m.warehouseCode}</TableCell>
                    <TableCell>{movementLabel[m.type] ?? m.type}</TableCell>
                    <TableCell className="hidden font-mono text-xs sm:table-cell">{m.reference}</TableCell>
                    <TableCell className={cn("pr-4 text-right tabular-nums", m.quantity < 0 ? "text-rose-600" : "text-emerald-600")}>
                      {m.quantity > 0 ? "+" : ""}
                      {fmt.number(m.quantity)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>
        </>
      )}
    </>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-4">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium tabular-nums">{value}</span>
    </div>
  );
}
