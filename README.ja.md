# NuGet License Collector

ソリューション内のNuGetパッケージのライセンス情報を収集・解析する.NETツールです。

## 機能

- **ライセンス解析**: ソリューション内のすべてのプロジェクトのNuGetパッケージライセンスを自動的に検出・解析
- **複数の出力形式**: テキストとJSON両方の出力形式をサポート
- **包括的なレポート**: パッケージ情報とライセンスの詳細を含む詳細なレポートを生成
- **クロスプラットフォーム**: Windows、macOS、Linuxで動作
- **グローバルツール**: 一度インストールすれば、どこでもコマンドラインから使用可能

## インストール

### グローバル.NETツールとしてインストール

```bash
dotnet tool install -g EXCEEDSYSTEM.NuGetLicenseCollector
```

### ソースからインストール

```bash
git clone https://github.com/exceedsystem/NuGetLicenseCollector.git
cd NuGetLicenseCollector
dotnet pack
dotnet tool install -g --add-source ./bin/Debug EXCEEDSYSTEM.NuGetLicenseCollector
```

## アンインストール

### グローバルツールのアンインストール

```bash
dotnet tool uninstall -g EXCEEDSYSTEM.NuGetLicenseCollector
```

### キャッシュフォルダの削除

アンインストール後、ディスク容量を解放するためにキャッシュフォルダを削除することができます：

**Windows:**
```cmd
rmdir /s "%USERPROFILE%\.exceedsystem\NuGetLicenseCollector"
```

**macOS/Linux:**
```bash
rm -rf ~/.exceedsystem/NuGetLicenseCollector
```

## 使用方法

### 基本的な使用方法

```bash
nuget-license-collector path/to/your/solution.sln
```

### 単一プロジェクトの解析

```bash
nuget-license-collector path/to/your/project.csproj
```

### コマンドラインオプション

```bash
nuget-license-collector <input> [options]
```

#### 引数

- `input` - ソリューションファイル（.sln）またはプロジェクトファイル（.csproj、.vbproj）へのパス

#### オプション

- `-o, --output <output>` - 出力ファイルパス（デフォルト: "nuget-licenses.txt"）
- `-j, --json` - JSON形式で出力
- `-f, --force-refresh` - ライセンスキャッシュをクリアして新しいライセンステキストをダウンロード
- `--help` - ヘルプ情報を表示
- `--version` - バージョン情報を表示

### 使用例

#### ソリューションからテキストレポートを生成

```bash
nuget-license-collector MySolution.sln
```

#### プロジェクトからテキストレポートを生成

```bash
nuget-license-collector MyProject.csproj
```

#### JSONレポートの生成

```bash
nuget-license-collector MySolution.sln --json
```

#### カスタム出力ファイルの指定

```bash
nuget-license-collector MySolution.sln -o licenses-report.txt
```

#### カスタムファイル名でJSONレポートを生成

```bash
nuget-license-collector MySolution.sln -j -o my-licenses.json
```

#### キャッシュされたライセンスを強制更新

```bash
nuget-license-collector MySolution.sln --force-refresh
```

## 出力形式

### テキスト形式

デフォルトのテキスト形式では、以下の情報を含む人間が読みやすいレポートを提供します：
- パッケージ名とバージョン
- 作者情報
- ライセンスタイプとライセンス全文
- プロジェクトURLとライセンスURL（利用可能な場合）
- ライセンスタイプ別にグループ化されたライセンス概要

### JSON形式

JSON形式では、さらなる処理に適した構造化されたデータを提供します：

```json
{
  "packages": [
    {
      "name": "PackageName",
      "version": "1.0.0",
      "author": "Author Name",
      "licenseType": "MIT",
      "licenseText": "License text content...",
      "licenseUrl": "https://...",
      "projectUrl": "https://..."
    }
  ],
  "summary": {
    "totalPackages": 10,
    "generatedAt": "2023-12-01T12:00:00Z"
  }
}
```

## 要件

- .NET 8.0以上
- 有効なNuGetパッケージ参照を持つソリューションファイル（.sln）またはプロジェクトファイル（.csproj、.vbproj）

## 動作原理

1. **入力解析**: ソリューションファイル（.sln）を解析してすべてのプロジェクトを発見、または単一のプロジェクトファイル（.csproj、.vbproj）を解析
2. **パッケージ発見**: プロジェクトアセット（`obj/project.assets.json`）を解析してNuGetパッケージ参照を検出
3. **ライセンス取得**: インテリジェントなキャッシュ機能付きでNuGet.orgに接続してパッケージメタデータとライセンス情報を取得
4. **レポート生成**: 指定された形式で包括的なレポートを作成

## 機能

- **インテリジェントキャッシュ**: パフォーマンス向上のため、ライセンスは30日間ローカルにキャッシュされます
- **複数形式対応**: RTF、HTML、プレーンテキストのライセンス形式をサポート
- **重複排除**: 複数のプロジェクト間でパッケージを自動的に重複排除
- **ネットワーク耐性**: NuGet API呼び出しに対する再試行ロジックを実装
- **クロスプラットフォーム**: Windows、macOS、Linuxで動作

## 貢献

貢献を歓迎します！プルリクエストをお気軽に提出してください。

## 変更履歴

プロジェクトの変更履歴の詳細については、[CHANGELOG.md](CHANGELOG.md)をご覧ください。

## ライセンス

このプロジェクトはMITライセンスの下でライセンスされています。詳細は[LICENSE](LICENSE)ファイルをご覧ください。

## トラブルシューティング

### よくある問題

1. **"File not found"** - ソリューションまたはプロジェクトファイルへのパスが正しいことを確認してください
2. **"Unsupported file type"** - .sln、.csproj、.vbprojファイルのみがサポートされています
3. **"No packages found"** - プロジェクトにNuGetパッケージ参照があり、`dotnet restore`で復元されていることを確認してください
4. **Network errors** - ツールがNuGet.orgにアクセスする必要があるため、インターネット接続を確認してください
5. **MSBuild errors** - プロジェクトが有効な状態でビルド可能であることを確認してください

### サポート

問題が発生した場合や質問がある場合は、[GitHubリポジトリ](https://github.com/exceedsystem/NuGetLicenseCollector/issues)でissueを開いてください。