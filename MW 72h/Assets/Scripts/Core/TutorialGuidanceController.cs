using System;
using System.Collections;
using MingBay.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MingBay.Core
{
    /// <summary>
    /// 教程关引导控制器。
    /// 金色高亮框指示操作目标 + MentorBubble 显示动态对话框文字。
    /// 高亮目标出现后禁用场景中所有可交互组件，仅保留高亮目标子树和对话框可交互。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("明湾/核心/教程引导控制器")]
    public sealed class TutorialGuidanceController : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Image highlightImage;
        [SerializeField] private GameObject mentorBubble;
        [SerializeField] private TMP_Text mentorBubbleText;
        [SerializeField] private TMP_Text guidanceText;

        [Header("游戏对象引用")]
        [SerializeField] private Level1GameView mainGameView;
        [SerializeField] private Level1GameFlowManager gameFlowManager;

        // 运行时状态
        private bool isTutorialStage;
        private int tutorialTicketIndex;
        private string activeTicketId = string.Empty;
        private int currentStepIndex = -1;
        private TutorialStep[] currentSteps = Array.Empty<TutorialStep>();
        private int followUpClickCount;
        private int selectedEvidenceIndex = -1;
        private bool guidanceActive;
        private bool waitingForDialogClick;
        private bool waitingForAnyKey;

        // Outline 高亮：直接复用项目中绿色高亮的实现手法（Unity UI Outline 组件）
        private Outline currentHighlightOutline;
        private static readonly Color HighlightColor = new(1f, 0.82f, 0.1f, 1f); // 金色

        // 证据卡片名（按索引对应 EvidenceLibraryPanel 中的卡片顺序）
        private static readonly string[] EvidenceCardNames =
            { "EvidenceCard_Profile", "EvidenceCard_History", "EvidenceCard_Device", "EvidenceCard_Region" };

        // ── 交互禁用系统 ──
        // 教程步骤中禁用高亮目标区域外的所有 Selectable（Button/Toggle/Slider 等），
        // 仅保留 MentorBubble 和高亮目标子树的可交互性。
        private readonly System.Collections.Generic.HashSet<Selectable> disabledSelectables = new();

        // MentorBubble 原始父节点（用于教程结束后恢复位置）
        private Transform mentorBubbleOriginalParent;
        private int mentorBubbleOriginalSiblingIndex;

        // ── 按钮监听追踪系统 ──
        // 记录所有动态添加的按钮点击监听，确保在 EndAllGuidance() 或组件销毁时能够清理。
        // 防止协程中断时监听残留导致后续逻辑错误。
        private struct ButtonListenerEntry
        {
            public Button button;
            public UnityEngine.Events.UnityAction handler;
        }
        private readonly System.Collections.Generic.List<ButtonListenerEntry> registeredButtonListeners = new();

        private const string DefaultMentorHint = "先点击工单APP，查看今日任务";

        [Serializable]
        public struct TutorialStep
        {
            public string stepId;
            public string dialogText;         // MentorBubble 显示的文字
            public string targetName;         // 高亮目标对象名（空=隐藏高亮框）
            public bool waitForDialogClick;   // 等待点击对话框推进
            public bool waitForAnyKey;        // 等待按任意键推进
        }

        // ── 生命周期 ──

        private void OnEnable()
        {
            if (gameFlowManager != null) gameFlowManager.StageChanged += OnStageChanged;
            if (mainGameView != null)
            {
                mainGameView.TicketSelected += OnTicketSelected;
                mainGameView.ViewDataRequested += OnViewDataRequested;
                mainGameView.FollowUpRequested += OnFollowUpRequested;
                mainGameView.EvidenceSelected += OnEvidenceSelected;
                mainGameView.SaveEvidenceRequested += OnSaveEvidenceRequested;
                mainGameView.TransferHumanRequested += OnTransferHumanRequested;
                mainGameView.ChatEvidenceActionRequested += OnSubmitEvidence;
                mainGameView.MarkResolvedRequested += OnMarkResolvedRequested;
                mainGameView.ResultActionRequested += OnResultActionRequested;
            }
        }

        private void OnDisable()
        {
            // 组件销毁或禁用时，确保清理所有教程资源
            EndAllGuidance();

            if (gameFlowManager != null) gameFlowManager.StageChanged -= OnStageChanged;
            if (mainGameView != null)
            {
                mainGameView.TicketSelected -= OnTicketSelected;
                mainGameView.ViewDataRequested -= OnViewDataRequested;
                mainGameView.FollowUpRequested -= OnFollowUpRequested;
                mainGameView.EvidenceSelected -= OnEvidenceSelected;
                mainGameView.SaveEvidenceRequested -= OnSaveEvidenceRequested;
                mainGameView.TransferHumanRequested -= OnTransferHumanRequested;
                mainGameView.ChatEvidenceActionRequested -= OnSubmitEvidence;
                mainGameView.MarkResolvedRequested -= OnMarkResolvedRequested;
                mainGameView.ResultActionRequested -= OnResultActionRequested;
            }
        }

        private void Start()
        {
            HideHighlight();
            if (mentorBubble != null && mentorBubble.TryGetComponent(out Button bubbleBtn))
                bubbleBtn.onClick.AddListener(OnDialogClicked);
        }

        private void Update()
        {
            if (waitingForAnyKey && Input.anyKeyDown)
            {
                waitingForAnyKey = false;
                AdvanceStep();
            }

        }

        // ── 阶段切换 ──

        private void OnStageChanged(string stageId)
        {
            bool isTutorial =
                string.Equals(stageId, "Stage_Tutorial", StringComparison.Ordinal) ||
                string.Equals(stageId, "TUTORIAL", StringComparison.Ordinal);
            isTutorialStage = isTutorial;
            if (isTutorial)
            {
                tutorialTicketIndex = 0;
                StartCoroutine(BeginTutorialStage());
            }
            else
            {
                EndAllGuidance();
            }
        }

        private IEnumerator BeginTutorialStage()
        {
            yield return null;
            // Phase 0: 桌面，关闭自动打开的工单窗口
            GameObject appWindow = GameObject.Find("TicketAppWindow");
            if (appWindow != null) appWindow.SetActive(false);

            RestoreMentorBubbleText();
            if (mentorBubble != null)
            {
                // 保存原始位置，然后移到 Canvas 最上层确保教程对话框不被遮挡
                mentorBubbleOriginalParent = mentorBubble.transform.parent;
                mentorBubbleOriginalSiblingIndex = mentorBubble.transform.GetSiblingIndex();
                Canvas canvas = mentorBubble.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    mentorBubble.transform.SetParent(canvas.transform, true);
                    mentorBubble.transform.SetAsLastSibling();
                }
                mentorBubble.SetActive(true);
            }
            HideHighlight();

            // 等待用户点击"工单队列"或"工单APP"
            bool clicked = false;
            UnityEngine.Events.UnityAction handler = null;
            handler = () => { clicked = true; };

            string[] entryButtonNames = { "Taskbar_WorkQueue", "DesktopButton_WorkApp" };
            foreach (string btnName in entryButtonNames)
            {
                Transform t = FindGlobal(btnName);
                if (t != null && t.TryGetComponent(out Button btn))
                {
                    btn.onClick.AddListener(handler);
                    registeredButtonListeners.Add(new ButtonListenerEntry { button = btn, handler = handler });
                }
            }

            while (!clicked) yield return null;

            // 清理监听（同时也会在 EndAllGuidance 中清理，但这里先清理避免重复）
            foreach (string btnName in entryButtonNames)
            {
                Transform t = FindGlobal(btnName);
                if (t != null && t.TryGetComponent(out Button btn))
                    btn.onClick.RemoveListener(handler);
            }
            // 从追踪列表中移除已清理的监听
            registeredButtonListeners.RemoveAll(entry => entry.button != null && entry.handler == handler);

            // Phase 1: 工单界面，高亮第一个队列项
            yield return null;
            string firstItem = FindFirstQueueItemName();
            Debug.Log($"[TutorialGuidance] Phase 1: 第一个队列项='{firstItem ?? "未找到"}'");
            PositionHighlightOn(firstItem);
            SetDialog("先来看看第一位居民的诉求", false, false);
        }

        private IEnumerator BeginTicket2QueueGuidance()
        {
            yield return null;
            string secondItem = FindNthQueueItemName(1);
            Debug.Log($"[TutorialGuidance] 工单2队列: 队列项='{secondItem ?? "未找到"}'");
            PositionHighlightOn(secondItem);
            SetDialog("接下来我会教你一个独门秘籍，查看第二位居民的诉求", false, false);
        }

        // ── 工单选中 ──

        private void OnTicketSelected(int ticketIndex)
        {
            if (!isTutorialStage) return;
            if (tutorialTicketIndex == 0 && ticketIndex == 0)
                StartGuidance("T_S01_001", BuildTicket1Steps());
            else if (tutorialTicketIndex == 1 && ticketIndex == 1)
                StartGuidance("T_S01_002", BuildTicket2Steps());
        }

        private void StartGuidance(string ticketId, TutorialStep[] steps)
        {
            activeTicketId = ticketId;
            currentSteps = steps ?? Array.Empty<TutorialStep>();
            currentStepIndex = 0;
            followUpClickCount = 0;
            guidanceActive = true;
            if (currentSteps.Length > 0) ConfigureCurrentStep();
        }

        private void EndAllGuidance()
        {
            // 停止所有正在运行的协程，防止协程继续执行导致清理后的状态被重新修改
            StopAllCoroutines();

            activeTicketId = string.Empty;
            currentStepIndex = -1;
            currentSteps = Array.Empty<TutorialStep>();
            followUpClickCount = 0;
            guidanceActive = false;
            waitingForDialogClick = false;
            waitingForAnyKey = false;

            // 清理所有动态添加的按钮监听（修复 Issue1：协程中断时监听残留）
            foreach (ButtonListenerEntry entry in registeredButtonListeners)
            {
                if (entry.button != null && entry.handler != null)
                    entry.button.onClick.RemoveListener(entry.handler);
            }
            registeredButtonListeners.Clear();

            // 恢复所有被禁用的交互组件（修复 Issue2：退出教程后组件保持禁用）
            RestoreSelectables();

            // 恢复 MentorBubble 原始位置
            RestoreMentorBubblePosition();

            HideHighlight();
            HideMentorBubble();
        }

        private void RestoreMentorBubblePosition()
        {
            if (mentorBubble != null && mentorBubbleOriginalParent != null)
            {
                mentorBubble.transform.SetParent(mentorBubbleOriginalParent, true);
                mentorBubble.transform.SetSiblingIndex(
                    Mathf.Clamp(mentorBubbleOriginalSiblingIndex, 0,
                        mentorBubbleOriginalParent.childCount - 1));
                mentorBubbleOriginalParent = null;
            }
        }

        private void EndGuidance()
        {
            activeTicketId = string.Empty;
            currentStepIndex = -1;
            currentSteps = Array.Empty<TutorialStep>();
            followUpClickCount = 0;
            guidanceActive = false;
            waitingForDialogClick = false;
            waitingForAnyKey = false;
            tutorialTicketIndex++;

            if (tutorialTicketIndex == 1)
                StartCoroutine(BeginTicket2QueueGuidance());
            else
            {
                EndAllGuidance();
                StartCoroutine(ReturnToLevel1NextFrame());
            }
        }

        /// <summary>
        /// 工单2结算完成 → 用户关闭结果面板 → 等待一帧后返回第一关界面。
        /// </summary>
        private IEnumerator ReturnToLevel1NextFrame()
        {
            yield return null; // 等待一帧，确保结果面板关闭动画完成
            SceneManager.LoadScene("Level1Scene");
        }

        // ── 步骤管理 ──

        private void ConfigureCurrentStep()
        {
            if (currentStepIndex < 0 || currentStepIndex >= currentSteps.Length)
            { EndGuidance(); return; }
            ShowGuidanceForStep(currentSteps[currentStepIndex]);
        }

        private void AdvanceStep()
        {
            currentStepIndex++;
            if (currentStepIndex >= currentSteps.Length)
            { EndGuidance(); return; }
            ConfigureCurrentStep();
        }

        private string GetCurrentStepId() =>
            currentStepIndex >= 0 && currentStepIndex < currentSteps.Length
                ? currentSteps[currentStepIndex].stepId : string.Empty;

        // ── UI 控制 ──

        private void ShowGuidanceForStep(TutorialStep step)
        {
            SetDialog(step.dialogText, step.waitForDialogClick, step.waitForAnyKey);

            // 动态目标名：card_selected 步骤高亮用户实际点击的卡片
            string target = step.targetName;
            if (step.stepId == "card_selected")
                target = GetSelectedCardName();

            if (!string.IsNullOrEmpty(target))
            {
                // card_selected 步骤：Outline 挂在当前卡片上，但禁用根用面板级保留所有卡片可切换
                string disableRoot = step.stepId == "card_selected" ? "EvidenceLibraryPanel" : null;
                PositionHighlightOn(target, disableRoot);
            }
            else
            {
                HideHighlight();
                // 无高亮目标但有等待条件时，仅保留对话框可交互，禁用其余所有 Selectable
                if (step.waitForDialogClick || step.waitForAnyKey)
                    DisableSelectablesExcept(null);
            }

            if (guidanceText != null)
                guidanceText.text = step.waitForDialogClick ? "— 点击对话框继续 —" :
                                    step.waitForAnyKey ? "— 按任意键继续 —" : "";
        }

        private void SetDialog(string text, bool waitForClick, bool waitForKey)
        {
            waitingForDialogClick = waitForClick;
            waitingForAnyKey = waitForKey;
            if (!string.IsNullOrEmpty(text) && mentorBubbleText != null)
            {
                mentorBubbleText.text = text;
                mentorBubbleText.ForceMeshUpdate();
            }
            if (waitForKey)
                StartCoroutine(SkipFirstFrameAnyKey());
        }

        // 避免触发步骤的按键本身被 anyKey 检测到
        private IEnumerator SkipFirstFrameAnyKey()
        {
            bool wasWaiting = waitingForAnyKey;
            waitingForAnyKey = false;
            yield return null;
            waitingForAnyKey = wasWaiting;
        }

        private void HideHighlight()
        {
            RemoveHighlightOutline();
            RestoreSelectables();
            if (guidanceText != null) guidanceText.text = "";
        }

        private void RestoreMentorBubbleText()
        {
            if (mentorBubbleText != null) mentorBubbleText.text = DefaultMentorHint;
        }

        private void HideMentorBubble()
        {
            if (mentorBubble != null) mentorBubble.SetActive(false);
        }

        // ── 交互禁用系统 ──

        /// <summary>
        /// 禁用场景中所有可交互的 Selectable（Button/Toggle/Slider/Scrollbar 等），
        /// 仅保留 <paramref name="exceptRoot"/> 子树（高亮目标区域）和 MentorBubble 对话框可交互。
        /// 传入 null 表示仅保留 MentorBubble。
        /// </summary>
        private void DisableSelectablesExcept(Transform exceptRoot)
        {
            RestoreSelectables(); // 先清除上一轮的禁用记录

            Selectable[] all = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Selectable s in all)
            {
                if (!s.interactable) continue;

                // 始终保留 MentorBubble 内的交互组件
                if (mentorBubble != null && s.transform.IsChildOf(mentorBubble.transform)) continue;

                // 保留高亮目标及其子级内的交互组件
                if (exceptRoot != null && (s.transform == exceptRoot || s.transform.IsChildOf(exceptRoot))) continue;

                s.interactable = false;
                disabledSelectables.Add(s);
            }

            Debug.Log($"[TutorialGuidance] 已禁用 {disabledSelectables.Count} 个 Selectable，保留目标='{(exceptRoot != null ? exceptRoot.name : "仅对话框")}'");
        }

        /// <summary>
        /// 恢复所有被 DisableSelectablesExcept 禁用的 Selectable。
        /// 多次调用安全。
        /// </summary>
        private void RestoreSelectables()
        {
            foreach (Selectable s in disabledSelectables)
            {
                if (s != null) s.interactable = true;
            }
            disabledSelectables.Clear();
        }

        // ── 高亮（Outline 组件，复用绿框实现手法）──

        /// <summary>
        /// 在目标对象上启用金色 Outline 高亮。
        /// 直接复用证据面板绿色高亮的实现手法：Unity UI Outline 组件 enable/disable。
        /// Outline 优先挂在带 Graphic 组件的对象上；若目标是纯容器面板，则在子级 Button 中查找。
        /// 同时禁用区域外的所有 Selectable——默认以高亮目标为禁用根，
        /// 传入 <paramref name="disableRootName"/> 可指定更宽的作用域（如卡片切换步骤需保留面板内所有卡片）。
        /// </summary>
        private void PositionHighlightOn(string targetName, string disableRootName = null)
        {
            RemoveHighlightOutline();
            if (string.IsNullOrEmpty(targetName)) return;

            Transform found = FindGlobal(targetName);
            if (found == null)
            {
                Debug.LogWarning($"[TutorialGuidance] 未找到目标: '{targetName}'");
                return;
            }

            Transform highlightTarget = found;
            if (!found.TryGetComponent(out Button _) && found.parent != null && found.parent.name == targetName)
                highlightTarget = found.parent;

            // 确定禁用根：显式指定 > 高亮目标自身
            Transform disableRoot = highlightTarget;
            if (!string.IsNullOrEmpty(disableRootName))
            {
                Transform specified = FindGlobal(disableRootName);
                if (specified != null) disableRoot = specified;
            }
            DisableSelectablesExcept(disableRoot);

            // Outline 显示：优先自身有 Graphic，否则找子级第一个带 Graphic 的 Selectable
            Transform outlineTarget = FindOutlineTarget(highlightTarget);
            if (outlineTarget != null)
            {
                Outline outline = outlineTarget.GetComponent<Outline>();
                if (outline == null) outline = outlineTarget.gameObject.AddComponent<Outline>();
                outline.effectColor = HighlightColor;
                outline.effectDistance = new Vector2(5f, -5f);
                outline.enabled = true;
                currentHighlightOutline = outline;
                Debug.Log($"[TutorialGuidance] Outline高亮: '{targetName}' → Outline挂在'{outlineTarget.name}' (禁用根='{disableRoot.name}')");
            }
            else
            {
                Debug.Log($"[TutorialGuidance] 禁用完成: '{targetName}' → 禁用根='{disableRoot.name}' (无 Graphic 可挂 Outline)");
            }
        }

        /// <summary>
        /// 为 Outline 找到合适的挂载对象。
        /// 规则：自身是 Selectable（Button 等可交互元素）+ 有 Graphic → 挂在自身。
        /// 自身只有 Graphic 无 Selectable（纯装饰背景面板）→ 跳过，找子级带 Graphic 的 Selectable。
        /// 都没有 → 返回 null，不显示 Outline。
        /// </summary>
        private static Transform FindOutlineTarget(Transform root)
        {
            // 自身是可交互元素 → 直接挂 Outline
            if (root.TryGetComponent(out Selectable _) && root.TryGetComponent(out Graphic _))
                return root;

            // 自身是纯装饰/背景 → 不挂，在子级可交互元素中查找
            Selectable[] children = root.GetComponentsInChildren<Selectable>();
            foreach (Selectable s in children)
            {
                if (s.TryGetComponent(out Graphic _))
                    return s.transform;
            }

            // 回退：自身有 Graphic 但无 Selectable，且没有子级 Selectable
            if (root.TryGetComponent(out Graphic _))
                return root;

            return null;
        }

        private void RemoveHighlightOutline()
        {
            if (currentHighlightOutline != null)
            {
                currentHighlightOutline.enabled = false;
                currentHighlightOutline = null;
            }
        }

        // ── 全局查找 ──

        private static Transform FindGlobal(string name)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
                if (t.name == name) return t;
            return null;
        }

        private static string FindFirstQueueItemName()
        {
            var names = new System.Collections.Generic.List<string>();
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
                if (t.name.StartsWith("QueueItem_"))
                    names.Add(t.name);
            names.Sort();
            return names.Count > 0 ? names[0] : null;
        }

        private string GetSelectedCardName() =>
            selectedEvidenceIndex >= 0 && selectedEvidenceIndex < EvidenceCardNames.Length
                ? EvidenceCardNames[selectedEvidenceIndex] : null;

        private static string FindNthQueueItemName(int n)
        {
            var names = new System.Collections.Generic.List<string>();
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
                if (t.name.StartsWith("QueueItem_"))
                    names.Add(t.name);
            names.Sort();
            return n < names.Count ? names[n] : null;
        }
        // ── 对话框点击 ──

        public void OnDialogClicked()
        {
            if (waitingForDialogClick)
            {
                waitingForDialogClick = false;
                AdvanceStep();
            }
        }

        // ── 事件处理 ──

        private void OnViewDataRequested()       { if (guidanceActive && !waitingForDialogClick && !waitingForAnyKey) TryAdvance("view_data"); }
        private void OnEvidenceSelected(int index)
        {
            if (!guidanceActive || waitingForDialogClick || waitingForAnyKey) return;
            selectedEvidenceIndex = index;
            string cardName = GetSelectedCardName();
            Debug.Log($"[TutorialGuidance] 选中证据卡片 index={index}, 名称={cardName}");

            string sid = GetCurrentStepId();
            if (sid == "evidence_select")
            {
                // 首次选中 → 进入卡片高亮步骤
                AdvanceStep();
            }
            else if (sid == "card_selected")
            {
                // 切换到另一张卡片 → 高亮框跟随新卡片，禁用根仍为面板
                PositionHighlightOn(cardName, "EvidenceLibraryPanel");
            }
        }
        private void OnSaveEvidenceRequested()
        {
            if (!guidanceActive || waitingForDialogClick || waitingForAnyKey) return;
            // 用户在卡片高亮步骤点击"收集此资料" → 推进到弹窗步骤
            if (GetCurrentStepId() == "card_selected") AdvanceStep();
        }
        private void OnTransferHumanRequested()  { if (guidanceActive && !waitingForDialogClick && !waitingForAnyKey) TryAdvance("transfer"); }
        private void OnSubmitEvidence()          { if (guidanceActive && !waitingForDialogClick && !waitingForAnyKey) TryAdvance("submit"); }

        private void OnFollowUpRequested()
        {
            if (!guidanceActive || waitingForDialogClick || waitingForAnyKey) return;
            if (GetCurrentStepId() == "follow_up")
            {
                followUpClickCount++;
                // T_S01_001: 1次追问; T_S01_002: 3次追问（maxAskCount）
                int maxClicks = activeTicketId == "T_S01_002" ? 3 : 1;
                Debug.Log($"[TutorialGuidance] 追问计数: {followUpClickCount}/{maxClicks}");
                if (followUpClickCount >= maxClicks)
                    AdvanceStep();
            }
            else TryAdvance("follow_up");
        }

        private void OnMarkResolvedRequested()
        {
            if (!guidanceActive || waitingForDialogClick || waitingForAnyKey) return;
            TryAdvance("mark_resolved");
        }

        private void OnResultActionRequested()
        {
            if (!guidanceActive || waitingForDialogClick || waitingForAnyKey) return;
            TryAdvance("result_close");
        }

        private void TryAdvance(string eventId) { if (GetCurrentStepId() == eventId) AdvanceStep(); }

        // ── 步骤定义（严格匹配 教程关卡流程.md）──
        //
        //  高亮目标名说明（均为场景中实际对象名）：
        //    QueueItem_*          = 工单队列项（运行时动态查找）
        //    ChatPanel            = 聊天面板区域
        //    Btn_DataLookup       = "资料库查找"按钮
        //    EvidenceLibraryPanel = 资料展示区
        //    Btn_SaveEvidenceHidden = "收集此资料"按钮（隐藏版）
        //    Btn_FollowUp         = "追问"按钮
        //    Btn_TransferHuman    = "转人工"按钮
        //    Btn_NotebookSubmit   = 笔记面板"提交"按钮
        //    Btn_MarkResolved     = "标记已解决"按钮
        //
        // ★ 手动调整：在 Unity 中找到 TutorialHighlightFrame，
        //    拖拽 RectTransform 的 Pos X/Y（偏移）和 Width/Height（尺寸）

        private TutorialStep[] BuildTicket1Steps()
        {
            return new[]
            {
                // 文档L4: 用户点击第一个工单后，出现聊天面板及内容，除了聊天面板区域，其他都遮蔽无法点击
                new TutorialStep { stepId = "read_chat", dialogText = "查看聊天内容，浏览完毕后，点击对话框进入下一步", targetName = "ChatPanel", waitForDialogClick = true },
                // 文档L5: "资料库查找"按钮边框高亮
                new TutorialStep { stepId = "view_data", dialogText = "点击【资料库查找】查看更多资料", targetName = "Btn_DataLookup" },
                // 文档L5: 用户点击"资料库查找"后展示资料，资料展示区边框高亮
                new TutorialStep { stepId = "evidence_select", dialogText = "点击任一资料查看详情", targetName = "EvidenceLibraryPanel" },
                // 文档L5: 用户点击任意资料卡片高亮框选该资料卡片
                //        targetName 动态替换为实际点击的卡片名（EvidenceCard_Profile/History/Device/Region）
                new TutorialStep { stepId = "card_selected", dialogText = "现在尝试将资料收集进【证据手册】中", targetName = "" },
                // 文档L5: 玩家点击"收集此资料"，弹出提示"已添加进证据手册"
                new TutorialStep { stepId = "collect_evidence", dialogText = "已添加进证据手册，点击对话框继续", targetName = "", waitForDialogClick = true },
                // 文档L6: 对话框"点击【追问】了解居民更详细需求"，高亮"追问"按钮
                new TutorialStep { stepId = "follow_up", dialogText = "点击【追问】了解居民更详细需求", targetName = "Btn_FollowUp" },
                // 文档L6: 用户点击"追问"→聊天更新→对话框显示转人工提示，高亮"转人工"按钮
                new TutorialStep { stepId = "transfer", dialogText = "通过追问发现跟AI处理建议有不妥之处，我们可以点击【转人工】进行人工处理", targetName = "Btn_TransferHuman" },
                // 文档L6: 用户点击"转人工"→聊天更新→聊天区域高亮，"按任意键跳转到证据面板"
                new TutorialStep { stepId = "transfer_done", dialogText = "为了防止有人胡乱惹事，转人工后需要出示相关证据，根据资料为居民找到解决方法", targetName = "ChatPanel", waitForAnyKey = true },
                // 文档L6: 进入笔记面板，提交证据（高亮面板以保留证据卡片和提交按钮均可交互）
                new TutorialStep { stepId = "submit", dialogText = "请选择一份已收集的资料，点击提交证据", targetName = "NotebookPanel" },
                // 文档L6: 提交证据后直接进入结算，对话框显示结算文案，用户关闭ResultPanel后推进
                new TutorialStep { stepId = "result_close", dialogText = "太好了你现在已经掌握了如何处理一条工单，但也要注意风险值", targetName = "" },
            };
        }

        private TutorialStep[] BuildTicket2Steps()
        {
            return new[]
            {
                // 文档L8: 进入工单2后，引导点击追问
                new TutorialStep { stepId = "follow_up", dialogText = "点击【追问】查看居民完整诉求，可以发现不是每次都能完美解决诉求，有些居民可能态度不好或要求太高很难解决", targetName = "Btn_FollowUp" },
                // 所有追问展示完毕后，引导点击标记已解决
                new TutorialStep { stepId = "mark_resolved", dialogText = "你可以长按这个按钮【标记已解决】轻松解决诉求", targetName = "Btn_MarkResolved" },
                // 结算弹窗出现时显示道别文案，关闭弹窗后自动转场到N1
                new TutorialStep { stepId = "result_close", dialogText = "你已经完美胜任工作了！！接下来全靠你自己喽", targetName = "" },
            };
        }
    }
}
