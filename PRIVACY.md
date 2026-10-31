# Privacy Policy / 隐私政策

_Last updated: 2026-10-31_

## English

ShangtiPower ("the app") is a small open-source desktop utility that displays the remaining
electricity balance of the user's own dormitory.

### What we collect

**Nothing.** The app has no account system, no analytics, no telemetry, no crash reporting
and no advertising SDK.

### What is stored on your device

Only the following, in `%APPDATA%\ShangtiPower\config.json` (or `config\config.json` next to
the executable in portable mode):

- the dormitory identifiers you selected (campus / building / floor / room)
- your low-balance threshold
- whether auto-start is enabled

You can delete this at any time with `ShangtiPower.exe --reset` or by uninstalling.

### Network requests

The app makes exactly one kind of request: a form POST to the campus electricity endpoint
(default `https://axinfu.sus.edu.cn:8082/electric`, configurable in source) to read the
balance of the room you selected. No personal data is sent with it.

### Third parties

None. The app does not share data with anyone.

### Contact

Open an issue at <https://github.com/cyy-12322/shangti-power/issues> or use GitHub Security
Advisories for security reports.

## 中文

「上体电量」是一个开源小工具，只用于显示**你自己宿舍**的剩余电量。

- **不采集任何数据**：没有账号、没有埋点、没有崩溃上报、没有广告 SDK。
- **本地只存**：你选的宿舍编号、低电量阈值、是否开机自启（`%APPDATA%\ShangtiPower\config.json`）。
  随时可用 `--reset` 或卸载清除。
- **网络请求只有一种**：向学校电费查询接口 POST 一次表单，读取所选房间的余额，不上传任何个人信息。
- **不向任何第三方共享数据**。

问题反馈：GitHub Issues。
