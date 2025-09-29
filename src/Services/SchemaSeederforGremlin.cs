using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;

namespace GraphRagText2Sql.Services
{
    /// <summary>
    /// Cosmos DB for Gremlin 用スキーマシーダー
    /// </summary>
    public sealed class SchemaSeederforGremlin
    {
        private readonly GremlinClient _g;

        // GremlinClient を DI で受け取る想定（Program.cs などで登録）
        public SchemaSeederforGremlin(GremlinClient gremlinClient) => _g = gremlinClient;

        public async Task SeedAsync()
        {
            // ▼ パーティションキー（/pk）として使う値
            string pk = "ecommerce";

            // ▼ テーブル頂点（label=table）
            var tables = new[]
            {
                "customers","addresses","categories","products","product_images","warehouses",
                "inventory","orders","order_items","payments","shipments","reviews","v_sales_daily"
            }.Select(t => new
            {
                id = $"t:{t}",
                label = "table",
                name = $"ecommerce.{t}",
            });

            // 頂点 upsert
            foreach (var t in tables)
            {
                await UpsertVertexAsync(
                    id: t.id,
                    label: t.label,
                    pk: pk,
                    props: new Dictionary<string, object?>
                    {
                        ["name"] = t.name
                        // "table" は table 頂点には不要なので付与しない
                    });
            }

            // ▼ カラム頂点（label=column）
            var columns = new List<(string id, string table, string name)>();
            void AddCols(string table, params string[] cols)
            {
                foreach (var c in cols)
                {
                    columns.Add((
                        id: $"c:{table}:{c}",
                        table: $"ecommerce.{table}",
                        name: c
                    ));
                }
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
            AddCols("v_sales_daily", "order_date", "orders", "gross_sales");

            foreach (var c in columns)
            {
                await UpsertVertexAsync(
                    id: c.id,
                    label: "column",
                    pk: pk,
                    props: new Dictionary<string, object?>
                    {
                        ["name"] = c.name,
                        ["table"] = c.table
                    });
            }

            // ▼ has_column エッジ（table -> column）
            foreach (var c in columns)
            {
                string tableSimple = c.table.Split('.').Last();
                string fromId = $"t:{tableSimple}";
                string toId   = c.id;
                string edgeId = $"e:hascol:{c.table}:{c.name}";

                await UpsertEdgeAsync(
                    label: "has_column",
                    edgeId: edgeId,
                    fromVertexId: fromId,
                    toVertexId: toId
                );
            }

            // ▼ 外部キーエッジ（column -> column）
            void Fk(string fromTable, string fromCol, string toTable, string toCol)
            {
                string fromId = $"c:{fromTable}:{fromCol}";
                string toId   = $"c:{toTable}:{toCol}";
                string edgeId = $"e:fk:{fromTable}:{fromCol}->{toTable}:{toCol}";

                _ = UpsertEdgeAsync(
                    label: "fk",
                    edgeId: edgeId,
                    fromVertexId: fromId,
                    toVertexId: toId
                );
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

            // 直列化を避けたい場合は上の Fk 呼び出しを Task.WhenAll で束ねる実装に変更可
        }

        /// <summary>
        /// 頂点の疑似 upsert（id+pk 固定）
        /// </summary>
        private async Task UpsertVertexAsync(string id, string label, string pk, IDictionary<string, object?> props)
        {
            // g.V().has('id', id).fold().coalesce(unfold(), addV(label).property('id', id).property('pk', pk)....)
            var gremlin =
                // "g.V().has('id', id)" +
                "g.V().hasId(id)" +
                ".fold()" +
                ".coalesce(" +
                "  unfold()," +
                "  addV(label).property('id', id).property('pk', pk)" +
                ")";

            var bindings = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["label"] = label,
                ["pk"] = pk
            };

            // まず upsert 骨格を実行
            await _g.SubmitAsync<dynamic>(gremlin, bindings);

            // 既存/新規問わずプロパティを upsert
            // property(cardinality, key, value) は Cosmos では single 相当になる
            foreach (var kv in props)
            {
                var setProp =
                // "g.V().has('id', id).property(key, val)";
                "g.V().hasId(id).property(key, val)";
                await _g.SubmitAsync<dynamic>(setProp, new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["key"] = kv.Key,
                    ["val"] = kv.Value
                });
            }
        }

        /// <summary>
        /// エッジの疑似 upsert（存在しなければ追加）。edgeId を明示設定。
        /// </summary>
        private async Task UpsertEdgeAsync(string label, string edgeId, string fromVertexId, string toVertexId)
        {
            // from の outE(label) -> inV() が to のものが既にあればそれを使い、無ければ addE
            var gremlin =
                "g.V().has('pk', pk).hasId(fromId)" +
                ".coalesce(" +
                "  outE(label).where(inV().has('pk', pk).hasId(toId))," +
                "  addE(label).to(g.V().has('pk', pk).hasId(toId)).property('eid', edgeId)" +
                ")";

            var bindings = new Dictionary<string, object?>
            {
                ["label"] = label,
                ["edgeId"] = edgeId,
                ["fromId"] = fromVertexId,
                ["toId"] = toVertexId,
                ["pk"] = "ecommerce" // 既定のパーティションキーを明示
            };

            await _g.SubmitAsync<dynamic>(gremlin, bindings);
        }
    }
}
