# 发布流程（Release）

> 本仓库的发布**不依赖 git tag**（GitHub 对"删除后重建的标签"常常不再触发工作流）。
> 实际做法：改 `.release-version` 里的版本号，推送到 main，CI 自动编译并创建 Release。

## 一、改版本号（5 处）

| 位置 | 内容 |
| --- | --- |
| `.release-version` | `1.0.1`（只写数字，CI 会加 `v` 前缀） |
| `src/AssemblyInfo.cs` | `AssemblyVersion`、`AssemblyFileVersion` |
| `src/Program.cs` | `public const string Version = "1.0.1";` |
| `installer/AssemblyInfo.cs` | `AssemblyVersion`、`AssemblyFileVersion` |
| `CHANGELOG.md` | 新增一节 |

## 二、本地自测

```powershell
.\build\build.ps1 -Sign
.\dist\ShangtiPower.exe --version
.\dist\ShangtiPower.exe --selftest
```

确认：

- 两个 exe 都生成，安装包能完整走完协议 → 目录 → 选项 → 安装
- 首次向导里楼栋下拉能滚动到全部（滚轮）
- 点 ✕ 后进程完全退出

## 三、推送，自动发布

```bash
git add -A
git commit -m "release: v1.0.1"
git push origin main
```

CI（`.github/workflows/build.yml`）在 `windows-latest` 上会：

1. 用系统自带 `csc.exe` 编译主程序和安装包
2. 打印签名状态（CI 里是未签名，除非接入 SignPath）
3. 上传 artifacts（Actions 页面可下载）
4. 读 `.release-version`：
   - 若对应的 Release **不存在** → 创建并附上
     `ShangtiPower.exe`、`ShangtiPowerSetup.exe`、`INSTALL.txt`、`SHA256SUMS.txt`
   - 若已存在 → 用 `--clobber` 更新附件，并清理历史遗留的错误附件名

> 为什么用 `.release-version` 而不是 tag：实测 GitHub 对"删了再建的 tag"不再触发
> `on.push.tags`。用文件控制更可靠，而且每次推送都会重新构建。

## 四、验证产物

```powershell
# 1) 从发布页下载后校验哈希
Get-FileHash .\ShangtiPowerSetup.exe -Algorithm SHA256
# 对比 SHA256SUMS.txt

# 2) 签名状态（接入 SignPath 前为 NotSigned）
Get-AuthenticodeSignature .\ShangtiPowerSetup.exe
```

## 五、接入 SignPath 后的签名发布

见 [SIGNPATH.md](SIGNPATH.md)。审批通过后把
`.github/workflows/release-signed.yml.template` 改名启用，填好 4 个 ID 与 `SIGNPATH_API_TOKEN`，
签名产物会作为新的 artifact 输出；再用签名版替换 Release 附件：

```bash
gh release upload v1.0.0 ShangtiPower.exe ShangtiPowerSetup.exe --clobber
```

## 六、发布检查清单

- [ ] `.release-version` 与代码内版本号一致
- [ ] CHANGELOG 已更新
- [ ] 本地 `--selftest` 通过
- [ ] 安装包能安装 + 卸载
- [ ] 首次向导能选到自己的楼栋（下拉可滚动、不闪退）
- [ ] 点 ✕ 后进程退出
- [ ] 桌面快捷方式 / 开机自启可开可关
- [ ] Release 四个附件齐全（exe ×2、INSTALL.txt、SHA256SUMS.txt）
- [ ] 下载后 SHA256 校验一致
- [ ] （可选）签名版 `Get-AuthenticodeSignature` 为 Valid
