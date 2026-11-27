"use client";

import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { NativeSelect } from "@/components/filters";
import { PageHeader } from "@/components/page-header";
import { Pagination } from "@/components/pagination";
import { EmptyRow, ErrorState, TableSkeleton } from "@/components/query-state";
import { OverdueBadge, StatusBadge } from "@/components/status-badge";
import { Card } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, qs } from "@/lib/api";
import { fmt, invoiceStatusLabel } from "@/lib/format";
import type { InvoiceListItem, Paged } from "@/lib/types";

export default function InvoicesPage() {
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState("");

  const params = { page, pageSize: 20, overdueOnly: filter === "overdue", status: filter === "overdue" ? undefined : filter };
  const { data, error, isPending } = useQuery({
    queryKey: ["invoices", params],
    queryFn: () => api<Paged<InvoiceListItem>>(`/api/v1/invoices${qs(params)}`),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader title="Faturalar" description="Tutarlar KDV dahil." />
      <div className="mb-4">
        <NativeSelect
          aria-label="Filtre"
          value={filter}
          onChange={(v) => {
            setFilter(v);
            setPage(1);
          }}
          options={[
            { value: "", label: "Tüm faturalar" },
            { value: "overdue", label: "Vadesi geçenler" },
            ...Object.entries(invoiceStatusLabel).map(([value, label]) => ({ value, label })),
          ]}
        />
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
                    <TableHead className="pl-4">Fatura</TableHead>
                    <TableHead>Müşteri</TableHead>
                    <TableHead className="hidden md:table-cell">Vade</TableHead>
                    <TableHead>Durum</TableHead>
                    <TableHead className="hidden text-right sm:table-cell">Toplam</TableHead>
                    <TableHead className="pr-4 text-right">Kalan</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.length === 0 && <EmptyRow colSpan={6} />}
                  {data.items.map((i) => (
                    <TableRow key={i.id}>
                      <TableCell className="pl-4">
                        <span className="font-medium">{i.invoiceNumber}</span>
                        <span className="block text-xs text-muted-foreground">{fmt.date(i.issueDate)}</span>
                      </TableCell>
                      <TableCell className="max-w-56 truncate">{i.customerName}</TableCell>
                      <TableCell className="hidden md:table-cell">{fmt.date(i.dueDate)}</TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          <StatusBadge status={i.status} kind="invoice" />
                          <OverdueBadge days={i.daysOverdue} />
                        </div>
                      </TableCell>
                      <TableCell className="hidden text-right tabular-nums sm:table-cell">{fmt.money(i.totalAmount)}</TableCell>
                      <TableCell className="pr-4 text-right tabular-nums">{fmt.money(i.outstanding)}</TableCell>
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
