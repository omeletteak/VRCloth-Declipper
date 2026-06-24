# 採寸仕様 (MEASUREMENT_SPEC)

このドキュメントは**構想資料**です。[ECOSYSTEM_VISION.md](ECOSYSTEM_VISION.md) §5(採寸表)・§6(Fit Report)で「分離予定」とした採寸層を、実装に近づいた分だけ具体化します。実装を拘束するのは [DESIGN.md](DESIGN.md) と [ROADMAP.md](../ROADMAP.md) のみ。関連: [FAMILY_MODEL.md](FAMILY_MODEL.md)(採寸空間の幾何・§7 最近傍・§8 差分診断)、[DIAGNOSTIC_HONESTY.md](DIAGNOSTIC_HONESTY.md)(計測の誤差と正直さ)、[INFORMATION_ARCHITECTURE.md](INFORMATION_ARCHITECTURE.md)(何を運ぶか)。最終更新: 2026-06-24。

## 0. 一行で

**素体・衣装を「同じ物差し(カプセル軸ごとの周径スカラー)」で測り、生は jsonl(No Cache)、束ねは派生 SQLite、固定はメッシュハッシュ＋計測条件のスナップショット。** 形状そのものはどこにも動かさない。

## 1. 位置づけ

ECOSYSTEM_VISION の三層「予測・保証・吸収」のうち**予測層**を具体化したもの。採寸表が「どのサイズ/素体が合うか」を購入前・着用前に予測し、診断(プリフライト)が保証し、修正(貫通ソルバ)が残差を吸収する。採寸は [FAMILY_MODEL.md](FAMILY_MODEL.md) §7「最も近い代表アバターはどれか」を機械的に答える材料でもある。

権利・プライバシー安全性: 採寸が出すのは**周径スカラー十数個＋一方向ハッシュ**だけで、そこから素体形状は復元できない(現実のアバター商品ページが身長を載せるのと同列の粒度、[INFORMATION_ARCHITECTURE.md](INFORMATION_ARCHITECTURE.md))。No Cache 原則と両立する。

## 2. 標準計測点

計測点は**プロキシカプセル**(`VRClothProxyGenerator` が Humanoid ボーンから生成する骨格セグメント列: Hips→Spine, Spine→Chest, …, UpperLeg→LowerLeg, … 最大19・実体は素体のボーン構成で15前後)で定義する。各計測点の値は:

- **半径 `radius_m`** — そのセグメント軸の周りの素体表面の代表距離(`VRClothBodyRadiusEstimator` が最近接軸割り当て＋パーセンタイルで推定)。**周径 ≈ 2π·radius**(円断面近似)。
- 補助: セグメント長 `length_m`、寄与頂点数 `sampleCount`、推定できたか `estimated`(できなければフォールバック半径)。

**[留保]** 現在の値は円断面近似の半径。真の周径(軸に垂直なスライスの凸包周長、ECOSYSTEM_VISION §5)は楕円断面の胴などで近似誤差を持つ。スライス周長への置き換えは精度向上の余地(本書 §8)。

## 3. アバター採寸(body) [一部 landed]

**手法**: Humanoid からカプセル生成 → **全ボディパーツを合算**して半径推定(分割素体=YM Body 等で体が複数メッシュに割れていても、髪だけを掴む偽 GREEN を避ける。[DIAGNOSTIC_HONESTY.md](DIAGNOSTIC_HONESTY.md) §1、`BodyModelConfidence`)。頭身(scale 不変の体型ファミリー記述子、FAMILY_MODEL §2)も併記。読むのはメモリ内の頂点、出すのはスカラーのみ。

**ツール(landed)**:
- インスペクタ **Dump Body Measurement (採寸)** ボタン(`VRClothMeasurementDump`) — 対象アバター1行を `vrcloth-body-measurements.jsonl` に追記。
- ヘッドレス **`VRClothMeasureCli`**(`-executeMethod VRClothDeclipper.VRClothMeasureCli.Run`) — (a) `-vrclothScene/-vrclothSceneDir` でシーン内の全 fitter、(b) `-vrclothAvatar`(カンマ可)/`-vrclothAvatarDir` で prefab を一時シーンに instantiate して採寸→破棄。GUI 不要。`-vrclothOut`(既定上書き/`-vrclothAppend` 追記)。

**JSONL 1行スキーマ(schema `vrcloth-body-measurement/1`)**:
```
{ schema, timestamp, avatar, headCount_neckRef, headCount_headRef, height_m,
  bodyCoverage, capsuleCount,
  capsules:[ { label, radius_m, length_m, sampleCount, estimated }, … ] }
```

**計測条件の固定**(再現性のため、ECOSYSTEM_VISION §6/§10): margin 5mm・**デフォルトポーズ**・**シェイプキー 0**。条件を変えて測れば別スナップショット(本書 §6)。

## 4. 衣装採寸(仕上がり寸法) [構想]

衣装には骨格=計測の基準が無い…が、**衣装はアバター骨にスキンされたメッシュ**である。ゆえに:

> **同じカプセルプロキシを生成し、半径推定を「素体メッシュ」でなく「衣装メッシュ」に向ければ、衣装の内周(仕上がり寸法)が body と同一の物差しで取れる。**

`VRClothBodyRadiusEstimator` の双子(対象レンダラを差し替えるだけ)。これが ECOSYSTEM_VISION §5「衣装側=仕上がり寸法(衣装内周)」の実体。**要件**: 衣装が骨格付き(着用状態、または骨を持つ skinned garment)であること。**[留保]** 着衣プレファブ(素体+衣装が一体)を素体として測ると衣装が body に混入する(逆も然り)ので、measure 対象レンダラの選別が要る。

