"use client";

import Link from "next/link";
import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { NativeSelect, SearchInput } from "@/components/filters";
import { PageHeader } from "@/components/page-header";
import { Pagination } from "@/components/pagination";
import { EmptyRow, ErrorState, TableSkeleton } from "@/components/query-state";
import { StatusBadge } from "@/components/status-badge";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useDebounced } from "@/hooks/use-debounced";
import { api, qs } from "@/lib/api";
import { useUser } from "@/lib/auth";
import { fmt, orderStatusLabel } from "@/lib/format";
import type { OrderListItem, Paged } from "@/lib/types";

export default function OrdersPage() {
  const user = useUser();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const q = useDebounced(search);

  const params = { page, pageSize: 20, search: q, status: status === "open" ? undefined : status, openOnly: status === "open", from, to };
  const { data, error, isPending } = useQuery({
    queryKey: ["orders", params],
    queryFn: () => api<Paged<OrderListItem>>(`/api/v1/orders${qs(params)}`),
    placeholderData: keepPreviousData,
  });

  const reset = <T,>(set: (v: T) => void) => (v: T) => {
    set(v);
    setPage(1);
  };

  return (
    <>
      <PageHeader title="Siparişler" description="Tarih, durum ve sipariş numarasına göre filtreleyin." />
      <div className="mb-4 flex flex-wrap gap-2">
        <SearchInput value={search} onChange={reset(setSearch)} placeholder="Sipariş no…" className="w-full sm:w-56" />
        <NativeSelect
          aria-label="Durum"
          value={status}
          onChange={reset(setStatus)}
          options={[
            { value: "", label: "Tüm durumlar" },
            { value: "open", label: "Açık (taslak + onaylı)" },
            ...Object.entries(orderStatusLabel).map(([value, label]) => ({ value, label })),
          ]}
        />
        <Input type="date" aria-label="Başlangıç" value={from} onChange={(e) => reset(setFrom)(e.target.value)} className="w-auto" />
        <Input type="date" aria-label="Bitiş" value={to} onChange={(e) => reset(setTo)(e.target.value)} className="w-auto" />
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
                    <TableHead className="pl-4">Sipariş</TableHead>
                    <TableHead>Müşteri</TableHead>
                    <TableHead className="hidden md:table-cell">Tarih</TableHead>
                    <TableHead>Durum</TableHead>
                    <TableHead className="hidden sm:table-cell">Depo</TableHead>
                    {user.canSeeFinancials && <TableHead className="pr-4 text-right">Tutar</TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.length === 0 && <EmptyRow colSpan={6} />}
                  {data.items.map((o) => (
                    <TableRow key={o.id}>
                      <TableCell className="pl-4">
                        <Link href={`/orders/${o.id}`} className="font-medium hover:underline">
                          {o.orderNumber}
                        </Link>
                        <span className="block text-xs text-muted-foreground">{o.lineCount} kalem</span>
                      </TableCell>
                      <TableCell className="max-w-56 truncate">
                        <Link href={`/customers/${o.customerId}`} className="hover:underline">
                          {o.customerName}
                        </Link>
                      </TableCell>
                      <TableCell className="hidden md:table-cell">{fmt.date(o.orderDate)}</TableCell>
                      <TableCell>
                        <StatusBadge status={o.status} />
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">{o.warehouseCode}</TableCell>
                      {user.canSeeFinancials && <TableCell className="pr-4 text-right tabular-nums">{fmt.money(o.totalAmount)}</TableCell>}
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
