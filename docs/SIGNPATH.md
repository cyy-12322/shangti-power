# 免费代码签名：SignPath Foundation 申请指南

> 目标：让用户下载安装时**不再出现「未知发布者 / SmartScreen 拦截」**，且**不用花钱**。
> 代价：签名发布者会显示为 **SignPath Foundation**（不是你的名字），因为证是签给基金会、由他们为你的仓库背书。

---

## 一、SignPath 是什么

[SignPath Foundation](https://signpath.org/) 为**开源项目**免费提供代码签名：

- 不需要你的身份证/营业执照
- 他们验证「二进制确实由你的**公开仓库**构建」，然后以基金会名义签名
- 私钥保存在他们的 HSM（硬件安全模块），你拿不到，也无法滥用
- 签名通过 CI（GitHub Actions）自动完成

---

## 二、申请前的准备清单

| 项 | 要求 | 本仓库状态 |
| --- | --- | --- |
| 公开 GitHub 仓库 | 必须 | ⬜ 你需要推上去 |
| 开源许可证 | OSI 认可（我们用 MIT） | ✅ LICENSE |
| README | 说明项目用途、构建方法 | ✅ README.md / README.en.md |
| 至少一个 Release | 有可下载的构建产物 | ⬜ 打 tag 后自动生成 |
| 构建可复现 | 别人能按文档从源码构建出同样产物 | ✅ `build/build.ps1` 一键构建，无私有依赖 |
| 无恶意行为 | 不采集隐私、无隐藏后门 | ✅ 纯电费查询、无遥测 |

> 重要：**先把仓库推上去、打一个 `v1.0.0` tag 生成 Release**，再去申请，通过率更高。

---

## 三、申请步骤

1. 打开 <https://signpath.org/apply>，填写申请表（英文）
2. 等待邮件（一般几天到 1–2 周）
3. 通过后会收到 SignPath 的邀请，你需要在 GitHub 安装 **SignPath GitHub App** 并授权仓库
4. 在 SignPath 控制台创建：
   - Organization
   - Project（关联你的仓库）
   - Signing Policy（用 Foundation 的证书）
   - Artifact Configuration（定义要签的文件）
5. 拿到 4 个 ID + 创建 API Token，填进本仓库

---

## 四、申请表文案（可直接粘贴/改写）

**Project name**

```
ShangtiPower (上体电量)
```

**Repository URL**

```
https://github.com/cyy-12322/shangti-power
```

**Short description**

```
ShangtiPower is a tiny open-source Windows desktop tool that shows students of Shanghai
University of Sport their dormitory's remaining electricity balance. It queries a public,
login-free campus endpoint and displays the result in a small native window. It contains no
telemetry, no account system and no network activity other than the single balance query.
```

**License**

```
MIT
```

**Programming language / build**

```
C# (.NET Framework 4.8), built with the compiler shipped by Windows (csc.exe).
No NuGet, no npm, no private dependencies.
```

**Build instructions**

```
Windows runner (windows-latest):
  powershell -ExecutionPolicy Bypass -File .\build\build.ps1

Outputs:
  dist\ShangtiPower.exe        (main application, ~86 KB)
  dist\ShangtiPowerSetup.exe   (installer, embeds the main exe)

A release is produced by pushing a v* tag; the GitHub Actions workflow builds both
binaries and publishes them via `gh release create`.
```

**Artifacts to be signed**

```
dist\ShangtiPower.exe
dist\ShangtiPowerSetup.exe
```

**Why do you need signing?**

```
The binaries are currently unsigned, so every user sees the Windows SmartScreen
"unrecognized app" warning on first run, which makes the tool look suspicious.
We are a small non-commercial student utility with no revenue; a paid CA certificate
is not affordable. SignPath Foundation signing would remove that warning and let users
verify the binary was built from our public repository.
```

**Anything else**

```
The app queries https://axinfu.sus.edu.cn:8082 (with the URL configurable) and stores the
user's dorm selection locally. It never uploads personal data. Source is fully open and
the build is reproducible from a clean checkout with the command above.
```

---

## 五、审批通过后要做什么

1. 在仓库 **Settings → Secrets and variables → Actions** 添加：
   - `SIGNPATH_API_TOKEN`
2. 打开 [`.github/workflows/release-signed.yml.template`](../.github/workflows/release-signed.yml.template)，
   把文件重命名为 `release-signed.yml`，并在文件头补上 `name:` 行，然后替换 4 个占位符：
   - `organization-id`
   - `project-slug`
   - `signing-policy-slug`
   - `artifact-configuration-slug`
3. 注意：SignPath 的 GitHub Action 目前要求先把待签文件上传为 artifact，
   再通过 `github-artifact-id` 传入；具体参数以你收到的官方文档为准。
4. 手动触发一次签名工作流 → 下载 `ShangtiPower-signed` → 用下面的命令验证：

```powershell
Get-AuthenticodeSignature .\ShangtiPowerSetup.exe
# Status 应为 Valid，Publisher 显示 SignPath Foundation
```

5. 验证通过后，用签名版替换 Release 附件即可。之后所有用户下载都不会再被拦截。

---

## 六、审核常见被拒原因

- 仓库里没有构建脚本 / 无法复现构建
- 许可证缺失或不明确
- 构建依赖私有资源（私有 npm 包、私有证书、内部服务器）
- 项目没有实际功能（空壳仓库）
- 项目包含恶意/可疑行为（打包器、下载器、混淆代码）

> 本仓库都不涉及：纯 C# 源码、MIT、零第三方依赖、构建脚本公开。
