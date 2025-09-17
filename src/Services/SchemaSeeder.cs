using GraphRagText2Sql.Models;

namespace GraphRagText2Sql.Services
{
    public sealed class SchemaSeeder
    {
        private readonly CosmosGraphService _graph;
        public SchemaSeeder(CosmosGraphService graph) => _graph = graph;

        public async Task SeedAsync()
        {
        // 提供スキーマを手作業でシンプル投入（POC用）
        // テーブルノード
            string pk = "ecommerce";
            var tables = new[]
            {
            "customers","addresses","categories","products","product_images","warehouses","inventory","orders","order_items","payments","shipments","reviews","v_sales_daily"
            }.Select(t => new GraphNode($"t:{t}", "table", $"ecommerce.{t}", null, pk));

            // カラムノード（主要なもののみ、必要に応じ拡張）
            var columns = new List<GraphNode>();
            void AddCols(string table, params string[] cols)
            {
                foreach (var c in cols)
                columns.Add(new GraphNode($"c:{table}:{c}", "column", c, $"ecommerce.{table}", pk));
            }
            AddCols("customers", "customer_id","email","full_name","created_at");
            AddCols("addresses", "address_id","customer_id","address_type","prefecture","city");
            AddCols("categories", "category_id","name","parent_id");
            AddCols("products", "product_id","sku","name","category_id","price","status","created_at");
            AddCols("product_images", "image_id","product_id","url");
            AddCols("warehouses", "warehouse_id","name");
            AddCols("inventory", "product_id","warehouse_id","qty_on_hand");
            AddCols("orders", "order_id","order_number","customer_id","status","subtotal","total_amount","placed_at");
            AddCols("order_items", "order_item_id","order_id","product_id","unit_price","quantity","line_total");
            AddCols("payments", "payment_id","order_id","method","amount","status","paid_at");
            AddCols("shipments", "shipment_id","order_id","carrier","tracking_number","status","shipped_at","delivered_at");
            AddCols("reviews", "review_id","product_id","customer_id","rating","created_at");

            await _graph.UpsertNodesAsync(tables);
            await _graph.UpsertNodesAsync(columns);

            // has_column edges
            var edges = new List<GraphEdge>();
            foreach (var col in columns)
            {
                edges.Add(new GraphEdge($"e:hascol:{col.table}:{col.name}", "has_column", from: $"t:{col.table!.Split('.').Last()}", to: col.id, pk: pk));
            }

            // fk edges（主要外部キーのみ）
            void Fk(string fromTable, string fromCol, string toTable, string toCol)
            {
                edges.Add(new GraphEdge($"e:fk:{fromTable}:{fromCol}->{toTable}:{toCol}", "fk",
                from: $"c:{fromTable}:{fromCol}", to: $"c:{toTable}:{toCol}", pk: pk));
            }
            Fk("addresses","customer_id","customers","customer_id");
            Fk("products","category_id","categories","category_id");
            Fk("product_images","product_id","products","product_id");
            Fk("inventory","product_id","products","product_id");
            Fk("inventory","warehouse_id","warehouses","warehouse_id");
            Fk("orders","customer_id","customers","customer_id");
            Fk("order_items","order_id","orders","order_id");
            Fk("order_items","product_id","products","product_id");
            Fk("payments","order_id","orders","order_id");
            Fk("shipments","order_id","orders","order_id");
            Fk("reviews","product_id","products","product_id");
            Fk("reviews","customer_id","customers","customer_id");

            await _graph.UpsertEdgesAsync(edges);
        }
    }
}