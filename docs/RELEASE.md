# 发布流程（Release）

## 一、改版本号（4 处，必须一致）

| 文件 | 字段 |
| --- | --- |
| `src/AssemblyInfo.cs` | `AssemblyVersion`、`AssemblyFileVersion` |
| `src/Program.cs` | `public const string Version = "1.0.0";` |
| `installer/AssemblyInfo.cs` | `AssemblyVersion`、`AssemblyFileVersion` |
| `CHANGELOG.md` | 新增一节记录本次改动 |

## 二、本地自测

```powershell
# 编译 + 本机签名 + 跑一次自检（需要先完成首次设置）
.\build\build.ps1 -Sign
.\dist\ShangtiPower.exe --version
.\dist\ShangtiPower.exe --selftest
```

确认：

- `dist\ShangtiPower.exe`、`dist\ShangtiPowerSetup.exe` 都生成
- 双击安装包能走完协议 → 目录 → 选项 → 安装
- 设置页「GitHub 项目主页」能打开
- 点 ✕ 后进程完全退出

## 三、提交代码

```bash
git add -A
git commit -m "release: v1.0.0"
git push origin main
```

## 四、发布（自动触发 CI 打包 + 发布）

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions 的 `Build` 工作流会自动：

1. `windows-latest` 上跑 `build/build.ps1`
2. 上传 artifacts
3. 创建 Release，附带：
   - `ShangtiPower.exe`
   - `ShangtiPowerSetup.exe`
   - `安装说明.txt`
   - `SHA256SUMS.txt`

> 首次使用前，在仓库 **Settings → Actions → General** 确认 Workflow permissions 为
> 「Read and write permissions」，否则 `gh release create` 会失败。

## 五、验证产物

```powershell
# 1) 哈希（可与 Release 上的 SHA256SUMS.txt 对比）
Get-FileHash .\ShangtiPowerSetup.exe -Algorithm SHA256

# 2) 签名（签名版应为 Valid；未签名显示 NotSigned）
Get-AuthenticodeSignature .\ShangtiPowerSetup.exe
```

## 六、如果已接入 SignPath（签名版发布）

1. 先把 `.github/workflows/release-signed.yml.template` 改名为 `release-signed.yml` 并填好 4 个 ID，再手动触发（输入 `yes`）
2. 下载 `ShangtiPower-signed` artifact
3. 验证 `Get-AuthenticodeSignature` 为 `Valid`、发布者为 `SignPath Foundation`
4. 用签名版替换 Release 里的两个 exe（`gh release upload v1.0.0 ... --clobber`）

## 七、发布检查清单

- [ ] 版本号 4 处一致
- [ ] CHANGELOG 已更新
- [ ] 本地 `--selftest` 通过
- [ ] 安装包能完整安装 + 卸载
- [ ] 首次设置向导能选到自己的楼栋（下拉可滚动）
- [ ] 点 ✕ 后进程退出
- [ ] 桌面/开始菜单快捷方式正常
- [ ] 开机自启可开可关
- [ ] Release 附带了安装说明和 SHA256
- [ ] （可选）签名版已验证 Valid
