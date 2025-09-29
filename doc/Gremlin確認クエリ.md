
# Gremlin クエリにおける `g.` の意味と実務での使い分け

*前提: Azure Cosmos DB (Gremlin API)*

---

## 1. `g` とは？

* **GraphTraversalSource（探索の起点）** を表す特別な変数。
* Gremlin クエリは「トラバーサル（探索手続き）」を **チェーン形式** で記述し、その起点が `g`。
* Cosmos DB では `g` が既定で定義されており、`g.` から書き始めるだけでクエリを実行できる。

---

## 2. 基本エントリーポイント

* `g.V()` → 頂点（Vertex）取得
* `g.E()` → 辺（Edge）取得

例：

```groovy
g.V().hasLabel('table')
```

👉 `label = 'table'` の頂点を返す。

---

## 3. 実務指針（3行ルール）

1. 実体（テーブル・ユーザーなど）に注目 → **`g.V()`**
2. 関係（外部キー・フォローなど）を調べたい → **`g.E()`**
3. 実体から関係へ／関係から実体へ → **起点に近い側から開始**

---

## 4. `g.V()` を起点にするケース

* **特定ラベルや ID で絞り込み**

```groovy
g.V().has('table','id','t:orders').valueMap(true)
```

* **実体から関係をたどる**

```groovy
g.V().has('table','id','t:orders').out('has_column')
```

* **一覧化 → 集計**

```groovy
g.V().hasLabel('table').as('t')
  .out('has_column').out('fk')
  .select('t').groupCount()
```

👉 実体が明確で、ID/ラベルで効率的にフィルタ可能な場合に有効。

---

## 5. `g.E()` を起点にするケース

* **関係そのものを調べる**

```groovy
g.E().hasLabel('fk').count()
```

* **エッジ属性で検索**

```groovy
g.E().hasLabel('fk').has('on_delete','cascade')
```

* **両端の頂点を取得**

```groovy
g.E().hasLabel('fk').as('e')
  .outV().values('id').as('src')
  .inV().values('id').as('dst')
  .select('src','dst')
```

👉 FK の全体スキャンや、関係の品質確認に有効。

---

## 6. 頂点⇔辺の変換

* 頂点から関係へ：`g.V().out('…') / in('…')`
* 関係から頂点へ：`g.E().outV() / inV()`

例：エッジから両端テーブルを取得

```groovy
g.E().hasLabel('fk').as('e')
  .outV().in('has_column').values('id').as('from_table')
  .select('e').inV().in('has_column').values('id').as('to_table')
  .select('from_table','to_table')
```

---

## 7. Cosmos DB 特有の注意点

* **エッジ方向ミス注意**：`has_column` は `table → column`
* **`project()` 後のキーで `order().by()` 不可**
* **`asc/desc` は不要**：昇順が既定
* **`dedup()` はラベル指定推奨**：`dedup('t','c')`

---

## 8. クイックマップ（まとめ）

* **ID/ラベル検索・一覧** → `g.V()`
* **関係の属性/件数** → `g.E()`
* **たどり探索** → 起点に近い側から
* **全体俯瞰** → `g.E()` + `.outV() / inV()`

---

## ✅ 確認用クエリ一式（Cosmos DB対応・説明付き）

### 1) 頂点確認

#### 1-1. 全テーブル頂点を確認

```groovy
g.V().hasLabel('table').values('id','name')
```

👉 テーブル一覧を表示。

#### 1-2. 全カラム頂点を確認

```groovy
g.V().hasLabel('column').values('id','name','table')
```

👉 カラム一覧を表示。

#### 1-3. 特定テーブル（例: customers）の頂点を確認

```groovy
g.V().has('table','id','t:customers').valueMap(true)
```

👉 `t:customers` の詳細を取得。

---

### 2) エッジ確認

#### 2-1. has_column エッジの確認

```groovy
g.E().hasLabel('has_column').limit(10).valueMap(true)
```

#### 2-2. fk エッジの確認

```groovy
g.E().hasLabel('fk').limit(10).valueMap(true)
```

---

### 3) リレーション確認

#### 3-1. テーブル → カラム（has_column）

```groovy
g.V().has('table','id','t:orders').out('has_column').valueMap(true)
```

#### 3-2. 外部キー（例: orders → customers）

```groovy
g.V().has('column','id','c:orders:customer_id').out('fk').valueMap(true)
```

