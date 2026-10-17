# Docs 索引

| 文件 | 用途 | 谁读 |
|---|---|---|
| `../CLAUDE.md` | Claude Code 强制规则（每次会话自动读取） | Claude Code |
| `Architecture.md` | v1 架构、模块、SOP 执行语义、界面视觉规范 | 双方 |
| `AssetGuide.md` | 需下载的资产、下载网址、放置文件夹、生成资产清单 | 你 |
| `PromptTemplates.md` | 里程碑 M0～M7 提示词与通用模板 | 你（复制给 Claude Code） |
| `PROGRESS.md` | 当前进度、待确认参数、待办 | 双方 |
| `DECISIONS.md` | 架构决策记录 | 双方 |
| `ThirdPartyAssets.md` | 第三方资产来源与许可登记 | 双方 |
| `Reference/SOP_TaskMatrix_v1.png` | SOP 任务矩阵参考图 | 双方 |

任务矩阵数据：`Assets/_Project/Config/TaskMatrix/SOP_TaskMatrix_v1.json`（改流程只改它）。

## 使用顺序
1. 按 `AssetGuide.md` 下载资产到 `_Downloads/` 对应子文件夹（M2 之前完成即可，可与 M0、M1 并行）。
2. 在 Claude Code 中依次执行 M0 → M7：每个会话先贴 `PromptTemplates.md` §1，再贴对应里程碑模板。
3. 每次会话结束用 §3.6 更新 `PROGRESS.md`。
