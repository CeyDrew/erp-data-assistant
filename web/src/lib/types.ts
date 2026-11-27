export type Role = "Yonetici" | "SatisTemsilcisi" | "DepoSorumlusu";

export interface UserInfo {
  id: string;
  email: string;
  fullName: string;
  roles: Role[];
  salesRepresentativeId: number | null;
  canSeeFinancials: boolean;
  documentDepartments: string[] | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserInfo;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type OrderStatus = "Draft" | "Confirmed" | "Shipped" | "Delivered" | "Cancelled";
export type InvoiceStatus = "Open" | "PartiallyPaid" | "Paid" | "Cancelled";

export interface ProductListItem {
  id: number;
  sku: string;
  name: string;
  category: string;
  unit: string;
  unitPrice: number | null;
  isActive: boolean;
  totalStock: number;
}

export interface WarehouseStock {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  quantity: number;
}

export interface ProductDetail {
  id: number;
  sku: string;
  name: string;
  categoryId: number;
  category: string;
  unit: string;
  unitPrice: number | null;
  unitCost: number | null;
  reorderLevel: number;
  isActive: boolean;
  stock: WarehouseStock[];
}

export interface StockMovementItem {
  occurredAt: string;
  warehouseCode: string;
  type: string;
  quantity: number;
  reference: string | null;
}

export interface CategoryItem {
  id: number;
  name: string;
  productCount: number;
}

export interface StockItem {
  productId: number;
  sku: string;
  productName: string;
  warehouseCode: string;
  warehouseName: string;
  quantity: number;
  reorderLevel: number;
  isCritical: boolean;
}

export interface WarehouseItem {
  id: number;
  code: string;
  name: string;
  city: string;
}

export interface CustomerListItem {
  id: number;
  code: string;
  name: string;
  city: string;
  taxNumber: string;
  salesRepresentativeId: number;
  salesRepresentative: string;
}

export interface CustomerDetail extends CustomerListItem {
  email: string | null;
  phone: string | null;
  creditLimit: number;
  orderCount: number;
  openOrderCount: number;
  totalRevenue: number;
  outstanding: number;
  lastOrderDate: string | null;
}

export interface OrderListItem {
  id: number;
  orderNumber: string;
  customerId: number;
  customerName: string;
  orderDate: string;
  status: OrderStatus;
  totalAmount: number | null;
  warehouseCode: string;
  lineCount: number;
}

export interface OrderLineItem {
  productId: number;
  sku: string;
  productName: string;
  unit: string;
  quantity: number;
  unitPrice: number | null;
  discountRate: number | null;
  lineTotal: number | null;
}

export interface OrderDetail {
  id: number;
  orderNumber: string;
  customerId: number;
  customerName: string;
  salesRepresentative: string;
  orderDate: string;
  status: OrderStatus;
  shippedAt: string | null;
  warehouseCode: string;
  totalAmount: number | null;
  invoiceNumber: string | null;
  lines: OrderLineItem[];
}

export interface InvoiceListItem {
  id: number;
  invoiceNumber: string;
  orderId: number;
  customerId: number;
  customerName: string;
  issueDate: string;
  dueDate: string;
  totalAmount: number;
  paidAmount: number;
  outstanding: number;
  status: InvoiceStatus;
  daysOverdue: number;
}

export interface SalesSummaryRow {
  key: string;
  label: string;
  orderCount: number;
  revenue: number;
}

export interface TopProductRow {
  rank: number;
  productId: number;
  sku: string;
  name: string;
  category: string;
  quantity: number;
  revenue: number;
}

export interface DashboardSummary {
  revenueThisMonth: number;
  revenueLastMonth: number;
  ordersThisMonth: number;
  openOrders: number;
  overdue: number;
  criticalStockItems: number;
  monthlyRevenue: SalesSummaryRow[];
  topProducts: TopProductRow[];
  revenueByCategory: SalesSummaryRow[];
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
