"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import {
  Bot,
  Boxes,
  FileText,
  LayoutDashboard,
  LogOut,
  Menu,
  Package,
  ReceiptText,
  ShoppingCart,
  Users,
  ScrollText,
  KeyRound,
  type LucideIcon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuth } from "@/lib/auth";
import { roleLabel } from "@/lib/format";
import type { UserInfo } from "@/lib/types";
import { cn } from "@/lib/utils";

interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  visible?: (u: UserInfo) => boolean;
}

const nav: { title: string; items: NavItem[] }[] = [
  {
    title: "Genel",
    items: [
      { href: "/dashboard", label: "Gösterge paneli", icon: LayoutDashboard },
      { href: "/assistant", label: "Asistan", icon: Bot },
      { href: "/documents", label: "Dokümanlar", icon: FileText },
      { href: "/tokens", label: "Erişim token'ları", icon: KeyRound },
    ],
  },
  {
    title: "ERP",
    items: [
      { href: "/orders", label: "Siparişler", icon: ShoppingCart },
      { href: "/customers", label: "Müşteriler", icon: Users },
      { href: "/invoices", label: "Faturalar", icon: ReceiptText, visible: (u) => u.canSeeFinancials },
      { href: "/products", label: "Ürünler", icon: Package },
      { href: "/stock", label: "Stok", icon: Boxes },
    ],
  },
  {
    title: "Yönetim",
    items: [{ href: "/ai-logs", label: "AI sorgu günlüğü", icon: ScrollText, visible: (u) => u.roles.includes("Yonetici") }],
  },
];

function Sidebar({ user, onNavigate }: { user: UserInfo; onNavigate?: () => void }) {
  const pathname = usePathname();
  const { logout } = useAuth();
  const router = useRouter();

  return (
    <div className="flex h-full flex-col">
      <div className="flex h-14 items-center gap-2 border-b px-4 font-semibold">
        <span className="flex size-7 items-center justify-center rounded-md bg-primary text-primary-foreground">
          <Bot className="size-4" />
        </span>
        ERP Veri Asistanı
      </div>
      <nav className="flex-1 space-y-5 overflow-y-auto p-3">
        {nav.map((group) => {
          const items = group.items.filter((i) => !i.visible || i.visible(user));
          if (items.length === 0) return null;
          return (
            <div key={group.title} className="space-y-1">
              <p className="px-2 text-xs font-medium tracking-wide text-muted-foreground uppercase">{group.title}</p>
              {items.map((item) => {
                const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={onNavigate}
                    className={cn(
                      "flex items-center gap-2.5 rounded-md px-2 py-1.5 text-sm transition-colors",
                      active ? "bg-primary text-primary-foreground" : "text-foreground/80 hover:bg-muted",
                    )}
                  >
                    <item.icon className="size-4" />
                    {item.label}
                  </Link>
                );
              })}
            </div>
          );
        })}
      </nav>
      <div className="border-t p-3">
        <div className="mb-2 px-1">
          <p className="truncate text-sm font-medium">{user.fullName}</p>
          <p className="truncate text-xs text-muted-foreground">{user.email}</p>
          <div className="mt-1.5 flex flex-wrap gap-1">
            {user.roles.map((r) => (
              <Badge key={r} variant="secondary">
                {roleLabel[r] ?? r}
              </Badge>
            ))}
          </div>
        </div>
        <Button
          variant="ghost"
          className="w-full justify-start"
          onClick={async () => {
            await logout();
            router.replace("/login");
          }}
        >
          <LogOut /> Çıkış yap
        </Button>
      </div>
    </div>
  );
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, status } = useAuth();
  const router = useRouter();
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (status === "anonymous") router.replace("/login");
  }, [status, router]);

  if (status !== "authenticated" || !user) {
    return (
      <div className="flex min-h-screen">
        <div className="hidden w-60 border-r bg-background p-4 md:block">
          <Skeleton className="h-8 w-40" />
        </div>
        <div className="flex-1 space-y-4 p-6">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-40 w-full" />
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen">
      <aside className="sticky top-0 hidden h-screen w-60 shrink-0 border-r bg-background md:block">
        <Sidebar user={user} />
      </aside>
      {open && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div className="absolute inset-0 bg-black/40" onClick={() => setOpen(false)} />
          <aside className="absolute inset-y-0 left-0 w-64 bg-background shadow-xl">
            <Sidebar user={user} onNavigate={() => setOpen(false)} />
          </aside>
        </div>
      )}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b bg-background/80 px-4 backdrop-blur md:hidden">
          <Button variant="ghost" size="icon" onClick={() => setOpen(true)} aria-label="Menüyü aç">
            <Menu />
          </Button>
          <span className="font-semibold">ERP Veri Asistanı</span>
        </header>
        <main className="mx-auto w-full max-w-7xl flex-1 p-4 md:p-6">{children}</main>
      </div>
    </div>
  );
}
