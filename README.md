# WukongCombatKit

独立单机 C# Mod，给《黑神话：悟空》两项战斗手感调整：

1. 轻攻/普通棍法连招中可按空格闪避打断
2. 玩家攻击可打中以自身为中心、无遮挡、几乎无限距离的敌人

不修改 AutoPerfectDodge，不自动闪避，不强制完美闪避，不改伤害/削韧/血量。

## 安装

前置：[B1CSharpLoader](https://github.com/czastack/B1CSharpLoader/releases) v0.0.8+

把文件放到：

```
BlackMythWukong/b1/Binaries/Win64/CSharpLoader/Mods/WukongCombatKit/
  WukongCombatKit.dll
  config.json
```

启动游戏后查看：

```
BlackMythWukong/b1/Binaries/Win64/CSharpLoader/Mods/WukongCombatKit/WukongCombatKit.log
```

应出现 `WukongCombatKit C# mod Init` 以及补丁注册日志。

## 配置

```json
{
  "EnableDodgeCancel": true,
  "EnableOmniHit": true,
  "MaxAttackRange": 100000,
  "DebugLog": false
}
```

`F8` 重新读取配置。关闭对应开关后该功能立即回到原版行为。

## 编译

需要 .NET SDK 8+，以及 B1CSharpLoader 的 `GameDll`（仅本地引用，不提交）。

```powershell
Copy-Item "C:\Users\zhanh\AppData\Local\Temp\opencode\b1cs\B1CSharpLoader-master\GameDll\*" lib\
Copy-Item "C:\Users\zhanh\AppData\Local\Temp\opencode\b1cs_release\b1\Binaries\Win64\CSharpLoader\0Harmony.dll" lib\
Copy-Item "C:\Users\zhanh\AppData\Local\Temp\opencode\b1cs_release\b1\Binaries\Win64\CSharpLoader\CSharpModBase.dll" lib\
dotnet test
dotnet build WukongCombatKit.sln -c Release
.\scripts\deploy.ps1
```

## 范围

- 只放宽“普攻状态是否允许被闪避取消”，空格仍走原版闪避输入
- 命中后仍走原版伤害/削韧/特效
- 隔墙、隔山射线挡住则不命中
- 不改多人/联机，不绕过反作弊或 DRM
