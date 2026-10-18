# TextureFireWorks

Unityで文章を入力し、VRChatの夜空へ打ち上げる文字花火を作るエディターです。
上昇する光と尾から、文字型の火花が広がり、少し残って落下・消失します。

![30mの観覧位置から描画した文字花火](docs/images/thank-you.png)

## 対応環境

- PC版VRChatワールド / Built-in Render Pipeline
- Unity **2022.3.22f1**
- VRChat Worlds SDK **3.10.4以上**（UdonSharp同梱）
- TextMesh Pro **3.0.6** と TMP Essential Resources
- 日本語を含む任意のTMPフォント（制作時に利用者が指定）

Quest、URP、アバター、ワールド内の自由入力は初版の対象外です。ワールドへアップロードする通常のMonoBehaviourは使用していません。

## 導入

配布版は[TextureFireWorks-0.1.0.unitypackage](Releases/TextureFireWorks-0.1.0.unitypackage)です。Worlds SDKとTMPを導入済みのワールドへインポートすると、制作ツールと「ありがとう！」のサンプルが入ります。
サンプルは`Assets/TFWSamples/ThankYou/ThankYou.prefab`です。ソースを直接導入する場合は次の手順を使います。

1. Creator Companionで作成したワールドをUnityで開きます。
2. Unityの `Window > Package Manager > + > Add package from disk...` から、このリポジトリの `Packages/com.maita.texture-fireworks/package.json` を選択します。
3. TMPの素材が未導入なら `Window > TextMeshPro > Import TMP Essential Resources` を実行します。
4. `Tools > TextureFireWorks > 文字花火エディター` を開きます。

VPMのSDKはUnity Package Managerから自動導入しません。先にCreator Companionで **Worlds SDK** を導入してください。
UPMで入れた同じツールと`.unitypackage`版を同時に導入しないでください。同名アセンブリ・GUIDが重複します。

Package ManagerのTextureFireWorksにある「Samples」から「文字花火のサンプル」をImportすると、「ありがとう！」と複数行の生成済み発射台を試せます。
サンプルの再生にフォントは不要です。サンプル設定から文章を作り直す場合だけ、手持ちのTMPフォントを指定してください。

## 文章を花火にする

1. エディターの「文章」に文字を入力し、「フォント（TMP）」へTMP Font Assetを指定します。
2. 「プレビューを生成」を押します。日本語、英数字、改行に対応します。
3. 色・密度・サイズ・時間を調整し、再生成します。「再生」「停止」「文字の完成時点」と時間スライダーで見え方を確認できます。
4. 「Prefabを保存」を押し、Assets内の保存先を選びます。
5. 保存されたPrefabをワールドのHierarchyへ配置します。
6. 親オブジェクトを回転して文字面を観覧エリアへ向けます。初期状態ではローカル **−Z側** から正しく読めます。
7. 「発射位置」「開花位置」をScene上で移動します。発射台を選ぶと位置ハンドルと観覧方向の矢印が表示されます。
8. 同梱の青い「打ち上げボタン」へ近づいてInteractすると再生します。

保存のたびに別名の専用フォルダーを作ります。中にはPrefab、火花のメッシュ・マテリアル、ボタン素材、編集用`Settings.asset`が入ります。
次回は「保存済み設定」に`Settings.asset`を指定して編集できます。生成したPrefab自体は文字やフォントへ依存しません。

### 日本語フォント

対応フォントファイルをUnityへ取り込み、`Create > TextMeshPro > Font Asset` などでTMP Font Assetを作成します。
Dynamic Atlasであれば必要な文字を制作時に追加できます。Staticの場合は使用する文字を先に含めてください。
未収録文字は生成前に表示し、別のフォントで黙って置き換えることはありません。絵文字などの補助文字、タブ、リッチテキストの装飾は対応しません。
フォント・音源の再配布条件は使用する素材の条件に従ってください。このリポジトリには第三者のフォント・音源を同梱しません。

