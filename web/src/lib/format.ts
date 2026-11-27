const currency = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", maximumFractionDigits: 0 });
const currencyExact = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });
const compact = new Intl.NumberFormat("tr-TR", { notation: "compact", maximumFractionDigits: 1 });
const number = new Intl.NumberFormat("tr-TR");
const date = new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" });
const dateTime = new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium", timeStyle: "short" });

export const fmt = {
  money: (v: number | null | undefined) => (v == null ? "—" : currency.format(v)),
  moneyExact: (v: number | null | undefined) => (v == null ? "—" : currencyExact.format(v)),
  compact: (v: number) => compact.format(v),
  number: (v: number) => number.format(v),
  date: (v: string | null | undefined) => (v ? date.format(new Date(v)) : "—"),
  dateTime: (v: string | null | undefined) => (v ? dateTime.format(new Date(v)) : "—"),
  percent: (v: number) => `%${number.format(Math.round(v * 1000) / 10)}`,
};

export const orderStatusLabel: Record<string, string> = {
  Draft: "Taslak",
  Confirmed: "Onaylı",
  Shipped: "Sevk edildi",
  Delivered: "Teslim edildi",
  Cancelled: "İptal",
};

export const invoiceStatusLabel: Record<string, string> = {
  Open: "Açık",
  PartiallyPaid: "Kısmi ödendi",
  Paid: "Ödendi",
  Cancelled: "İptal",
};

export const roleLabel: Record<string, string> = {
  Yonetici: "Yönetici",
  SatisTemsilcisi: "Satış temsilcisi",
  DepoSorumlusu: "Depo sorumlusu",
};
