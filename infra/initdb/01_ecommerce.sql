-- =========================================
-- ECサイト用サンプルデータベース (PostgreSQL)
-- =========================================
-- 接続しているデータベース名を表示
SELECT current_database();

SELECT table_name, table_schema
FROM information_schema.tables

-- 1) スキーマ作成
-- PostgreSQL on Azure 用（スキーマ作成）
DROP SCHEMA IF EXISTS ecommerce CASCADE;
CREATE SCHEMA ecommerce;
SET search_path TO ecommerce;

-- 2) テーブル定義
-- 顧客
CREATE TABLE customers (
  customer_id BIGSERIAL PRIMARY KEY,
  email       TEXT NOT NULL,
  full_name   TEXT NOT NULL,
  phone       TEXT,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 大文字小文字を無視した一意制約（機能的一意インデックス）
CREATE UNIQUE INDEX uq_customers_email_ci ON customers (lower(email));


-- 住所（配送/請求）
CREATE TABLE addresses (
  address_id       BIGSERIAL PRIMARY KEY,
  customer_id      BIGINT NOT NULL REFERENCES customers(customer_id) ON DELETE CASCADE,
  address_type     TEXT NOT NULL CHECK (address_type IN ('shipping','billing')),
  postal_code      TEXT NOT NULL,
  prefecture       TEXT NOT NULL,
  city             TEXT NOT NULL,
  line1            TEXT NOT NULL,
  line2            TEXT,
  country          TEXT NOT NULL DEFAULT 'JP',
  is_default       BOOLEAN NOT NULL DEFAULT FALSE,
  created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- カテゴリ（親子）
CREATE TABLE categories (
  category_id      BIGSERIAL PRIMARY KEY,
  name             TEXT NOT NULL,
  parent_id        BIGINT REFERENCES categories(category_id) ON DELETE SET NULL
);

-- 商品
CREATE TABLE products (
  product_id       BIGSERIAL PRIMARY KEY,
  sku              TEXT NOT NULL UNIQUE,
  name             TEXT NOT NULL,
  description      TEXT,
  category_id      BIGINT REFERENCES categories(category_id) ON DELETE SET NULL,
  price            NUMERIC(10,2) NOT NULL CHECK (price >= 0),
  status           TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active','archived','draft')),
  created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 商品画像
CREATE TABLE product_images (
  image_id         BIGSERIAL PRIMARY KEY,
  product_id       BIGINT NOT NULL REFERENCES products(product_id) ON DELETE CASCADE,
  url              TEXT NOT NULL,
  sort_order       INT NOT NULL DEFAULT 1
);

-- 在庫（単一倉庫想定でも将来拡張可）
CREATE TABLE warehouses (
  warehouse_id     BIGSERIAL PRIMARY KEY,
  name             TEXT NOT NULL
);

CREATE TABLE inventory (
  product_id       BIGINT NOT NULL REFERENCES products(product_id) ON DELETE CASCADE,
  warehouse_id     BIGINT NOT NULL REFERENCES warehouses(warehouse_id) ON DELETE CASCADE,
  qty_on_hand      INT NOT NULL DEFAULT 0 CHECK (qty_on_hand >= 0),
  PRIMARY KEY (product_id, warehouse_id)
);

-- 注文
CREATE TABLE orders (
  order_id         BIGSERIAL PRIMARY KEY,
  order_number     TEXT NOT NULL UNIQUE,
  customer_id      BIGINT NOT NULL REFERENCES customers(customer_id) ON DELETE RESTRICT,
  status           TEXT NOT NULL CHECK (status IN ('pending','paid','shipped','delivered','cancelled','refunded')),
  subtotal         NUMERIC(10,2) NOT NULL CHECK (subtotal >= 0),
  tax_amount       NUMERIC(10,2) NOT NULL DEFAULT 0 CHECK (tax_amount >= 0),
  shipping_fee     NUMERIC(10,2) NOT NULL DEFAULT 0 CHECK (shipping_fee >= 0),
  total_amount     NUMERIC(10,2) NOT NULL CHECK (total_amount >= 0),
  currency         TEXT NOT NULL DEFAULT 'JPY',
  shipping_address_id BIGINT REFERENCES addresses(address_id) ON DELETE SET NULL,
  billing_address_id  BIGINT REFERENCES addresses(address_id) ON DELETE SET NULL,
  placed_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 注文明細
CREATE TABLE order_items (
  order_item_id    BIGSERIAL PRIMARY KEY,
  order_id         BIGINT NOT NULL REFERENCES orders(order_id) ON DELETE CASCADE,
  product_id       BIGINT NOT NULL REFERENCES products(product_id) ON DELETE RESTRICT,
  sku              TEXT NOT NULL,
  product_name     TEXT NOT NULL,
  unit_price       NUMERIC(10,2) NOT NULL CHECK (unit_price >= 0),
  quantity         INT NOT NULL CHECK (quantity > 0),
  line_total       NUMERIC(10,2) GENERATED ALWAYS AS (unit_price * quantity) STORED
);

-- 決済
CREATE TABLE payments (
  payment_id       BIGSERIAL PRIMARY KEY,
  order_id         BIGINT NOT NULL REFERENCES orders(order_id) ON DELETE CASCADE,
  method           TEXT NOT NULL CHECK (method IN ('credit_card','bank_transfer','cod','digital_wallet')),
  amount           NUMERIC(10,2) NOT NULL CHECK (amount >= 0),
  status           TEXT NOT NULL CHECK (status IN ('authorized','captured','failed','refunded')),
  transaction_ref  TEXT,
  paid_at          TIMESTAMPTZ
);

-- 出荷
CREATE TABLE shipments (
  shipment_id      BIGSERIAL PRIMARY KEY,
  order_id         BIGINT NOT NULL REFERENCES orders(order_id) ON DELETE CASCADE,
  carrier          TEXT,
  tracking_number  TEXT,
  status           TEXT NOT NULL CHECK (status IN ('ready','shipped','delivered','returned')),
  shipped_at       TIMESTAMPTZ,
  delivered_at     TIMESTAMPTZ
);

-- レビュー
CREATE TABLE reviews (
  review_id        BIGSERIAL PRIMARY KEY,
  product_id       BIGINT NOT NULL REFERENCES products(product_id) ON DELETE CASCADE,
  customer_id      BIGINT NOT NULL REFERENCES customers(customer_id) ON DELETE CASCADE,
  rating           INT NOT NULL CHECK (rating BETWEEN 1 AND 5),
  title            TEXT,
  body             TEXT,
  created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (product_id, customer_id) -- 1人1商品1レビュー
);

-- 3) 便利インデックス
CREATE INDEX idx_customers_created_at   ON customers(created_at);
CREATE INDEX idx_products_category      ON products(category_id);
CREATE INDEX idx_products_status        ON products(status);
CREATE INDEX idx_orders_customer        ON orders(customer_id);
CREATE INDEX idx_orders_placed_at       ON orders(placed_at);
CREATE INDEX idx_order_items_order      ON order_items(order_id);
CREATE INDEX idx_reviews_product        ON reviews(product_id);

-- 4) サンプルデータ投入
-- 倉庫
INSERT INTO warehouses (name) VALUES
('Tokyo Main'), ('Osaka Sub');

-- 顧客
INSERT INTO customers (email, full_name, phone) VALUES
('taro@example.com','山田 太郎','080-1111-2222'),
('hanako@example.com','佐藤 花子','080-3333-4444'),
('ken@example.com','鈴木 健','080-5555-6666');

-- 住所
INSERT INTO addresses (customer_id, address_type, postal_code, prefecture, city, line1, line2, country, is_default)
VALUES
(1,'shipping','100-0001','東京都','千代田区','千代田1-1',NULL,'JP',TRUE),
(1,'billing','100-0001','東京都','千代田区','千代田1-1',NULL,'JP',TRUE),
(2,'shipping','150-0001','東京都','渋谷区','神宮前1-1-1','APT 301','JP',TRUE),
(2,'billing','150-0001','東京都','渋谷区','神宮前1-1-1','APT 301','JP',TRUE),
(3,'shipping','530-0001','大阪府','大阪市北区','梅田1-1-1',NULL,'JP',TRUE);

-- カテゴリ
INSERT INTO categories (name, parent_id) VALUES
('ファッション', NULL),
('家電', NULL),
('食品', NULL),
('メンズ', 1),
('レディース', 1);

-- 商品
INSERT INTO products (sku, name, description, category_id, price, status) VALUES
('APP-TS-0001','無地Tシャツ（白）','コットン100%',4,990,'active'),
('APP-TS-0002','無地Tシャツ（黒）','コットン100%',4,990,'active'),
('APP-PK-0003','パーカー','裏起毛',4,3990,'active'),
('ELC-EAR-1001','ワイヤレスイヤホン','ノイズキャンセリング',2,8990,'active'),
('ELC-KB-1002','メカニカルキーボード','青軸',2,12990,'active'),
('FD-SN-2001','ポテトチップス（うすしお）','国産じゃがいも',3,150,'active'),
('FD-CF-2002','ドリップコーヒー（10袋）','中深煎り',3,780,'active'),
('APP-DR-0004','ワンピース','春夏向け',5,6990,'active');

-- 画像
INSERT INTO product_images (product_id, url, sort_order) VALUES
(1,'https://example.com/img/ts_white_1.jpg',1),
(2,'https://example.com/img/ts_black_1.jpg',1),
(4,'https://example.com/img/earbuds_1.jpg',1);

-- 在庫
INSERT INTO inventory (product_id, warehouse_id, qty_on_hand) VALUES
(1,1,120),(1,2,40),
(2,1,100),(2,2,30),
(3,1,60),
(4,1,80),
(5,1,50),
(6,1,300),
(7,1,200),
(8,1,25);

-- 注文（例：山田太郎が2件）
INSERT INTO orders (
  order_number, customer_id, status, subtotal, tax_amount, shipping_fee, total_amount,
  currency, shipping_address_id, billing_address_id, placed_at
) VALUES
('ORD-20250901-0001', 1, 'paid',      9980,  998, 500, 11478, 'JPY', 1, 2, NOW() - INTERVAL '8 days'),
('ORD-20250905-0002', 1, 'shipped',  13980, 1398, 0, 15378,  'JPY', 1, 2, NOW() - INTERVAL '4 days'),
('ORD-20250906-0003', 2, 'pending',    930,   93, 500, 1523,  'JPY', 3, 4, NOW() - INTERVAL '3 days');

-- 注文明細
INSERT INTO order_items (order_id, product_id, sku, product_name, unit_price, quantity)
VALUES
-- ORD-0001: Tシャツ白x2
(1, 1, 'APP-TS-0001', '無地Tシャツ（白）', 990, 2),
-- ORD-0002: イヤホンx1 + キーボードx1
(2, 4, 'ELC-EAR-1001', 'ワイヤレスイヤホン', 8990, 1),
(2, 5, 'ELC-KB-1002',  'メカニカルキーボード', 12990, 1),
-- ORD-0003: ポテチx2 + コーヒーx1
(3, 6, 'FD-SN-2001', 'ポテトチップス（うすしお）', 150, 2),
(3, 7, 'FD-CF-2002', 'ドリップコーヒー（10袋）', 780, 1);

-- 決済
INSERT INTO payments (order_id, method, amount, status, transaction_ref, paid_at) VALUES
(1,'credit_card',11478,'captured','TX-111111', NOW() - INTERVAL '8 days'),
(2,'credit_card',15378,'captured','TX-222222', NOW() - INTERVAL '4 days');

-- 出荷
INSERT INTO shipments (order_id, carrier, tracking_number, status, shipped_at, delivered_at) VALUES
(2,'Yamato','YT123456789JP','shipped', NOW() - INTERVAL '3 days', NULL);

-- レビュー
INSERT INTO reviews (product_id, customer_id, rating, title, body) VALUES
(4,1,5,'音質良し','ノイキャンも効いて満足'),
(1,2,4,'着心地が良い','夏にちょうど良い厚み');

-- 5) よく使うビュー例（売上ダッシュボード用）
CREATE OR REPLACE VIEW v_sales_daily AS
SELECT
  date_trunc('day', placed_at)::date AS order_date,
  COUNT(*) AS orders,
  SUM(total_amount) AS gross_sales
FROM orders
WHERE status IN ('paid','shipped','delivered','refunded')
GROUP BY 1
ORDER BY 1;

-- 6) 参照例クエリ（コメントとして）
-- 売上日次
-- SELECT * FROM v_sales_daily;
-- 注文一覧（明細付き）
-- SELECT o.order_number, o.status, i.sku, i.product_name, i.quantity, i.unit_price, i.line_total
-- FROM orders o JOIN order_items i ON i.order_id = o.order_id
-- ORDER BY o.placed_at DESC;

-- commit;