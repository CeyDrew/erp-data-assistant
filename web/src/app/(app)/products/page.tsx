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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useDebounced } from "@/hooks/use-debounced";
import { api, qs } from "@/lib/api";
import { useUser } from "@/lib/auth";
import { fmt } from "@/lib/format";
import type { CategoryItem, Paged, ProductListItem } from "@/lib/types";

export default function ProductsPage() {
  const user = useUser();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const q = useDebounced(search);

  const categories = useQuery({
    queryKey: ["categories"],
    queryFn: () => api<CategoryItem[]>("/api/v1/products/categories"),
    staleTime: Infinity,
  });
  const params = { page, pageSize: 20, search: q, categoryId };
  const { data, error, isPending } = useQuery({
    queryKey: ["products", params],
    queryFn: () => api<Paged<ProductListItem>>(`/api/v1/products${qs(params)}`),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader title="Ürünler" />
      <div className="mb-4 flex flex-wrap gap-2">
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
          aria-label="Kategori"
          value={categoryId}
          onChange={(v) => {
            setCategoryId(v);
            setPage(1);
          }}
          options={[
            { value: "", label: "Tüm kategoriler" },
            ...(categories.data ?? []).map((c) => ({ value: String(c.id), label: `${c.name} (${c.productCount})` })),
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
                    <TableHead className="pl-4">Ürün</TableHead>
                    <TableHead className="hidden sm:table-cell">Kategori</TableHead>
                    <TableHead className="text-right">Stok</TableHead>
                    {user.canSeeFinancials && <TableHead className="pr-4 text-right">Birim fiyat</TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.length === 0 && <EmptyRow colSpan={4} />}
                  {data.items.map((p) => (
                    <TableRow key={p.id}>
                      <TableCell className="pl-4">
                        <Link href={`/products/${p.id}`} className="font-medium hover:underline">
                          {p.name}
                        </Link>
                        <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                          {p.sku}
                          {!p.isActive && <Badge variant="outline">Pasif</Badge>}
                        </span>
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">{p.category}</TableCell>
                      <TableCell className="text-right tabular-nums">
                        {fmt.number(p.totalStock)} <span className="text-xs text-muted-foreground">{p.unit}</span>
                      </TableCell>
                      {user.canSeeFinancials && <TableCell className="pr-4 text-right tabular-nums">{fmt.moneyExact(p.unitPrice)}</TableCell>}
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
