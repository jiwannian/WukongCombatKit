# WukongCombatKit

《黑神话：悟空》独立单机 C# Mod。手动操作时提高战斗手感，不改 AutoPerfectDodge，不自动闪避，不强制完美闪避，不改伤害、削韧、血量。

## 功能

1. **立刻闪避（可选）**  
   打开后，按下闪避键时只要角色在攻击或闪避中，就立即开始这次闪避。可打断普攻，也可无间隔接下一次翻滚。关闭后完全回到原版闪避窗口。死亡/假死仍不能闪。

2. **有限范围全方位攻击**  
   玩家攻击以自身为中心做球形检测，默认距离 `2500`。背后、侧面、空中、高处的敌人，以及附近可打碎/可交互物品，只要没有真正隔墙，都能被这次攻击打中。隔墙仍不命中。伤害和削韧走原版流程。

## 安装

前置：[B1CSharpLoader](https://github.com/czastack/B1CSharpLoader/releases) v0.0.8+

把发布文件放到：

```
BlackMythWukong/b1/Binaries/Win64/CSharpLoader/Mods/WukongCombatKit/
  WukongCombatKit.dll
  config.json
```

启动游戏后看日志：

```
BlackMythWukong/b1/Binaries/Win64/CSharpLoader/Mods/WukongCombatKit/WukongCombatKit.log
```

应出现 `WukongCombatKit C# mod Init` 以及补丁注册记录。

## 配置

```json
{
  "EnableImmediateDodge": true,
  "EnableOmniHit": true,
  "MaxAttackRange": 2500,
  "DebugLog": false
}
```

| 项 | 作用 |
|---|---|
| `EnableImmediateDodge` | 立刻闪避。`true` 打开，`false` 关闭并回到原版闪避 |
| `EnableOmniHit` | 有限范围全方位攻击 |
| `MaxAttackRange` | 全方位攻击半径，默认 `2500` |
| `DebugLog` | 调试日志 |

游戏内快捷键：

- `F7` 开关立刻闪避，并写回 `config.json`
- `F8` 重新读取配置

关闭对应开关后该功能立即回到原版行为。

## 范围

- 只影响本地单机手动操作
- 不自动闪避，不把普通闪避改成完美闪避
- 不改多人/联机，不绕过反作弊或 DRM
- 不提交游戏程序集

## 编译

需要 .NET SDK 8+，以及 B1CSharpLoader 的 `GameDll`（仅本地引用）。

```powershell
Copy-Item "<B1CSharpLoader>\GameDll\*" lib\
Copy-Item "<B1CSharpLoader>\CSharpLoader\0Harmony.dll" lib\
Copy-Item "<B1CSharpLoader>\CSharpLoader\CSharpModBase.dll" lib\
dotnet test
dotnet build src\WukongCombatKit\WukongCombatKit.csproj -c Release
.\scripts\deploy.ps1
```
