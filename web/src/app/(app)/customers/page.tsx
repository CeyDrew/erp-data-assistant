"use client";

import Link from "next/link";
import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { SearchInput } from "@/components/filters";
import { PageHeader } from "@/components/page-header";
import { Pagination } from "@/components/pagination";
import { EmptyRow, ErrorState, TableSkeleton } from "@/components/query-state";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useDebounced } from "@/hooks/use-debounced";
import { api, qs } from "@/lib/api";
import { useUser } from "@/lib/auth";
import type { CustomerListItem, Paged } from "@/lib/types";

export default function CustomersPage() {
  const user = useUser();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [city, setCity] = useState("");
  const q = useDebounced(search);
  const c = useDebounced(city);

  const params = { page, pageSize: 20, search: q, city: c };
  const { data, error, isPending } = useQuery({
    queryKey: ["customers", params],
    queryFn: () => api<Paged<CustomerListItem>>(`/api/v1/customers${qs(params)}`),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader
        title="Müşteriler"
        description={user.roles.includes("SatisTemsilcisi") ? "Yalnızca size atanmış müşteriler listelenir." : undefined}
      />
      <div className="mb-4 flex flex-wrap gap-2">
        <SearchInput
          value={search}
          onChange={(v) => {
            setSearch(v);
            setPage(1);
          }}
          placeholder="Ad, kod veya vergi no…"
          className="w-full sm:w-72"
        />
        <Input
          value={city}
          onChange={(e) => {
            setCity(e.target.value);
            setPage(1);
          }}
          placeholder="Şehir"
          className="w-40"
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
                    <TableHead className="pl-4">Müşteri</TableHead>
                    <TableHead className="hidden sm:table-cell">Şehir</TableHead>
                    <TableHead className="hidden md:table-cell">Vergi no</TableHead>
                    <TableHead className="pr-4">Temsilci</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.length === 0 && <EmptyRow colSpan={4} />}
                  {data.items.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell className="pl-4">
                        <Link href={`/customers/${c.id}`} className="font-medium hover:underline">
                          {c.name}
                        </Link>
                        <span className="block text-xs text-muted-foreground">{c.code}</span>
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">{c.city}</TableCell>
                      <TableCell className="hidden font-mono text-xs md:table-cell">{c.taxNumber}</TableCell>
                      <TableCell className="pr-4">{c.salesRepresentative}</TableCell>
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
