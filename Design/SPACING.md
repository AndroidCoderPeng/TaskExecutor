# TaskExecutor 设计稿 · 间距对照清单（XAML 落地参考）

> 用途：把 `index.html` 设计稿逐界面拆成「控件层级 → Margin / Padding / Gap」，数值为 px（WPF 中 1:1 对应设备无关像素，设计稿为 100% 缩放）。
> 读法：`Margin="左,上,右,下"`，`Padding` 同理；CSS `gap` 在 WPF 里没有原生属性，按下方「Gap 映射」处理。

## 通用骨架（所有界面共用，先建好一次）

| 控件 | CSS | XAML 对应 |
|------|-----|-----------|
| `.win`（主窗口） | 1280×800，半径 8 | `Window` 固定尺寸，无外边距 |
| `.titlebar`（标题栏） | 高 36，无 padding | `Grid`/`DockPanel` 行高 36 |
| ├ `.app-ico` | `Margin="0,0,8,0"`（右 8），宽高 16 | 图标 `Margin="12,0,8,0"`（左 12 由容器给） |
| ├ `.title` | 占满剩余 | `HorizontalAlignment=Stretch` |
| └ `.win-controls` | 三个按钮各 46×36 | `StackPanel` 横向，按钮 `Width=46` |
| `.win-body` | 横向 flex，无 padding | `Grid` 两列（侧栏 + 主区） |
| `.sidebar`（侧边栏） | 宽 216，`Padding="12,12,12,14"` | `StackPanel` 纵向 |
| ├ `.logo` | `Padding="6,8,10,8"`（上6 右8 下10 左8），`Gap=8` | 图标+文字，间距 8 |
| ├ `.nav` | `Padding="0,0,2,0"`（右2），`Gap=2`，行内 `Margin-Top=2` | 导航列表 `Spacing=2` |
| │  ├ `.nav-label` | `Padding="12,10,4,10"`，`Gap=6` | 分组标题，下边距留 4 |
| │  └ `.nav-item` | 高 32，`Padding="0,10,0,10"`，`Gap=9` | 每项 `Height=32`，文字与图标间距 9 |
| │     └ `.kbd` | `Margin="auto,0,0,0"`（靠右），`Padding="1,5,1,5"` | 快捷键标签（已删除，可忽略） |
| ├ `.sidebar-spacer` | 占满剩余高度 | `Grid`/`DockPanel` 撑开 |
| └ `.adb-card` | `Padding="10,12,10,12"`，`Gap=6` | 连接状态卡片 |
| `.main`（主区域） | 无 padding | `DockPanel` / `Grid` 列 |
| ├ `.topbar`（顶栏） | 高 52，`Padding="0,20,0,20"` | `Height=52`，左右 20 |
| │  ├ `.topbar-right` | `Gap=12` | 右侧按钮组间距 12 |
| │  ├ `.badge.green` | `Padding="5,10,5,10"`，`Gap=6` | 状态徽标 |
| │  └ `.btn` | `Padding="7,14,7,14"`，`Gap=6` | 按钮 |
| └ `.content`（内容区） | `Padding="16,20,16,20"`，纵向 `Gap=12` | 每个界面根容器 |
| `.card`（卡片） | 仅边框，无 padding | 容器 |
| ├ `.card.grow` | 撑满剩余高度 | `VerticalAlignment=Stretch` |
| ├ `.card-head`（卡片头） | `Padding="12,16,12,16"`，左右子项 `Gap=8`/`12` | 标题行 |
| │  ├ `.left` | `Gap=8` | 标题+小字 |
| │  └ `.head-right` | `Gap=12` | 右侧徽标+筛选器 |
| │     ├ `.count-badge` | `Padding="1,6,1,6"` | 计数徽标 |
| │     └ `.filters` | `Gap=12` | 筛选文字组 |
| └ 表格 `table` | 见各视图 | 见下 |
| 弹层 `.overlay` | 覆盖标题栏下区域 | `Grid` 居中 |
| `.dialog`（弹窗） | 宽 460，`Padding="20,24,20,24"`，`Gap=14` | 见底部「新建/编辑任务」 |