#### 3-3. カラムから親テーブルを逆引き

```groovy
g.V().has('column','id','c:orders:order_id').in('has_column').valueMap(true)
```

---

### 4) サマリ

#### 4-1. 頂点の件数

```groovy
g.V().count()
```

#### 4-2. 辺の件数

```groovy
g.E().count()
```

---

### 5) 外部キー関係を洗い出す

#### 5-A. 参照している外部キー一覧（例: orders）

```groovy
g.V().has('table','id','t:orders').as('srcTable')
  .out('has_column').as('srcCol')
  .out('fk').as('dstCol')
  .in('has_column').as('dstTable')
  .order().
    by(select('srcTable').values('id')).
    by(select('srcCol').values('id'))
  .dedup('srcTable','srcCol','dstCol','dstTable')
  .project('from_table','from_column','to_table','to_column')
    .by(select('srcTable').values('id'))
    .by(select('srcCol').values('id'))
    .by(select('dstTable').values('id'))
    .by(select('dstCol').values('id'))
```

#### 5-B. 参照されている外部キー一覧（例: customers）

```groovy
g.V().has('table','id','t:customers').as('dstTable')
  .out('has_column').as('dstCol')
  .in('fk').as('srcCol')
  .in('has_column').as('srcTable')
  .order().
    by(select('srcTable').values('id')).
    by(select('srcCol').values('id'))
  .dedup('srcTable','srcCol','dstCol','dstTable')
  .project('from_table','from_column','to_table','to_column')
    .by(select('srcTable').values('id'))
    .by(select('srcCol').values('id'))
    .by(select('dstTable').values('id'))
    .by(select('dstCol').values('id'))
```

#### 5-C. 両方向一覧（参照 / 被参照）

* outgoing（参照している）

```groovy
g.V().has('table','id','t:orders').as('t')
  .out('has_column').as('c')
  .out('fk').as('rc')
  .in('has_column').as('rt')
  .order().
    by(select('t').values('id')).
    by(select('c').values('id'))
  .dedup('t','c','rc','rt')
  .project('direction','from_table','from_column','to_table','to_column')
    .by(constant('out'))
    .by(select('t').values('id'))
    .by(select('c').values('id'))
    .by(select('rt').values('id'))
    .by(select('rc').values('id'))
```

* incoming（参照されている）

```groovy
g.V().has('table','id','t:orders').as('t')
  .in('has_column').as('rc')
  .in('fk').as('c')
  .in('has_column').as('st')
  .order().
    by(select('st').values('id')).
    by(select('c').values('id'))
  .dedup('t','c','rc','st')
  .project('direction','from_table','from_column','to_table','to_column')
    .by(constant('in'))
    .by(select('st').values('id'))
    .by(select('c').values('id'))
    .by(select('t').values('id'))
    .by(select('rc').values('id'))
```

#### 5-D. 全テーブル横断の外部キー一覧

```groovy
g.V().hasLabel('table').as('srcTable')
  .out('has_column').as('srcCol')
  .out('fk').as('dstCol')
  .in('has_column').as('dstTable')
  .order().
    by(select('srcTable').values('id')).
    by(select('srcCol').values('id'))
  .dedup('srcTable','srcCol','dstCol','dstTable')
  .project('from_table','from_column','to_table','to_column')
    .by(select('srcTable').values('id'))
    .by(select('srcCol').values('id'))
    .by(select('dstTable').values('id'))
    .by(select('dstCol').values('id'))
```

#### 5-E. 外部キー件数サマリ

* 参照している件数（outgoing）

```groovy
g.V().hasLabel('table').as('t')
  .out('has_column').out('fk')
  .select('t').values('id')
  .groupCount()
```

* 参照されている件数（incoming）

```groovy
g.V().hasLabel('table').as('t')
  .out('has_column')
  .in('fk')
  .select('t').values('id')
  .groupCount()
```

---

## ✅ まとめ

* `g` = GraphTraversalSource（クエリの入口）
* **実体を見るなら `g.V()`、関係を見るなら `g.E()`**
* Cosmos DB では `g.` が必須
* 確認用クエリを活用することで、スキーマや FK 関係を効率的に検証可能

---

これで「全部入り版（本文＋確認用クエリ一式）」が完成しました。
ご希望に合わせて、このまま **PDF化** や **スライド化** もできますが、どちらをご希望ですか？
