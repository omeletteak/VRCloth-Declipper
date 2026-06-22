# TODO — 実機 E2E(Mini Stack × SELESTIA / 「無料で着せられる」マップ)

**軸**: 無料の Mini Stack(SEI10, booth.pm/ja/items/8414572)を、**mini stack 非対応 ∩ もちふぃった〜が有料プロファイルのみ**のアバターに着せ、貫通修正で「**有料プロファイル不要・無料で着せられる**」を実証する。
**第一点**: **SELESTIA**(もちふぃった〜は有料のみ ¥500〜600 / mini stack 非対応 / 人気)。
**正直な境界**: 体型差が深い(赤)はリターゲ=もちふぃった〜の領域。vrcloth は貫通修正であってフィットではない。
詳細手順: docs/E2E_TEST_GUIDE.md

**ワークフロー(既存決定: 手動 prep + ヘッドレス sweep)**
- A 手動 prep(自分/Unity): 着せ→位置/回転/スケール合わせ→Manual Bake→VRClothDeclipper 付け直し+Mesh SDF ON→保存 → `Assets/TEST_vrcloth_declipper/`
- B ヘッドレス数値(エージェント): `-vrclothSceneDir -vrclothApply` で緑/黄/赤+残留マップ
- C 目視確定(自分/Unity): 過膨張・偽RED・肌見せ境界×関節+スクショ

## 準備
- [ ] Mini Stack 入手(無料)。スクショ公開するなら VN3 規約を確認
- [ ] SELESTIA を手持ちから用意
- [ ] ヌルテスト用に mini stack 対応アバター1体(Shianao/Airi/Manuka/Chocolat/Chiffon/Plum/Milfy/Eku/Sio/Rurune/Mayo/Kumaly から)

## A 手動 prep(Unity)
- [ ] ヌルテスト用: mini stack を対応アバターに着せ→Bake→VRClothDeclipper+Mesh SDF ON→保存
- [ ] SELESTIA: mini stack を着せ→位置/回転/スケール合わせ→Manual Bake→VRClothDeclipper 付け直し+Mesh SDF ON→保存
- [ ] 両シーンを `Assets/TEST_vrcloth_declipper/` に置いたらエージェントに連絡

## B ヘッドレス数値(エージェントが回す)
- [ ] `-vrclothSceneDir -vrclothApply` で一括 before→after。GREEN/YELLOW/RED + 残留を JSON で取得

## C 目視確定(Unity)
- [ ] ヌルテスト: mesh-SDF で GREEN
- [ ] SELESTIA: `M=0` / 肌見せ境界×関節(襟元・胸元・スカート裾⇔太もも)に素体が出てないか / 胸尻(Sync 不発)が衣装に収まるか / Before-After スクショ
- [ ] 緑/黄/赤を記録(=マップの第一点)

## マップ拡張(第一点が取れたら)
- [ ] 緑〜黄の主役を追加: 有料プロファイルのみ ∩ 体型が mini stack ファミリーに近い体を2〜3
- [ ] 赤の境界実例を1体: 体型が遠い体(例: Wolferia=無料プロファイルありだが「ここからリターゲ」の正直な端として)

## 記録 = 「無料で着せられる」マップ
- [ ] 表: アバター / 体型タグ / 判定 / 主要数値 / 回避できた有料プロファイル額
- [ ] スカートは静止で評価(PhysBone の動的クリッピングはツール対象外)
- [ ] 「直せてほしいのに赤 / 無理筋なのに緑」は数値を残す(§9 較正)
