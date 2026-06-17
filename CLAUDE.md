# Project Instructions for AI Agents

This file provides instructions and context for AI coding agents working on this project.

## Session Completion

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds

## Build & Test

Unity 2022.3.22f1 の EditMode テストをバッチモードで実行する(Unity がこのプロジェクトを開いていると失敗する。先に閉じること):

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe" -batchmode `
  -projectPath "C:\Users\omelette_ak\github-repos\VRCloth-Fitter" `
  -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\vrcloth-test-results.xml" -logFile "$env:TEMP\vrcloth-test-log.txt"
```

- 終了コード: 0=全件成功 / 2=テスト失敗あり / それ以外=コンパイルエラー等(ログを確認)
- 失敗の詳細は結果 XML の `//test-case[@result='Failed']`、コンパイルエラーはログの `error CS` を見る
- `-testFilter "VRClothDeclipper.Tests.クラス名"` で絞り込み実行できる

実機での目視 E2E テストの手順は [docs/E2E_TEST_GUIDE.md](docs/E2E_TEST_GUIDE.md)(ユーザーが GUI で実施。エージェントは実行できない)。

## Architecture Overview

VRChat アバター衣装の貫通自動修正を行う Unity エディタ拡張。4アセンブリ構成(すべて `Assets/VRCloth-Declipper/` 配下):

- **Core**(`VRClothDeclipper.Core`)— エディタ非依存の幾何計算。カプセル距離・貫通検出・押し出し・Laplacian 平滑化・スキニング数学(`SkinningMath`)
- **Runtime**(`VRClothDeclipper.Runtime`)— シーンに置く `VRClothDeclipper` コンポーネント(設定の入れ物)のみ
- **Editor**(`VRClothDeclipper.Editor`)— パイプライン本体。`VRClothPipeline.Run()` が キャプチャ(`BakeMesh`)→ Humanoid ボーンからカプセル生成 → `PenetrationSolver`(押し出し+平滑化の反復)→ 逆スキニングでメッシュ複製へ書き戻し(`VRClothMeshApplier`)。シーンビュー可視化とインスペクタ GUI もこの層
- **Tests.Editor** — EditMode テスト(Core 直叩き+実 SkinnedMeshRenderer でのラウンドトリップ)

座標規約: キャプチャは `BakeMesh(useScale: false)` + `TRS(position, rotation, scale=1)` でワールド化し、書き戻しはブレンドスキン行列の逆行列で戻す(`SkinnedRoundTripTests` がこの整合を Unity 実スキニングと突き合わせて検証)。

## Conventions & Patterns

- **No Cache 原則** — アバター素体形状を復元しうる中間データを保存・出力しない。`ClothSnapshot` はメモリ内のみ、フィット結果のメッシュ複製もアセット化しない(シーン内完結)
- **公開ドキュメントのトーン** — 公開リポジトリの文書・issue・コミットメッセージでは、競合/先行製品を名指しで批評・内部解析しない。プライバシーや設計上の論点は「変換型ツール一般の構造的性質」として書き、主語を自分(本プロジェクトの設計選択)に置く。X 等で炎上しやすい強い表現(覇権・制覇・寡占・戦場 等)を避け中立語を使う。根拠と規律は [docs/INFORMATION_ARCHITECTURE.md](docs/INFORMATION_ARCHITECTURE.md) §6
- `Assets/` 配下の `.meta` ファイルは必ずコミットに含める
- コミットメッセージは `feat(fitting): 日本語要約` 形式
- タスク管理は ROADMAP.md(フェーズ別の実行計画)。新規タスク・残課題は ROADMAP.md に追記する
