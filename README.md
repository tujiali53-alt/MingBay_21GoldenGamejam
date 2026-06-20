# MingBay 21 Golden Game Jam

这是一个使用 Unity `6000.3.6f1` 制作的 Game Jam 项目。

本文档面向第一次参加多人 Git 项目的成员，介绍本项目的分支用途和日常操作。命令需要在项目目录 `MW 72h` 中执行。

## 一、项目分支

| 分支 | 用途 | 谁主要使用 |
| --- | --- | --- |
| `main` | 稳定版本，只接收已经测试通过的内容 | 项目负责人 |
| `develop` | 日常开发整合分支 | 程序、策划 |
| `art` | 美术资源制作与导入 | 美术 |
| `test` | 集成测试和问题修复验证 | 测试人员 |

不要直接在 `main` 上开发。普通功能应先进入 `develop`，测试通过后再合并到 `main`。

## 二、第一次使用 Git

### 1. 配置用户名和邮箱

每台电脑只需配置一次：

```bash
git config --global user.name "你的名字"
git config --global user.email "你的GitHub邮箱"
```

### 2. 下载项目

```bash
git clone https://github.com/tujiali53-alt/MingBay_21GoldenGamejam.git
cd MingBay_21GoldenGamejam/"MW 72h"
```

必须使用 Unity `6000.3.6f1` 打开项目，避免 Unity 自动升级项目文件和资源格式。

### 3. 查看分支

```bash
git fetch --all
git branch -a
```

切换到需要工作的分支：

```bash
git switch develop
```

如果本地还没有该分支，但远程已有：

```bash
git switch --track origin/develop
```

## 三、推荐的日常开发流程

开始工作前，先同步最新内容：

```bash
git switch develop
git pull --ff-only origin develop
```

为自己的功能创建独立分支：

```bash
git switch -c feature/player-movement
```

完成一小段可以正常运行的工作后：

```bash
git status
git add Assets/Scripts
git commit -m "实现玩家移动"
git push -u origin feature/player-movement
```

然后在 GitHub 上创建 Pull Request，将功能分支合并到 `develop`。合并完成后可以删除功能分支：

```bash
git switch develop
git pull --ff-only origin develop
git branch -d feature/player-movement
```

## 四、美术资源工作流程

美术成员通常在 `art` 分支工作：

```bash
git switch art
git pull --ff-only origin art
```

将资源放入 `Assets/Art Assets` 或约定的资源目录。提交前确认资源文件及其 `.meta` 文件都被加入：

```bash
git status
git add "Assets/Art Assets"
git commit -m "添加角色行走动画"
git push origin art
```

美术资源完成并确认可用后，通过 Pull Request 合并到 `develop`。

## 五、测试和发布流程

建议按以下顺序流转：

```text
功能分支 / art
        ↓
     develop
        ↓
       test
        ↓
       main
```

- `develop`：整合程序和美术内容。
- `test`：验证场景、UI、输入、存档和构建是否正常。
- `main`：只保留可以正常演示或发布的版本。

不要为了赶时间跳过测试直接把未验证内容推入 `main`。

## 六、Unity 项目的重要规则

### 必须提交

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- 所有资源对应的 `.meta` 文件

### 不要提交

- `Library/`
- `Logs/`
- `Temp/`
- `UserSettings/`
- `.sln`、`.csproj`

这些文件由 Unity 或 IDE 自动生成，项目中的 `.gitignore` 已经配置了对应规则。

### 多人协作注意事项

1. 移动或重命名资源时，尽量在 Unity 编辑器内操作。
2. 不要手动删除 `.meta` 文件，否则资源 GUID 会改变，场景引用可能丢失。
3. 同一时间尽量不要让多人修改同一个 `.unity` 场景或 `.prefab`。
4. 提交前保存场景，退出播放模式，并检查 Console 是否存在报错。
5. 提交要小而清晰，不要把多个无关功能塞进同一个 commit。

## 七、查看当前状态

```bash
git status
git branch --show-current
git log --oneline -10
```

`git status` 是最安全也最常用的检查命令。看不懂当前状态时，先执行它，不要急着删除文件或使用强制命令。

## 八、撤销常见误操作

撤销尚未暂存的文件修改：

```bash
git restore 文件路径
```

取消暂存，但保留文件修改：

```bash
git restore --staged 文件路径
```

安全地撤销一个已经提交并推送的 commit：

```bash
git revert commit编号
git push
```

不要随意使用 `git reset --hard` 或强制推送，它们可能删除自己或队友的工作。

## 九、发生冲突怎么办

先查看冲突文件：

```bash
git status
```

普通文本或 C# 文件可以手动处理冲突标记：

```text
<<<<<<<
你的内容
=======
另一边的内容
>>>>>>>
```

保留正确内容并删除这些标记，然后：

```bash
git add 冲突文件
git commit
```

如果 `.unity`、`.prefab` 或 `.meta` 文件发生复杂冲突，不要凭感觉删除内容。先停止合并：

```bash
git merge --abort
```

然后联系对应文件的修改者，共同决定保留哪个版本。

## 十、提交信息建议

提交信息应说明“做了什么”，例如：

```text
实现玩家移动
添加主菜单界面
修复敌人重复生成
导入第一关背景资源
调整游戏音量默认值
```

避免使用“修改一下”“更新”“test”等无法说明内容的信息。

## 十一、遇到问题时提供这些信息

向队友求助时，请附上以下命令的完整输出：

```bash
git status
git branch --show-current
git log --oneline -5
```

不要提交或发送账号密码、访问令牌以及其他敏感信息。
