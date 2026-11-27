// Panelin ekran görüntülerini docs/screenshots altına alır.
// Kullanım: API ve web çalışırken `npm run screenshots` (Chrome kurulu olmalı).
import { mkdir } from "node:fs/promises";
import path from "node:path";
import { chromium } from "playwright-core";

const BASE = process.env.WEB_URL ?? "http://localhost:3000";
const OUT = path.resolve(import.meta.dirname, "../../docs/screenshots");
const only = process.argv.slice(2);

async function ask(page, question) {
  await page.getByPlaceholder("Örn.").fill(question);
  await page.keyboard.press("Enter");
  await page.getByText(/kayıt #\d+/).last().waitFor({ timeout: 180_000 });
}

const shots = [
  { user: "yonetici@erp.local", name: "assistant", path: "/assistant", run: (p) => ask(p, "Geçen ay en çok satan 5 ürün hangileri?") },
  { user: "temsilci@erp.local", name: "assistant-rag", path: "/assistant", run: (p) => ask(p, "Garanti şartnamesine göre ayıpsız ürünlerde iade süresi kaç gün?") },
  { user: "yonetici@erp.local", name: "documents", path: "/documents" },
  { user: "yonetici@erp.local", name: "ai-logs", path: "/ai-logs", run: (p) => p.locator("tbody tr").first().click() },
  { user: "yonetici@erp.local", name: "dashboard", path: "/dashboard" },
  { user: "yonetici@erp.local", name: "orders", path: "/orders" },
  { user: "yonetici@erp.local", name: "order-detail", path: "/orders/19950" },
  { user: "yonetici@erp.local", name: "stock", path: "/stock?critical", action: async (p) => p.getByLabel("Yalnızca kritik").check() },
  { user: "temsilci@erp.local", name: "sales-rep-customers", path: "/customers" },
  { user: "depo@erp.local", name: "warehouse-dashboard", path: "/dashboard" },
];

// Windows + Docker Desktop: localhost IPv6 (::1) yönlendirmesi zaman zaman takılıyor; origin değişmeden IPv4 kullan.
const browser = await chromium.launch({ channel: "chrome", args: ["--host-resolver-rules=MAP localhost 127.0.0.1"] });
await mkdir(OUT, { recursive: true });

for (const user of [...new Set(shots.map((s) => s.user))]) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: "tr-TR", deviceScaleFactor: 1 });
  const page = await context.newPage();
  await page.goto(`${BASE}/login`, { waitUntil: "domcontentloaded" });
  await page.getByLabel("E-posta").fill(user);
  await page.getByLabel("Şifre").fill("Demo123!");
  await page.getByRole("button", { name: "Giriş yap" }).click();
  await page.waitForURL("**/dashboard", { waitUntil: "commit" });

  for (const shot of shots.filter((s) => s.user === user && (only.length === 0 || only.includes(s.name)))) {
    await page.goto(`${BASE}${shot.path.split("?")[0]}`, { waitUntil: "domcontentloaded" });
    await page.waitForLoadState("networkidle", { timeout: 10_000 }).catch(() => {});
    if (shot.action) {
      await shot.action(page);
      await page.waitForLoadState("networkidle", { timeout: 10_000 }).catch(() => {});
    }
    if (shot.run) await shot.run(page);
    await page.waitForTimeout(800);
    await page.screenshot({ path: path.join(OUT, `${shot.name}.png`) });
    console.log("✓", shot.name);
  }
  await context.close();
}

await browser.close();