### Gap 映射（CSS gap → WPF）
- 纵向列表/卡片：`StackPanel`（纵向）`Spacing=值`，或每个子项 `Margin="0,0,0,值"`（首项/末项按需去边）。
- 横向按钮组：`StackPanel`（横向）`Spacing=值`。
- `Grid` 多列（`.stats` 4列、`.io-grid` 2列、`.cmd-grid` 5列）：用 `Margin="值/2"` 错位模拟 gap，或直接给每格 `Margin="0,0,值,值"` 并让容器负边距抵消（推荐统一给子项 `Margin="0,0,12,12"`，再给容器 `Margin="0,-12,-12,-12"` 抵消外圈）。

---

## 各界面清单

### 1. 无线连接（wifi）
- `.content`：`Padding=16,20`，`Gap=12`
- `.page-note`（说明段）：`Margin="0,0,0,0"`（随 content gap 与下卡片间隔）
- `.card`「当前连接」
  - `.card-head`：`Padding=12,16`
  - `.form`：`Padding=6,16,16`，行 `Gap=2`
  - `.form-row`：`Padding=7,0`，`Gap=12`，label 宽 96 右对齐
  - 右侧两个按钮（`btn-ghost`）：`StackPanel` `Gap=8`
- `.card`「连接新设备」
  - `.card-head`：`Padding=12,16`
  - `.form` / `.form-row`：同上
  - `.form-foot`：`Padding=12,16`，`Gap` 由 `.btns` 提供
    - `.btns`：`Gap=8`

### 2. 任务管理（tasks）
- `.content`：`Padding=16,20`，`Gap=12`
- `.stats`（Grid 4列）：`Gap=12` → 每格 `Margin="0,0,12,12"`
  - `.stat`：`Padding=12,14`，内部纵向 `Gap=4` → 子项 `Margin="0,0,4,0"`
    - `label`（小标题）/`b`（大数字）
- `.card.grow`「任务表格」
  - `.card-head`：`Padding=12,16`；`.left Gap=8`；`.filters Gap=12`
  - `table`
    - `thead th`：`Padding=9,10`；首列 `Padding-Left=16`，末列 `Padding-Right=16`
    - `tbody td`：`Padding=9,10`，上边框 1
    - `.chips`（动作链）：`Gap=4`
      - `.chip`：`Padding=2,6`
    - `.status-pill`：`Padding=2,8`，`Gap=5`
    - `.ops`（操作）：`Gap=12`
- `.card.grow`「运行日志」
  - `.card-head`：`Padding=12,16`
  - `.log-list`：`Padding=2,16,12`（上2 右16 下12 左16），`Gap`（行）由 `.log-row` 提供
  - `.log-row`：`Padding=6,0`，`Gap=10`

### 3. 数据导入导出（sync）
- `.content`：`Padding=16,20`，`Gap=12`
- `.page-note`：说明段
- `.io-grid`（Grid 2列）：`Gap=12` → 每格 `Margin="0,0,12,12"`
  - `.io-card`：`Padding=16`，内部 `Gap=8`
    - `b`（标题）/`p`（说明）
    - `.file-hint`：`Padding=14`，虚线框
    - 底部按钮行：`StackPanel Gap=8`，`Margin-Top=2`

### 4. 目标 APP（apps）
- `.content`：`Padding=16,20`，`Gap=12`
- `.page-note`
- `.card`
  - `.card-head`：`Padding=12,16`；`.left Gap=8`
  - `table`（同表格规范）：`th Padding=9,10`（首/末列左右 16），`td Padding=9,10`
  - `.form-foot`：`Padding=12,16`；左侧输入行 `Gap=8`，右侧 `.btns Gap=8`

### 5. 邮箱配置（mail）
- `.content`：`Padding=16,20`，`Gap=12`
- `.card`
  - `.card-head`：`Padding=12,16`；`.left Gap=8`
  - `.form`：`Padding=6,16,16`，`Gap=2`
    - `.form-row`：`Padding=7,0`，`Gap=12`，label 宽 96 右对齐
  - `.form-foot`：`Padding=12,16`；`.btns Gap=8`

