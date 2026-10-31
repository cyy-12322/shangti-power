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

## 四、申请表：逐项怎么填（照抄即可）

| 表单字段 | 填什么 |
| --- | --- |
| **Repository URL** | `https://github.com/cyy-12322/shangti-power` |
| **Homepage URL** | `https://github.com/cyy-12322/shangti-power` |
| **Download URL** | `https://github.com/cyy-12322/shangti-power/releases` |
| **Privacy Policy URL** | `https://github.com/cyy-12322/shangti-power/blob/main/PRIVACY.md` |
| **Wikipedia URL** | 留空（可选字段） |
| **Maintainer Type** | 选 **Individual**（个人开发者）；没有这个选项就选 Individual/Personal |
| **Build System** | 选 **GitHub Actions** |
| **First Name** | 你的名（如 `Yu` 或 `雨`） |
| **Last Name** | 你的姓（如 `Chen` 或 `陈`） |
| **Email** | `2744438196@qq.com` |
| **Company Name** | 填 `N/A`（个人项目，没有公司就写 N/A 或留空） |
| **Primary Discovery Channel** | 选 **GitHub**（没有该选项就选 Other） |
| **Please specify the exact source** | `GitHub repository search; shared among classmates at Shanghai University of Sport` |

> 注意：Homepage / Download / Privacy 三个 URL 必须**能直接打开**，
> 所以 `PRIVACY.md`、`SECURITY.md`、Release 页都是必须存在的（本仓库已有）。

### 表单里的 Tagline / Category / Description / Reputation

**Tagline\***（一句话标语，别超过 80 字符）

```
Check your dorm's remaining electricity in one click.
```

**Category\***（下拉框，按选项选最接近的）

优先级：`Desktop Application` → `Utilities` → `Other`（都有就填 Other 并在描述里说明）

**Description\***（项目描述，一段话）

```
ShangtiPower is a small open-source Windows desktop utility that shows students of Shanghai
University of Sport their dormitory's remaining electricity balance. It queries a public,
login-free campus endpoint and renders the result in a compact native window with a dark UI.
The app stores only the user's dorm selection locally, sends exactly one HTTP request per
query, and contains no telemetry, no accounts and no third-party dependencies.
It is written in C# (.NET Framework 4.8) and built reproducibly by GitHub Actions using the
csc compiler that ships with Windows; all source and build scripts are public under MIT.
```

**Reputation\***（项目可信度/知名度说明 —— 新项目如实写，重点讲"可验证"）

```
New but fully public project. Released under the MIT license at
https://github.com/cyy-12322/shangti-power with source code, build script, binaries,
SHA-256 checksums and a privacy policy. It is used by a small group of students at
Shanghai University of Sport for checking their own dormitory balance; there is no
commercial distribution and no bundled third-party code. Every binary is produced by
GitHub Actions directly from the public repository, so the build is reproducible and
inspectable. No security incidents, no telemetry, no network activity other than the
single balance query.
```

> 如果 Reputation 其实是**数字/星级**字段（例如填 star 数），新项目就填 `0` 或选 `New / Not yet established`，不要编数字。
### 如果表单还有额外的自由描述框（不同年份会变）

**Project description**

```
ShangtiPower is a tiny open-source Windows desktop tool that shows students of Shanghai
University of Sport their dormitory's remaining electricity balance. It queries a public,
login-free campus endpoint and displays the result in a small native window. No telemetry,
no accounts, one HTTP request per query.
```

**Build instructions**

```
Windows (GitHub Actions, windows-latest):
  powershell -ExecutionPolicy Bypass -File .\build\build.ps1

Outputs:
  dist\ShangtiPower.exe        main application (~86 KB)
  dist\ShangtiPowerSetup.exe   installer, embeds the main exe (~148 KB)

No NuGet, no npm, no private dependencies — only the csc.exe that ships with Windows.
```

**Artifacts to sign**

```
dist\ShangtiPower.exe
dist\ShangtiPowerSetup.exe
```
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
