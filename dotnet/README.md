# Declipper.Core (v2) — 純 .NET コア

v2 再設計([docs/REARCHITECTURE.md](../docs/REARCHITECTURE.md))の柱1「UnityEngine 完全非依存の幾何コア」の実装場所。数学は `System.Numerics.Vector3`、テストは `dotnet test` で秒単位に回る。Unity 側は将来(S2)このソースを共有コンパイルするため、**`netstandard2.1` / C# 9 を超える言語機能・API は使わないこと**(Unity 2022.3 互換の上限)。

## 状態(2026-07-03, S0 スケルトン)

**このスケルトンはまだ一度もビルドされていない**(作成マシンに dotnet SDK が無かった)。着手時の最初の作業は SDK 導入と `dotnet test` の通過確認:

```bash
# SDK が無ければ(root 不要、~/.dotnet へ):
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
export PATH="$HOME/.dotnet:$PATH"

cd dotnet && dotnet test
```

期待: `CapsuleSdfTests` 5件が緑(実装済み部分)、スタブはテスト対象外。コンパイルエラーがあればまず直すこと(スケルトンの契約 doc は正、シグネチャの些細な修正は可)。

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

1. **ゴールデンフィクスチャの用意(最初にやる)** — v1(Unity バッチモード)から代表入力(実衣装メッシュ1つ+カプセル列/素体メッシュ)に対する検出結果・ソルブ後頂点・プリフライト統計を JSON で吐くダンプを書き、`tests/fixtures/` に固定。以後の移植はすべて「v2 が同一出力を返すか」で合否を取る(docs/REARCHITECTURE.md §4 S1)
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