### 6. 企业微信配置（wecom）
- 同「邮箱配置」结构：`.card` → `.card-head Padding=12,16` → `.form Padding=6,16,16,Gap=2` → `.form-row Padding=7,0,Gap=12` → `.form-foot Padding=12,16,.btns Gap=8`
- 开关行 `.form-row` 内 `.switch` 与 `.hint-inline` 间距由 `Gap=12` 提供

### 7. 任务等待时间（wait）/ 8. 任务重置时间（reset）/ 9. 随机时间范围（random）
- 单卡片结构一致：
  - `.card` → `.card-head Padding=12,16`（`.left Gap=8`）
  - `.form Padding=6,16,16,Gap=2`
  - `.form-row Padding=7,0,Gap=12`，label 宽 96 右对齐
  - （random 有两行 `.form-row`）
  - `.form-foot Padding=12,16`，`.btns Gap=8`

### 10. 调度开关（switches）
- `.content`：`Padding=16,20`，`Gap=12`
- `.card`「调度开关」
  - `.card-head`：`Padding=12,16`
  - `.form Padding=6,16,16,Gap=2`；`.form-row Padding=7,0,Gap=12`
- `.card`「节假日数据」
  - `.card-head`：`Padding=12,16`
  - `.form` / `.form-row`：同上
  - `.form-foot Padding=12,16`；`.btns Gap=8`

### 11. 指令测试（cmds）
- `.content`：`Padding=16,20`，`Gap=12`
- `.card`
  - `.card-head`：`Padding=12,16`；右侧 `.btn-ghost` 单按钮
  - `.cmd-grid`（Grid 5列）：`Padding=14,16`，`Gap=12` → 每格 `Margin="0,0,12,12"`
    - `.cmd-btn`：`Padding=14,10`，内部纵向 `Gap=8`（图标 + 文字 + 小字）
  - `.cmd-out`：`Padding=12,16`，高 170，上边框 1

### 12. 常见问题（faq）
- `.content`：`Padding=16,20`，`Gap=12`
- `.card`
  - `.faq-item`：下边框 1（首项无上边距）
    - `.faq-q`：`Padding=13,16`，左右 justify（标题 + 箭头）
    - `.faq-a`：`Padding=0,16,14`（上0 左右16 下14），`LineHeight` 约 1.8

### 13. 关于（about）
- `.content`：`Padding=16,20`
- `.about-card`（居中卡片）：`Padding=36,24`，内部纵向 `Gap=10`，文字居中
  - 图标 52×52；`.ver` 12px；`p` 说明
  - `.file-hint`：虚线框，撑满宽度
  - `.links`（按钮行）：`Gap=16`

---

## 新建 / 编辑任务弹窗（dialog，叠加在内容上）
- `.overlay`：覆盖标题栏以下全屏，半透明黑
- `.dialog`：宽 460，`Padding=20,24`，内部纵向 `Gap=14`
  - `.dlg-head`：`Padding` 含在 gap；标题 + 关闭按钮（`dlg-close Padding=4`）
  - `.device-strip`：`Padding=8,12`，`Gap=8`，浅底圆角
  - `.field`：label `Margin-Bottom=6`
    - `.input` / `.select`：高 34，`Padding=0,12`
    - `.time-row`：`Gap=8`
  - `.checks`：纵向 `Gap=8`
    - `.check`：`Gap=8`（方框 + 文字）
  - `.toggles`：纵向 `Gap=10`
    - `.toggle-row`：左右 justify（文字 + 开关）
  - `.dlg-foot`：上边框 1，`Padding-Top=14`；`.btns Gap=8`

---

## 快速对照（最常用数值）
- 卡片头：`Padding=12,16`
- 表单行：`Padding=7,0`，行间距 `Gap=2`，label 宽 96 右对齐，控件区 `Gap=12`
- 表单底栏：`Padding=12,16`，按钮 `Gap=8`
- 按钮：`.btn Padding=7,14`，`.badge Padding=5,10`
- 内容区：左右 20、上下 16，卡片间 `Gap=12`
- 表格单元格：`Padding=9,10`，首列左 16 / 末列右 16
- 网格卡片（stats/io/cmd）：`Gap=12`
