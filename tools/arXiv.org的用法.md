**arXiv**（[https://arxiv.org/](https://arxiv.org/)）是全球最大的免费学术预印本（Preprint）平台，涵盖计算机科学（cs）、物理学（math）、数学、定量生物学、金融等领域。

在 arXiv 上，你可以进行论文检索、在线阅读下载、论文发表（预印本）等操作。以下是 arXiv 的详细用法指南：

---

### 一、 核心功能：如何高效检索与阅读论文

#### 1. 快速精准搜索

* **基础搜索框**：在右上角搜索框输入论文标题、作者姓名或论文 ID。
* **高级搜索（Advanced Search）**：
* **按字段过滤**：选择 `Title`（标题）、`Author`（作者）、`Abstract`（摘要）或 `All fields`。
* **学科分类筛选**：如 `Computer Science` -> `Artificial Intelligence (cs.AI)` 或 `Machine Learning (cs.LG)`。
* **逻辑运算符**：支持使用 `AND`、`OR`、`ANDNOT` 进行组合检索（如：`ti:"large language model" AND au:Vaswani`）。



#### 2. 理解论文编号与链接格式

arXiv 的论文 ID 格式为 `YYMM.NNNNN`（年份月份.序号），基于 ID 可以通过直接拼写 URL 快速访问：

* **摘要页（Abstract）**：`[https://arxiv.org/abs/2301.12345](https://arxiv.org/abs/2301.12345)`
* **PDF 直接下载**：`[https://arxiv.org/pdf/2301.12345.pdf](https://arxiv.org/pdf/2301.12345.pdf)`
* **HTML 网页版（部分论文支持）**：将 URL 中 `/abs/` 改为 `/html/`，可在浏览器直接阅读排版良好的网页版。

#### 3. 追踪最新研究（Catchup / Recent）

* 进入具体学科页面（如 `cs.CL` - 自然语言处理），点击右侧的 **`recent`** 查看最近工作日更新的论文，或点击 **`new`** 查看当日最新提交。

---

### 二、 进阶技巧：如何更好地利用 arXiv 资源

#### 1. 版本控制（Version History）

arXiv 允许作者更新论文。在摘要页右下角的 **`Submission history`** 可以看到：

* `v1`, `v2`, `v3` 表示不同版本及更新时间。
* **注意**：默认访问的 URL（不带版本号）始终指向**最新版本**。如果引用特定历史版本，需使用 `[https://arxiv.org/abs/2301.12345v1](https://arxiv.org/abs/2301.12345v1)`。

#### 2. 获取 LaTeX 源码与数据

在摘要页右侧的 **`Download`** 板块：

* **`Other formats`** -> **`Download source`**：如果作者开放了源码，你可以直接下载论文的原始 `.tex` 源码、图片及数据包，非常适合学习高质量的学术排版与模板。

#### 3. 复制 BibTeX 引用

* 在摘要页右侧工具栏中，点击 **`CiteAs`** 或利用页面自带的 **`BibTeX`** 导出功能，可直接获取标准的学术引用格式。

---

### 三、 作者指南：如何在 arXiv 上发表论文

如果你是研究者，想要上传自己的研究成果，基本流程如下：

1. **注册账号与背书（Endorsement）:** 首次提交需完成认证.
注册账号并填写学术机构邮箱（如 `.edu` 邮箱可自动获得部分权限）。如果是首次向某个特定学科分类（如 cs.CV）提交论文，系统可能会要求你提供已有资深作者的背书码（Endorsement Code）。


2. **准备论文文件:** 严格遵循格式要求.
建议使用 LaTeX 进行排版，将所有的 `.tex` 文件、图片格式（`.png`/`.pdf`）打包为单个 `.zip` 或 `.tar.gz` 压缩包。部分分类也支持直接上传 PDF，但 LaTeX 编译方式更受推荐。


3. **提交并填写元数据 (Metadata):** 校对论文摘要与作者信息.
在账号后台点击 Start New Submission，上传压缩包。系统会自动编译生成 PDF 预览。随后填写论文标题、摘要、作者列表、学科分类及项目开源链接等。


4. **审核与公开发布:** 注意提交时间节点.
提交后论文会进入人工/自动预审核（Moderation）阶段。根据东部时间（ET）每日的截止时间点，通过审核的论文会在指定时间统一向全球公开发布并分配唯一的 arXiv ID。


---

### 四、 常用第三方拓展与神器（推荐）

直接使用 arXiv 官方界面有时不够直观，社区开发了许多优质拓展工具：

1. **arXiv Xplorer / Connected Papers**：通过论文引用图谱，可视化查找相关研究领域的前沿论文。
2. **Arxiv-vanity / Arxiv-latex-cleaner**：将 arXiv PDF 自动转化为优雅排版的网页，支持移动端舒适阅读。
3. **沉浸式翻译 / ReadPaper**：搭配浏览器插件，可实现 arXiv 论文摘要及 PDF 全文的双语对照翻译。
4. **Hugging Face Papers**：查看 AI/计算机领域每天在 arXiv 上讨论度最高的热门论文。