### 初期設定

| 項目 | 初期値 |
|---|---|
| 文章 | ありがとう！ |
| 文字サイズ | 1.8m（フォントの行高基準） |
| 行間 | 15 |
| 火花密度 | 0.45 |
| 文字の点数上限 | 8,000点 |
| 装飾 | 252点（周囲180・光の玉1・尾70・閃光1） |
| 火花サイズ | 0.065m |
| 打ち上げ高さ | 15m |
| 上昇 / 展開 / 維持 / 消失 | 2 / 1.2 / 2 / 2秒 |
| 落下加速度 | 1.5 |

上限を超える場合は密度を下げてください。文章は512文字以内です。長文は文章ごとに発射台を分けると読みやすくなります。
初期の観覧距離は30mです。太めのフォントを選び、実際の観覧エリアからも確認してください。

## 配置・再生・同期

- 1つの発射台に1種類の文章を持たせます。複製すれば別の場所や別の文章を配置できます。
- 各ボタンの`launcher`へ対応する発射台を割り当てます。生成済みのボタンは接続済みです。
- 操作は全員へ同期します。所有者が発射要求を受け付け、発射番号とVRChatのサーバー時刻を共有します。
- 受信後は共通時刻のフレームに追従します。ネットワーク遅延により、全員の画面で全く同じ瞬間に始まることを保証するものではありません。
- 再生中の連打は無視します。終了後に再発射できます。発射の同期送信に失敗した場合は再送します。
- 途中参加者は演出中ならその時点の花火を表示し、過去の音は鳴らしません。終了した花火は再生しません。
- 所有者が退出しても、同期済みの発射時刻を維持します。
- 上昇音・開花音は任意です。生成前にエディターでクリップを指定するか、Prefab内のAudioSourceへ設定します。
- Unity Play Modeでは発射台のInspectorの「打ち上げる」から確認できます。VRChatの同期確認は複数クライアントで行います。

文字の形は固定された平面です。各火花だけがカメラに向きます。裏側から見ると文章は反転します。
花火メッシュの静的バッチングは有効にしないでください。シェーダーで移動するため、カリング用Boundsは一辺4,000mを確保しています。発射・開花位置は親から1,000m以内を目安に配置してください。

## unitypackageで渡す

`Tools > TextureFireWorks > 導入用unitypackageを書き出す` を選びます。
生成済みPrefabをProjectウィンドウで選択してから実行すると、そのPrefabと実行時に必要な素材も含めます。
選択していなければ制作ツールだけを書き出します。SDK・TMP・編集用設定・フォントは同梱しません。
書き出し先に既存のファイルがある場合は上書きせずエラーにします。別名を選んでください。

受け取る側もWorlds SDKとTMPを先に導入します。`.unitypackage`版は`Assets/TextureFireWorks/Package`へツールを配置します。
ワールドのアップロードや公開操作は行いません。

## 開発と検証

本体は`Packages/com.maita.texture-fireworks`です。`.validation/`はローカルの専用検証ワールド、`artifacts/`は検証出力で、Git管理しません。
既存の他ワールドは変更せず、SDKのローカルコピーを専用ワールドに配置して検証します。

検証の実施結果と残る手動確認は[検証記録](docs/VALIDATION.md)を参照してください。
Unity上での生成・描画・Prefab保存・UdonSharp変換と、VRChat SDKのローカルワールドバンドル生成を確認しています。複数クライアントでの同期とVR実機表示は未検証です。

## 参考資料

- [VRChatが対応するUnity](https://creators.vrchat.com/sdk/upgrade/current-unity-version/)
- [UdonSharp](https://creators.vrchat.com/worlds/udon/udonsharp/)
- [同期変数](https://creators.vrchat.com/worlds/udon/networking/variables/)
- [途中参加と同期](https://creators.vrchat.com/worlds/udon/networking/late-joiners/)
