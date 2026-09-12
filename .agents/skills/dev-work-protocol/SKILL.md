---
name: dev-work-protocol
description: >-
  Reusable development work protocol: classify the turn, then apply process
  verification, structure verification, or execution-only; track 漏洞/功能 as
  V_/F_ work units; explain code by structural-change level; bound comments;
  own docs and temporary drawio. Use when the user discusses 开发策略, 代码结构,
  漏洞, 功能, 待处理事务, issues a 开发/开工/实现 command, or asks 代码的具体情况. After
  classifying, load only the matching reference. Do not use for git-only work,
  asset import, or unrelated tooling.
---





# 开发工作协议

可复用方法论。本仓库只提供原则对照；不要把专题算法文档写进本 skill。

分类仍待与用户核对。不确定时先问一句，再动手。

## 1. 先路由，再读文件

每轮先判对话类型，**只读对应 reference**。不要一次读完本目录。


| 对话类型 | 判定 | 本轮只读 |
|----------|------|----------|
| 开发策略 / 询问代码结构 | 在谈分层、边界、该不该做、现有结构是什么 | [references/design-principles.md](references/design-principles.md) |
| 功能与漏洞安排 | 出现漏洞或增减功能：当前是什么问题、怎么编号分类 | [references/work-tracking.md](references/work-tracking.md)；具体问题落到文档时叠加 [references/docs-and-artifacts.md](references/docs-and-artifacts.md) |
| 已明确开工 | 用户要求实现、修改、补齐、接入、按范围开发 | [references/start-modes.md](references/start-modes.md)，写代码时再读 [references/implementation.md](references/implementation.md) |
| 询问代码的具体情况 | 在问这段代码做什么、从哪看、和开工前如何关联 | [references/code-explain.md](references/code-explain.md) |
| 文档归属 / 临时图 | 在谈文档放哪、drawio、解释图、事务表要不要留 | [references/docs-and-artifacts.md](references/docs-and-artifacts.md) |


可叠加，但仍按需读取。例如「结构验证开工」：先 `start-modes`，写代码时 `implementation`；只有用户要看结构或改动太大时，再读 `code-explain` 和 `docs-and-artifacts`。

## 2. 触发与分层

- **开发策略 / 代码结构**：读原则对照，用现有分层回答，不展开专题实现。
- **功能与漏洞安排**：只回答当前是什么问题、怎么分类；具体问题落到文档，不写进 skill。
- **开发命令**：必须先分成开工三类之一，再写代码。落不进三类则先核对需求，不准开工。
- **代码具体情况**：用代码或图画回答，文本只做指引。

未点名本 skill、也不是上表类型时，不要为了「可能有用」而加载本协议。

## 3. 明确开工时：先分类

按照用户对 **AI 即将书写的代码** 的了解程度，分成 3 种。细则见 `start-modes.md`。

1. **流程验证** — 不在乎代码怎么写，只看是否越界、流程是否跑通。
2. **结构验证** — 知道缺口或需求，不清楚代码如何安排；用代码（或 drawio）回答结构。
3. **执行落地** — 需求和实现已基本确认；AI 只做基本执行，完工后只讲超出用户思路的结构，或纠正用户判断。

用户没标明时：用一句话问是哪一类。能从指令直接对上例子则不必问。

若不属于上述情况，例如用户说明确开工，但对当前结构几乎不明白，且还要求通读开工后代码：不要写代码，返回需求核对，进一步收敛，直到属于三种分类之一。细则见 `start-modes.md`。

## 4. 全程硬规则

- 先划范围，再改代码。范围外的重构、重命名、顺手清理一律不做。
- 文本回复保持短。能指到符号、文件、图，就不要复述实现。
- 结构变动等级按 `code-explain.md`：数字越大结构越大；大等级默认包含小等级。结构变动不等于代码量。
- 解释图是临时产物，目录由第 6 节用户填写框决定；未填则先问，不要猜路径。见 `docs-and-artifacts.md`。
- 专题正文以第 6 节填的文档为准。编号规则在 skill 里，具体路径只允许出现在填写框，不要写进其它 reference。

## 5. 用户忽略代码范围

只在 [references/implementation.md](references/implementation.md) 第 1 节维护，避免两处不一致。流程验证且该块仍为空时，整次改动都按「忽略代码」处理。

## 6. 本仓库文档入口

（留好范围，用户在此处修改）

未填的项：先问用户，不要根据常见目录名猜测，也不要把猜到的路径写回其它 skill 文件。

```text
文档索引：
Docs/README.md

仓库总览：
README.md

事务表（漏洞与功能安排）：
Docs/漏洞与功能安排.md

事务评估目录（按问题编号的具体文档）：
Docs/事务具体安排与评估/

结构图（可选）：
Docs/物理框架结构图.drawio

临时解释目录（可选）：

```