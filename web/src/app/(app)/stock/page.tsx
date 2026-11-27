"use client";

import Link from "next/link";
import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { NativeSelect, SearchInput } from "@/components/filters";
import { PageHeader } from "@/components/page-header";
import { Pagination } from "@/components/pagination";
import { EmptyRow, ErrorState, TableSkeleton } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useDebounced } from "@/hooks/use-debounced";
import { api, qs } from "@/lib/api";
import { fmt } from "@/lib/format";
import type { Paged, StockItem, WarehouseItem } from "@/lib/types";

export default function StockPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [warehouse, setWarehouse] = useState("");
  const [criticalOnly, setCriticalOnly] = useState(false);
  const q = useDebounced(search);

  const warehouses = useQuery({
    queryKey: ["warehouses"],
    queryFn: () => api<WarehouseItem[]>("/api/v1/stock/warehouses"),
    staleTime: Infinity,
  });
  const params = { page, pageSize: 25, search: q, warehouse, criticalOnly };
  const { data, error, isPending } = useQuery({
    queryKey: ["stock", params],
    queryFn: () => api<Paged<StockItem>>(`/api/v1/stock${qs(params)}`),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader title="Stok" description="Bakiyeler stok hareketlerinden hesaplanır; kritik kalemler üstte." />
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <SearchInput
          value={search}
          onChange={(v) => {
            setSearch(v);
            setPage(1);
          }}
          placeholder="Ürün adı veya SKU…"
          className="w-full sm:w-72"
        />
        <NativeSelect
          aria-label="Depo"
          value={warehouse}
          onChange={(v) => {
            setWarehouse(v);
            setPage(1);
          }}
          options={[{ value: "", label: "Tüm depolar" }, ...(warehouses.data ?? []).map((w) => ({ value: w.code, label: w.name }))]}
        />
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={criticalOnly}
            onChange={(e) => {
              setCriticalOnly(e.target.checked);
              setPage(1);
            }}
            className="size-4 accent-primary"
          />
          Yalnızca kritik
        </label>
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
                    <TableHead className="pl-4">Ürün</TableHead>
                    <TableHead>Depo</TableHead>
                    <TableHead className="hidden w-48 md:table-cell">Seviye</TableHead>
                    <TableHead className="pr-4 text-right">Bakiye</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.length === 0 && <EmptyRow colSpan={4} />}
                  {data.items.map((s) => (
                    <TableRow key={`${s.productId}-${s.warehouseCode}`}>
                      <TableCell className="pl-4">
                        <Link href={`/products/${s.productId}`} className="font-medium hover:underline">
                          {s.productName}
                        </Link>
                        <span className="block text-xs text-muted-foreground">{s.sku}</span>
                      </TableCell>
                      <TableCell>{s.warehouseCode}</TableCell>
                      <TableCell className="hidden md:table-cell">
                        <Progress value={Math.min(100, (s.quantity / Math.max(1, s.reorderLevel * 6)) * 100)} />
                      </TableCell>
                      <TableCell className="pr-4 text-right tabular-nums">
                        <div className="flex items-center justify-end gap-2">
                          {s.isCritical && <Badge className="border-0 bg-amber-100 text-amber-800">Kritik</Badge>}
                          {fmt.number(s.quantity)}
                        </div>
                        <span className="block text-xs text-muted-foreground">min {fmt.number(s.reorderLevel)}</span>
                      </TableCell>
                    </TableRow>
                  ))}
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
