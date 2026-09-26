# FanControl.IdeaPad5Pro16ACH6

[FanControl](https://github.com/Rem0o/FanControl.Releases) plugin for the fans of the Lenovo IdeaPad 5 Pro-16ACH6.  
Lenovo IdeaPad 5 Pro-16ACH6 のファンを FanControl で制御するプラグインです。

## Install / インストール

Download `FanControl.IdeaPad5Pro16ACH6.dll` from [Releases](../../releases) and install it with Settings → Install plugin, or place it in FanControl's `Plugins` folder ([FanControl wiki](https://github.com/Rem0o/FanControl.Releases/wiki/Plugins)).  
[Releases](../../releases) の DLL を、設定 →「プラグインをインストール」で入れるか、FanControl の `Plugins` フォルダに配置します。

## Notes / 注意

- 0 % stops the fan; 100 % ≈ 7300 rpm.  
  0% で停止、100% で約 7300rpm。
- After a FanControl crash, the fans keep their last speed until FanControl starts again.  
  FanControl が異常終了すると、次に起動するまで最後の回転数のままです。
- Tested on 82L5 (BIOS GSCN40WW). Use at your own risk.  
  82L5（BIOS GSCN40WW）で確認済み。自己責任でお使いください。

## Build

Requires FanControl to be installed (references its `FanControl.Plugins.dll`).

```
dotnet build FanControl.IdeaPad5Pro16ACH6 -c Release
```

## License

MIT. `LpcIO.bin` is from [PawnIO.Modules](https://github.com/namazso/PawnIO.Modules) (LGPL-2.1).
