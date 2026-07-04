# Declipper.Core (v2) — 純 .NET コア

v2 再設計([docs/REARCHITECTURE.md](../docs/REARCHITECTURE.md))の柱1「UnityEngine 完全非依存の幾何コア」の実装場所。数学は `System.Numerics.Vector3`、テストは `dotnet test` で秒単位に回る。Unity 側は将来(S2)このソースを共有コンパイルするため、**`netstandard2.1` / C# 9 を超える言語機能・API は使わないこと**(Unity 2022.3 互換の上限)。

## 状態(2026-07-04, S1 移植＋ゴールデンゲート完了)

スタブは全て実装済み、v1↔v2 ゴールデンゲートも稼働。`dotnet test` は **29件緑・秒未満**(dotnet SDK 9.0 で検証、`src` は `netstandard2.1`/C# 9 を維持):

```bash
cd dotnet && dotnet test
```

移植済み(構成的/解析的ユニットテストで固定 — 純組合せ・ベクトル論理は v1 golden より厳密):

| ピース | 移植元 | テスト観点 |
|---|---|---|
| `Solver/PenetrationDetection` | 同名 v1 | 検出深度・入力順(SDF 単一経路) |
| `Solver/VertexAdjacency` | 同名 v1 | 溶接トポロジ併合・近傍 |
| `Solver/LaplacianSmoothing` | 同名 v1 | リング成長・クローン書戻し |
| `Solver/ProjectedSolver` | `PenetrationSolver.SolveProjected` | 不変条件 finalHitCount==0 |
| `Sdf/MeshSdf` | `MeshSdfCollider` | 加速版 vs ブルートフォース一致・符号・勾配 |
| `Diagnostics/PreflightDiagnostic` | 同名 v1 | Green/Collapsed/Retargeting/InnerWall 分類 |
| `Surface/CapsuleSurface` | 新規(v1 対応物なし) | round-trip 恒等(body＋caps＋球)・margin クランプ |

### ゴールデンフィクスチャ基盤(landed)

v1↔v2 等価ゲートが稼働(§4「ゴールデンテストが通るまで v1 を消さない」= **S2 で v1 を置換する条件**)。

- ダンパー: `Assets/VRCloth-Declipper/Tests/Fixtures/GoldenFixtureDumper.cs`(Editor 専用 asmdef・`UNITY_INCLUDE_TESTS` 非依存)。Unity バッチで再生成:
  ```bash
  Unity.exe -batchmode -quit -projectPath <repo> \
    -executeMethod VRClothDeclipper.GoldenFixtures.GoldenFixtureDumper.DumpAll
  ```
- fixtures(`dotnet/tests/fixtures/*.json`):
  - capsule 系3件(`tube_over_capsule`/`sheet_over_sphere`/`two_spheres`)— 検出+ソルブ+プリフライトを厳密照合
  - `meshsdf_sphere` — 216 プローブで MeshSdf の符号付き距離(厳密)＋勾配(統計的、下記)
  - `meshsolve_shell_in_sphere` — mesh SDF ボディでの full-solve(同心シェル cloth・315頂点)
  - 照合は `GoldenTests.cs`
- **入力の権利制約**: 入力は 100% 手続き生成(購入・アバター由来ゼロ)なので入出力とも public repo に commit 可 — No Cache・再配布禁止の両方を満たす
- **mesh SDF の条件付けに関する知見**: 低ポリ mesh SDF の**スカラー距離場は v1↔v2 で厳密再現**(1e-4)だが、**勾配はファセット境界で不連続**で、v1(Mono)/v2(.NET) が最近接ファセットのタイブレークを float 精度で別々に解決するため境界近傍で最大 面角度ぶんずれる。ゆえに勾配は統計的に検証(遠方プローブの大多数が厳密一致・符号反転ゼロ)、full-solve は良条件な cloth(面追従シェル・各頂点が1ファセットに明確最近接)を使い 315頂点/8反復で 1e-7 のほぼビット一致を確認。中心付近に置いた cloth は全ファセット等距離で縮退する(移植バグでなく ill-conditioned 入力)
- **残(小)**: 基準マネキン(ROADMAP フェーズ5)を使った E2E 近似 fixture は将来足せる

### 移植時の設計判断・妥協点(記録)

- **nullable-as-error**: `src` は `TreatWarningsAsErrors`+`Nullable enable`。v1 の防御的 null ガードは CS8602 で落ちるため、`?` アノテーション＋早期 return へ整形した(挙動は不変)。
- **`RedCause.None` 復活**: v2 スタブ enum は None を落としていたが、`PreflightReport` は常に `RedCause` フィールドを持つため非Red時のセンチネルが必要。v1 準拠で None をゼロ値に戻した。
- **`MeshSdf` の一件メモ削除**: v1 は距離/勾配を別クエリ＋一件メモで返したが、v2 契約は距離+勾配一括の `Sample`。メモはステートフルで並列化(§2 柱1)を阻むため削除しステートレス化。Scan→PushOut 間の再計算最適化が要れば別途スレッドセーフな形で。
- **`CapsuleSurface.Coordinates=(t,na,θ)`**: スタブは "(t, azimuth, unused)" と例示したが、(t,azimuth) の2自由度では半球キャップを表現できず round-trip がキャップで破綻する。na(法線の軸成分)を "unused" 枠に充ててキャップ含め厳密恒等化した(妥協でなく厳密化)。

## レイアウト

```
dotnet/
  Declipper.sln
  src/Declipper.Core/
    Sdf/
      ISignedDistanceField.cs   実装済  v2 の唯一の体表現契約(距離+勾配の一括 Sample)
      Capsule.cs                実装済  カプセル解析解(v1 BodyCapsule の移植)
      CapsuleSetSdf.cs          実装済  カプセル合成(min-union)
      MeshSdf.cs                スタブ  BVH 最近接 + Barnes–Hut 巻き数(S1 の最重量級)
    Surface/
      SurfaceBinding.cs         契約のみ 体表面対応(binding)— v2 の中核プリミティブ
    Solver/
      PenetrationDetection.cs   スタブ  検出(単一パス、カプセル専用経路は作らない)
      VertexAdjacency.cs        スタブ  隣接(シーム溶接の意味論を v1 から保存)
      LaplacianSmoothing.cs     スタブ  変位場の平滑化(位置は触らない)
      ProjectedSolver.cs        スタブ  唯一のソルバ(制約付き最適化、coarse は移植しない)
    Diagnostics/
      Preflight.cs              スタブ  緑/黄/赤判定 + RedCause 名指し
  tests/Declipper.Core.Tests/   NUnit(net8.0)
```

## S1 の実装順(推奨)

各スタブの XML doc が契約と移植元ポインタを持つ。移植は「v1 の数学をそのまま、型だけ差し替え」が原則 — アルゴリズム改良は移植と混ぜない。

1. **ゴールデンフィクスチャの用意(最初にやる)** — v1(Unity バッチモード)から代表入力に対する検出結果・ソルブ後頂点・プリフライト統計を JSON で吐くダンプを書き、`tests/fixtures/` に固定。以後の移植はすべて「v2 が同一出力を返すか」で合否を取る(docs/REARCHITECTURE.md §4 S1)。
   **入力の権利制約(必須)**: フィクスチャは public リポジトリにコミットされる。入力メッシュ(衣装・素体とも)は**合成メッシュ(プロシージャル生成)または再配布自由な基準マネキン(ROADMAP フェーズ5)に限る**。購入アセット由来の頂点データは、ソルブ後頂点(=変形した衣装形状)を含め一切コミットしない — No Cache 原則と再配布禁止の両方に抵触する
2. `PenetrationDetection` ← `Assets/VRCloth-Declipper/Core/PenetrationDetection.cs`(自明)
3. `VertexAdjacency` / `LaplacianSmoothing` ← 同名 v1 ファイル(シーム溶接の意味論に注意)
4. `ProjectedSolver` ← `PenetrationSolver.SolveProjected`(coarse `Solve` は**移植しない**)
5. `MeshSdf` ← `MeshSdfCollider.cs`(BVH + Barnes–Hut。最重量級、性能もフィクスチャで v1 比計測)
6. `PreflightDiagnostic` ← `PreflightDiagnostic.cs`(しきい値は名前付き定数で一箇所に)
7. `IBodySurface` のカプセル実装(新規 — v1 に対応物なし。round-trip 恒等 `Evaluate(Bind(p)) == p` をテストの起点に)

## やらないこと

- 永続化 API(No Cache — コアはメモリ内で完結する)
- UnityEngine への参照(それは S2 のアダプタ層の仕事)
- v1 コードの削除(ゴールデンテストが通るまで v1 は正として残す)
