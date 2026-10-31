# 代码签名指南

> 目的：消除 Windows 的「未知发布者 / SmartScreen 已保护你的电脑」提示，让安装体验和正规软件一致。

三种方案，按「花钱多少 / 麻烦程度」排序：

| 方案 | 成本 | 效果 | 适用 |
| --- | --- | --- | --- |
| 本机自签名 | 免费 | 只对你自己的电脑有效 | 开发调试 |
| SignPath Foundation | 免费 | 所有用户无提示（发布者显示 SignPath Foundation） | 开源项目首选 |
| CA 商业证书（OV/EV） | €60–400/年 | 发布者显示你自己/公司；EV 立即零提示 | 正式分发 |

---

## 一、本机自签名（开发调试）

```powershell
# 编译 + 自签名 + 导入本机信任（当前用户，无需管理员）
.\build\build.ps1 -Sign
```

脚本会：

1. 创建（或复用）一张代码签名证书：`CN=ShangtiPower Local Dev`
2. 导入到「当前用户」的 **受信任根证书** 与 **受信任的发布者**
3. 按「签主程序 → 内嵌进安装包 → 签安装包」的顺序签名

验证：

```powershell
Get-AuthenticodeSignature .\dist\ShangtiPowerSetup.exe
# Status: Valid
```

移除信任（清理）：

```powershell
.\build\sign.ps1 -RemoveSelfSign
```

> 自签名只在**信任了该证书的机器**上有效。发给别人仍需对方导入证书（不推荐），
> 所以对外分发请用下面两种方案。

---

## 二、SignPath Foundation（开源免费）

见 [SIGNPATH.md](SIGNPATH.md)。

要点：
- 需要公开仓库 + Release + 可复现构建
- 审核通过后签名在 CI 里完成（私钥在对方 HSM）
- 发布者显示为 **SignPath Foundation**

---

## 三、CA 商业证书（OV / EV）

### 申请渠道

| 渠道 | 大致价格 | 说明 |
| --- | --- | --- |
| Certum Open Source（开源证书） | 免费门槛：GitHub 1k+ Star；否则 €329/年 | 中国区代理：certumcodesign.cn |
| Certum / Sectigo / SSL.com 等 OV | €60–200/年 | 个人可申请，需身份证明 |
| EV 代码签名 | €300–500/年 | 需要硬件 token，SmartScreen 立即信任 |
| Azure Trusted Signing | 约 $9.99/月 | 微软云签名，需身份/组织验证 |

### 拿到 `.pfx` 后

```powershell
.\build\build.ps1 -Sign -PfxPath "D:\cert\my.pfx" -PfxPassword "你的密码"
```

脚本会先签主程序、再嵌入安装包、最后签安装包（顺序不能反，否则内层程序没签名）。
时间戳默认用 DigiCert 的公共 TSA，保证证书过期后签名仍有效。

验证：

```powershell
Get-AuthenticodeSignature .\dist\ShangtiPower.exe
Get-AuthenticodeSignature .\dist\ShangtiPowerSetup.exe
```

### 证书类型区别

- **OV**：验证组织/个人身份；新文件初期 SmartScreen 可能仍有少量提示，随下载量积累消失
- **EV**：硬件 token + 更严格审核；签名后**立即**获得 SmartScreen 信任
- **自签名**：只有导入证书的机器信任

---

## 四、构建流水线说明

`build\build.ps1` 的参数：

| 参数 | 说明 |
| --- | --- |
| （无） | 只编译，不签名 |
| `-Sign` | 用本机自签名证书签名 |
| `-Sign -PfxPath <pfx> -PfxPassword <pwd>` | 用 CA 证书签名 |
| `-DebugBuild` | 不优化、便于调试 |

单独签名可执行文件：

```powershell
.\build\sign.ps1 -Thumbprint "证书指纹"
.\build\sign.ps1 -File ShangtiPowerSetup.exe
```