## 5. 採寸コーパスと SQLite 分析層

OMs 等の本番プロジェクトの全アバター・全衣装を測れば、ECOSYSTEM_VISION の**採寸コーパス**になり、[FAMILY_MODEL.md](FAMILY_MODEL.md) §7 のファミリークラスタリングと §9 の体型同質性仮説を実データで検証できる。

**パイプライン(確定方針)**:
```
Unity CLI ──→ jsonl(生・No Cache・依存ゼロ・人が読める)
                 │  python sqlite3 (stdlib) で取り込み
                 ▼
              SQLite(派生・再生成可・gitignore・ローカルの分析 DB)
```
- **SQLite 書き込みを Unity ツールに入れない**(`Mono.Data.Sqlite` 依存を抱えない)。生 jsonl が真実、DB はいつでも作り直せる分析キャッシュ。
- SQLite を選ぶ理由は**データ形状**: 採寸結果は構造化・多数行・成長・関係的で、最近傍/クラスタ/(avatar×garment)行列を **SQL の宣言的クエリ・JOIN** で引ける。散文(メモリ)はファイル+git、表形式で問い合わせるものは SQLite ― 道具をデータ形状に合わせる。

**スキーマ素描**:
```
avatars(id, name, head_count, height_m, body_coverage, mesh_hash, conditions_json, measured_at)
capsule_measurements(avatar_id, label, radius_m, length_m, sample_count, estimated)
garments(id, name, mesh_hash, conditions_json, measured_at)         -- §4 実装後
garment_measurements(garment_id, label, radius_m, …)                 -- §4 実装後
fits(avatar_id, garment_id, verdict, penetrating, clearance_p95, …)  -- §7 マッチング行列
```

## 6. 版管理: メッシュハッシュ＋条件スナップショット

ECOSYSTEM_VISION §6 の identity。各採寸行に**何を測ったか**を固定する。

**ハッシュの役割を分ける(混ぜない)**:
- **(A) 来歴・版固定・陳腐化検知 = 堅い** — 資産が変わればハッシュが変わる→再採寸が要ると分かる。両資産を持つ者は再採寸して数値を検証できる(偽装は自滅)。
- **(B) ユーザー間で「同じアバターだ」をハッシュ一致で判定 = 脆い** — import 設定・MOD・浮動小数差で、同じアバターでもハッシュは一致しない。**ユーザー間の体型比較は採寸値(周径スカラー)でやる**(改変版もバニラに近い数値=同ファミリー)。
- 原則: **ハッシュ=「全く同じ資産か」、採寸値=「似た体か」。**

**ハッシュの作り方(正規化が肝)**: 頂点量子化(例 0.1mm)・bind/T-pose・シェイプキー 0・安定した頂点順・スケール正規化。対象=ベース頂点+トポロジ+ボーン名(+任意で blendshape フレーム差分)。**SHA-256 一方向**ゆえ形状を漏らさない(指紋であって形状ではない)。

**スナップショット = ハッシュ単体でなく、再現性キー**:
```
{ meshHash, shapekeyValues, pose, scaleNorm, toolVersion, thresholdVersion, margin }
```
**同ハッシュ＋同条件 → 同じ採寸値が再現する。** シェイプキーを動かして測れば別スナップショット(その値も記録)。

## 7. マッチング行列(模擬器)

body 採寸表(§3)× 衣装採寸表(§4)が揃えば、(avatar × garment) のマッチング行列を作れる。各セルは「採寸照合による予測(どのサイズが合うか)」と、実着用での「プリフライト診断による保証(緑/黄/赤＋貫通＋クリアランス、FAMILY_MODEL §8 差分診断)」。これが ECOSYSTEM_VISION の**ショップ↔ユーザーのシミュレーション**を本番プロジェクト内で実体化したもの。クリアランス(LOOSE 側)の両側化は [ROADMAP.md](../ROADMAP.md) フェーズ5「クリアランス統計」。

## 8. 正直な留保

- **頭身は入口の予測であって門番ではない**(FAMILY_MODEL §9)。Neck/Head 基準でぶれ、topY が髪を含む等の誤差(ROADMAP「頭身測定の精度向上」)。最終判定は形状ベクトル(radii)/差分診断。
- **半径=円断面近似**。真の周径(スライス周長)への置き換えは §2 の精度余地。
- **着衣プレファブの混入**(§4)。素体採寸は素体メッシュ、衣装採寸は衣装メッシュに対象を絞る。
- **スケール**は VRChat で任意(FAMILY_MODEL §2)。ファミリー判定は scale 不変量(頭身・正規化 radii)で、絶対 radii は条件として記録。

## 9. 実装状況と順番

- **landed**: アバター body 採寸(分割素体合算・偽 GREEN ガード・dump ボタン・ヘッドレス CLI(a)(b))。実証: unkt × 所持素体4体を OMs でヘッドレス採寸し最近傍=Shinano を算出。
- **次**: ① アバター全素体 BASE を採寸 → SQLite 取込 → クラスタ分析 → ② 衣装内周 measure(§4)→ ③ マッチング行列(§7)。各採寸行に `meshHash`＋条件(§6)を刻む。
- FIT_REPORT_FORMAT(プリフライト判定込みの完全な Fit Report スキーマ)は別途分離予定。本書は採寸(予測層)の範囲。
