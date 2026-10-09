using DjSetupList.Models;

namespace DjSetupList.Templates;

public static class BuiltinTemplates
{
    public static IReadOnlyList<SetupTemplate> All { get; } =
    [
        Club(),
        Mobile(),
        Outdoor(),
        Wedding(),
        UsbOnly()
    ];

    public static SetupTemplate? Find(string idOrName) =>
        All.FirstOrDefault(t =>
            t.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
            t.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase));

    private static ChecklistItem Item(string id, string name, string? note = null, bool essential = true) =>
        new() { Id = id, Name = name, Note = note, Essential = essential };

    private static ChecklistCategory Cat(string id, string title, params ChecklistItem[] items) =>
        new() { Id = id, Title = title, Items = items.ToList() };

    private static SetupTemplate Club() => new()
    {
        Id = "club",
        Name = "クラブ / ブース持ち込み",
        Description = "ハウス機材があるクラブ向け。USB・ヘッドフォン・控え機材中心。",
        Categories =
        [
            Cat("media", "メディア / 音源",
                Item("usb-primary", "USB（メイン）", "バックアップと別物理媒体"),
                Item("usb-backup", "USB（バックアップ）"),
                Item("rekordbox", "rekordbox / Serato ライブラリ確認", "解析・キー・BPM"),
                Item("playlist", "セットリスト / プレイリスト控え")),
            Cat("gear", "個人機材",
                Item("headphones", "ヘッドフォン"),
                Item("headphone-adapter", "ヘッドフォン変換アダプタ", "6.3↔3.5", essential: false),
                Item("usb-hub", "USBハブ", essential: false),
                Item("laptop", "ノートPC（バックアップ用）", essential: false)),
            Cat("cables", "ケーブル / 小物",
                Item("rca", "RCA / フォーンケーブル", essential: false),
                Item("usb-cable", "USBケーブル予備"),
                Item("earplugs", "耳栓"),
                Item("tape", "マスキングテープ / ラベル", essential: false)),
            Cat("docs", "連絡・控",
                Item("contact", "会場 / オーガナイザー連絡先"),
                Item("set-time", "セットタイム確認"),
                Item("load-in", "搬入時間・駐車確認", essential: false))
        ]
    };

    private static SetupTemplate Mobile() => new()
    {
        Id = "mobile",
        Name = "モバイルDJ（フル持ち込み）",
        Description = "ミキサー・プレイヤー・スピーカーまで自分で運ぶ案件向け。",
        Categories =
        [
            Cat("players", "プレイヤー / ミキサー",
                Item("cdj-or-controller", "CDJ / コントローラー"),
                Item("mixer", "ミキサー"),
                Item("laptop", "ノートPC"),
                Item("power-strip", "電源タップ（余裕口数）")),
            Cat("pa", "PA / スピーカー",
                Item("speakers", "スピーカー（L/R）"),
                Item("stands", "スピーカースタンド", essential: false),
                Item("sub", "サブウーファー", essential: false),
                Item("mic", "マイク + スタンド", essential: false)),
            Cat("cables", "ケーブル",
                Item("xlr", "XLR ケーブル（必要本数）"),
                Item("speakon", "スピコン / スピーカーケーブル"),
                Item("power", "電源延長コード"),
                Item("rca-trs", "RCA / TRS ケーブル一式"),
                Item("spare-cables", "予備ケーブル一式")),
            Cat("media", "メディア / 音源",
                Item("usb-primary", "USB（メイン）"),
                Item("usb-backup", "USB（バックアップ）"),
                Item("offline-library", "オフライン音源確認")),
            Cat("pack", "搬入 / その他",
                Item("cases", "ケース / バッグ"),
                Item("gloves", "搬入用手袋", essential: false),
                Item("multitool", "マルチツール / ドライバー", essential: false),
                Item("flashlight", "ライト", essential: false),
                Item("water", "水分・軽食", essential: false))
        ]
    };

    private static SetupTemplate Outdoor() => new()
    {
        Id = "outdoor",
        Name = "屋外 / フェス",
        Description = "屋外特有の電源・天候・養生を含むチェックリスト。",
        Categories =
        [
            Cat("power", "電源 / 安全",
                Item("power-check", "電源容量・系統確認"),
                Item("extension", "延長コード（屋外対応）"),
                Item("tape-down", "養生テープ / ケーブルマット"),
                Item("grounding", "アース・漏電対策確認", essential: false)),
            Cat("weather", "天候対策",
                Item("cover", "機材カバー / テント確認"),
                Item("weights", "風対策ウェイト", essential: false),
                Item("towel", "タオル・乾燥剤", essential: false)),
            Cat("gear", "機材",
                Item("controller", "コントローラー / CDJ"),
                Item("mixer", "ミキサー", essential: false),
                Item("headphones", "ヘッドフォン"),
                Item("speakers", "スピーカー", essential: false)),
            Cat("media", "メディア",
                Item("usb-primary", "USB（メイン）"),
                Item("usb-backup", "USB（バックアップ）"),
                Item("phone-hotspot", "テザリング予備", "同期失敗時用", essential: false)),
            Cat("ops", "オペレーション",
                Item("schedule", "タイムテーブル確認"),
                Item("stage-plot", "ステージプロット / 入力リスト", essential: false),
                Item("contact", "現場連絡先"))
        ]
    };

    private static SetupTemplate Wedding() => new()
    {
        Id = "wedding",
        Name = "結婚式 / パーティー",
        Description = "MC連携・BGM・リクエスト・禁止曲を含むイベント向け。",
        Categories =
        [
            Cat("program", "プログラム",
                Item("run-of-show", "タイムテーブル / 進行表"),
                Item("ceremony-tracks", "入場・退場・記念曲リスト"),
                Item("do-not-play", "禁止曲リスト"),
                Item("requests", "リクエスト受付方法確認", essential: false)),
            Cat("audio", "音響",
                Item("controller", "コントローラー / ミキサー"),
                Item("mic", "ワイヤレスマイク"),
                Item("spare-battery", "マイク予備電池"),
                Item("speakers", "スピーカー", essential: false)),
            Cat("media", "メディア",
                Item("usb-primary", "USB（メイン）"),
                Item("usb-backup", "USB（バックアップ）"),
                Item("laptop", "ノートPC"),
                Item("playlist", "プレイリスト控え（紙 or 端末）")),
            Cat("look", "見た目 / その他",
                Item("outfit", "衣装確認"),
                Item("business-card", "名刺 / 連絡先カード", essential: false),
                Item("tips-box", "チップボックス", essential: false),
                Item("water", "水分"))
        ]
    };

    private static SetupTemplate UsbOnly() => new()
    {
        Id = "usb-only",
        Name = "USBのみ（超軽量）",
        Description = "機材持ち込みなし。USBと最低限の身の回り品だけ。",
        Categories =
        [
            Cat("media", "メディア",
                Item("usb-primary", "USB（メイン）"),
                Item("usb-backup", "USB（バックアップ）"),
                Item("export-check", "エクスポート形式確認", "CDJ機種に合わせる")),
            Cat("personal", "身の回り",
                Item("headphones", "ヘッドフォン", essential: false),
                Item("earplugs", "耳栓"),
                Item("water", "水分"),
                Item("contact", "現場連絡先"))
        ]
    };
}
