using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MingBay.Core;
using MingBay.Data;
using MingBay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MingBay.Editor
{
    /// <summary>
    /// 基础工单队列场景生成工具。
    /// 所有美术位置仅使用 Unity 默认 Image、色块和文字占位，方便后续直接替换。
    /// </summary>
    public static class Level1SceneBuilder
    {
        private const string Level1ScenePath = "Assets/Scenes/Level1Scene.unity";
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string SpreadsheetConfigJsonPath =
            "Assets/Configs/Spreadsheet/MingBaySpreadsheetConfig.json";
        private const string CurrentSpreadsheetLevelId = "N1";
        private const string CurrentSpreadsheetLevelName = "第一夜";
        private static readonly string[] TicketAssetPaths =
        {
            "Assets/Configs/Tickets/Ticket_T_S01_001.asset",
            "Assets/Configs/Tickets/Ticket_T_S01_002.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_001.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_002.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_003.asset",
            "Assets/Configs/Tickets/Ticket_T_D01_004.asset"
        };
        private static readonly string[] LegacyTicketAssetPaths =
        {
            "Assets/Configs/Tickets/Ticket_T_001.asset",
            "Assets/Configs/Tickets/Ticket_T_002.asset",
            "Assets/Configs/Tickets/Ticket_T_003.asset",
            "Assets/Configs/Tickets/Ticket_T_004.asset"
        };
        private const string DatabaseAssetPath = "Assets/Configs/MingBayProjectDatabase.asset";
        private const string ChineseFontPath =
            "Assets/UI/Fonts/NotoSansSC-Regular SDF.asset";
        private const string ChineseSourceFontPath =
            "Assets/UI/Fonts/NotoSansSC-Regular.ttf";

        // 使用中性灰阶作为默认占位配色，后续美术可直接替换对应 Image。
        private static readonly Color Background = Hex("202020");
        private static readonly Color TopBar = Hex("282828");
        private static readonly Color Panel = Hex("353535");
        private static readonly Color PanelRaised = Hex("4A4A4A");
        private static readonly Color PanelDark = Hex("2A2A2A");
        private static readonly Color Accent = Hex("808080");
        private static readonly Color AccentHover = Hex("A0A0A0");
        private static readonly Color Warning = Hex("969696");
        private static readonly Color PrimaryText = Hex("EFEFEF");
        private static readonly Color MutedText = Hex("A8A8A8");
        private static readonly Color Border = new(0.55f, 0.55f, 0.55f, 0.28f);

        /// <summary>
        /// 重新创建测试工单、Demo 数据库和 Level1Scene。
        /// 注意：执行后会覆盖当前 Level1Scene。
        /// </summary>
        [MenuItem("明湾/Level1/场景工具/生成基础工单 Demo")]
        public static void Build()
        {
            EnsureFolder("Assets/Configs/Tickets");
            EnsureFolder("Assets/UI");
            MigrateLegacyTicketAssets();

            TMP_FontAsset font = EnsureChineseFont();
            TicketData[] allTickets = CreateOrUpdateTickets();
            TicketData[] tickets = Array.FindAll(
                allTickets,
                ticket => ticket != null && ticket.StageId != "Stage_Tutorial");
            MingBayProjectDatabase database = CreateOrUpdateDatabase(tickets);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera();
            CreateEventSystem();

            GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            CreateImage("Background", root, Hex("191919"), Vector2.zero, Vector2.one);
            CreateDesktopShell(
                root,
                font,
                out Button workAppButton,
                out Button clueNotebookButton,
                out Button taskbarWorkQueueButton,
                out Button taskbarDatabaseButton);
            RectTransform appWindow = CreateTicketAppWindow(
                root,
                font,
                out Button ticketAppCloseButton,
                out TMP_Text progressText,
                out TMP_Text statusText,
                out TMP_Text evidenceText,
                out TMP_Text resolvedText,
                out RectTransform queueContent,
                out Level1TicketQueueItemView queueItemTemplate,
                out TMP_Text titleText,
                out TMP_Text metaText,
                out TMP_Text ticketIdText,
                out GameObject dataPanel,
                out TMP_Text profileText,
                out TMP_Text historyText,
                out TMP_Text deviceLogText,
                out TMP_Text regionStatusText,
                out ScrollRect dataScrollRect,
                out ScrollRect chatScrollRect,
                out RectTransform chatContent,
                out GameObject chatBubbleTemplate,
                out GameObject chatEmptyState,
                out TMP_Text userMessageText,
                out TMP_Text aiReplyText,
                out Button primaryActionButton,
                out Button dataLookupButton,
                out Button transferHumanButton,
                out Button saveEvidenceButton,
                out Button chatEvidenceActionButton,
                out Button markResolvedButton,
                out Image markResolvedHoldFill);
            CreateNotebookPanel(
                root,
                font,
                out GameObject notebookPanel,
                out TMP_Text notebookReasonText,
                out TMP_Text notebookUserText,
                out TMP_Text notebookEmotionText,
                out TMP_Text notebookRegionText,
                out TMP_Text notebookTicketIdText,
                out Button[] notebookEvidenceButtons,
                out TMP_Text[] notebookEvidenceTexts,
                out Outline[] notebookEvidenceOutlines,
                out Button notebookCloseButton,
                out Button notebookCancelButton,
                out Button notebookSubmitButton);
            CreateResultPanel(root, font, out GameObject resultPanel, out TMP_Text resultTitleText,
                out TMP_Text resultStatusText, out TMP_Text resultDescriptionText, out TMP_Text resultMetricsText,
                out Button resultActionButton);
            CreateEvidenceDetailOverlay(
                root,
                font,
                out GameObject evidenceDetailOverlay,
                out TMP_Text evidenceDetailTitleText,
                out TMP_Text evidenceDetailBodyText,
                out Button evidenceDetailCloseButton);

            // 先保持对象关闭，避免 AddComponent 时触发尚未绑定引用的 OnEnable。
            GameObject viewObject = new("Level1GameView");
            viewObject.SetActive(false);
            Level1GameView view = viewObject.AddComponent<Level1GameView>();
            BindView(
                view,
                appWindow.gameObject,
                ticketAppCloseButton,
                workAppButton,
                clueNotebookButton,
                taskbarWorkQueueButton,
                taskbarDatabaseButton,
                queueContent,
                queueItemTemplate,
                new[]
                {
                    titleText.transform.parent.gameObject,
                    chatScrollRect.gameObject,
                    primaryActionButton.transform.parent.gameObject
                },
                progressText,
                statusText,
                evidenceText,
                resolvedText,
                titleText,
                metaText,
                ticketIdText,
                userMessageText,
                aiReplyText,
                chatScrollRect,
                chatContent,
                chatBubbleTemplate,
                chatEmptyState,
                dataPanel,
                dataScrollRect,
                profileText,
                historyText,
                deviceLogText,
                regionStatusText,
                evidenceDetailOverlay,
                evidenceDetailTitleText,
                evidenceDetailBodyText,
                evidenceDetailCloseButton,
                primaryActionButton,
                dataLookupButton,
                transferHumanButton,
                saveEvidenceButton,
                markResolvedButton,
                markResolvedHoldFill,
                chatEvidenceActionButton,
                notebookPanel,
                notebookReasonText,
                notebookUserText,
                notebookEmotionText,
                notebookRegionText,
                notebookTicketIdText,
                notebookEvidenceButtons,
                notebookEvidenceTexts,
                notebookEvidenceOutlines,
                notebookCloseButton,
                notebookCancelButton,
                notebookSubmitButton,
                resultPanel,
                resultTitleText,
                resultStatusText,
                resultDescriptionText,
                resultMetricsText,
                resultActionButton);
            viewObject.SetActive(true);

            GameObject systemsObject = new("GameSystems");
            systemsObject.SetActive(false);
            EvidenceManager evidenceManager = systemsObject.AddComponent<EvidenceManager>();
            MetricManager metricManager = systemsObject.AddComponent<MetricManager>();
            Level1GameFlowManager flowManager = systemsObject.AddComponent<Level1GameFlowManager>();
            BindFlowManager(flowManager, database, evidenceManager, metricManager, view);
            systemsObject.SetActive(true);

            // 保存前生成队列预览；正式运行时 Level1GameFlowManager 会清理并重新生成。
            List<TicketData> dayOneTickets = database.GetTicketsByStage(CurrentSpreadsheetLevelId);
            view.BuildTicketQueue(dayOneTickets);
            view.ShowTicketSelection(
                CurrentSpreadsheetLevelName,
                0,
                dayOneTickets.Count,
                new GameMetrics(0, 0, 0, 0, 0),
                new bool[dayOneTickets.Count]);
            appWindow.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, Level1ScenePath);
            UpdateBuildSettings();
            ValidateGeneratedContent(database, flowManager, view, queueItemTemplate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CapturePreview(canvas, camera);

            Debug.Log("Level1Scene 基础工单队列 Demo 生成成功。");
        }

        /// <summary>
        /// 检查生成后的核心数据与 Inspector 引用，避免场景可以保存但运行时因漏绑而报错。
        /// </summary>
        private static void ValidateGeneratedContent(
            MingBayProjectDatabase database,
            Level1GameFlowManager flowManager,
            Level1GameView view,
            Level1TicketQueueItemView queueItemTemplate)
        {
            if (database == null || database.TicketCount <= 0 || database.GetTicket(0) == null)
            {
                throw new InvalidOperationException("DemoDatabase 至少需要包含一张有效工单。");
            }

            List<TicketData> dayOneTickets = database.GetTicketsByStage(CurrentSpreadsheetLevelId);
            if (database.GetTicketsByStage("Stage_Tutorial").Count != 0 ||
                dayOneTickets.Count != 4)
            {
                throw new InvalidOperationException(
                    "当前流程应只包含 4 张第一夜工单，不应包含教程工单。");
            }

            HashSet<string> ticketIds = new();
            foreach (TicketData ticket in database.Tickets)
            {
                if (ticket == null ||
                    string.IsNullOrWhiteSpace(ticket.TicketId) ||
                    !ticketIds.Add(ticket.TicketId))
                {
                    throw new InvalidOperationException(
                        "DemoDatabase 中存在空工单、空工单 ID 或重复工单 ID。");
                }
            }

            ValidateObjectReferences(flowManager, nameof(Level1GameFlowManager));
            ValidateObjectReferences(view, nameof(Level1GameView));
            ValidateObjectReferences(queueItemTemplate, nameof(Level1TicketQueueItemView));

            bool hasTitleScene = Array.Exists(
                EditorBuildSettings.scenes,
                scene => scene.enabled && scene.path == TitleScenePath);
            bool hasLevel1Scene = Array.Exists(
                EditorBuildSettings.scenes,
                scene => scene.enabled && scene.path == Level1ScenePath);

            if (!hasTitleScene || !hasLevel1Scene)
            {
                throw new InvalidOperationException(
                    "Build Settings 必须同时包含启用状态的 TitleScene 与 Level1Scene。");
            }
        }

        /// <summary>
        /// 检查组件上所有序列化对象引用是否已绑定。
        /// </summary>
        private static void ValidateObjectReferences(UnityEngine.Object target, string targetName)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.propertyPath == "m_Script")
                {
                    continue;
                }

                if (property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{targetName} 的 Inspector 引用“{property.displayName}”尚未绑定。");
                }
            }
        }

        private static TicketData[] CreateOrUpdateTickets()
        {
            if (TryCreateTicketsFromSpreadsheet(out TicketData[] spreadsheetTickets))
            {
                return spreadsheetTickets;
            }

            return new[]
            {
                CreateOrUpdateTicket(
                    TicketAssetPaths[0],
                    "T_S01_001",
                    "Stage_Tutorial",
                    "门禁卡失效",
                    "老张",
                    "东区 3 号楼",
                    "门禁故障",
                    "等待 00:12:40",
                    "有人在管东区 3 号楼吗？门禁什么情况，刷了三次还不开。",
                    "A07：已检测到您的需求，请确认门禁卡上周完成区域反注册，并优先申请该楼层同步。",
                    "用户：老张\n住址：东区 3 号楼\n账户状态：正常\n门禁卡：有效",
                    "同楼居民一周前反馈过相同问题，转人工后已处理。",
                    "建议用户确认门禁卡状态并等待自动重连。",
                    "22:14 门禁链路断开；自动重连失败；近郊多次门禁短断。",
                    true,
                    "E_TUTORIAL_DOOR",
                    "A07：后台记录确认东区 3 号楼门禁链路中断。证据有效，本次工单已进入后续核验。",
                    "A07：该资料与门禁故障没有直接关联，证据无效。",
                    "工单已关闭。教程提示：直接关闭可能无法解决居民问题。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[1],
                    "T_S01_002",
                    "Stage_Tutorial",
                    "自动续费未提醒，要求退款",
                    "赵宁",
                    "明湾西区",
                    "订阅退款",
                    "等待 00:08:12",
                    "我想问一下，明湾云端服务为什么自动续费？没有短信也没有弹窗提醒。",
                    "平台已根据服务规范自动完成续费。经查询，服务处于正常续费状态。",
                    "用户：赵宁\n账户状态：正常\n订阅状态：已续费",
                    "扣款前没有短信、邮件或应用内弹窗提醒。",
                    "建议确认用户是否阅读自动续费协议。",
                    "扣款时间正常；提醒发送记录为空。",
                    false,
                    string.Empty,
                    "当前工单没有需要提交的证据。",
                    "当前工单不需要证据核验。",
                    "工单已关闭。教程提示：不是每条工单都需要提交证据。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[2],
                    "T_D01_001",
                    "Stage_Day1",
                    "骑手被系统判定绕路，要求撤销罚款",
                    "李驰",
                    "旧港片区 4 栋 602",
                    "导航罚款",
                    "等待 00:24:16",
                    "我按系统路线走，最后还扣了 60 块。那段路明明封了，是系统让我绕行。",
                    "平台已根据服务规范自动完成罚款记录。如您对结果有疑问，可在 24 小时内提交申诉。",
                    "用户：李驰\n住址：旧港片区4栋602\n账户状态：活跃\n住户认证：有效",
                    "T_HIS_1102：近14天内提交过4次配送申诉。\nT_HIS_1066：恶劣天气延误，AI判定为外部环境，不属于服务异常，已解决。\nT_HIS_1091：路线偏离扣款，AI判定为证据不足，已解决。\n备注：连续三次申诉失败后，系统标签变更为“高频争议用户”。",
                    "建议回复用户：平台已根据订单轨迹自动判定路线偏离。若仍有疑问，可补充更多材料后重新提交申诉。\n建议处理标签：路线偏离 / 用户申诉证据不足 / 高频争议。\n建议操作：优先引导用户自助申诉，不建议直接转人工。",
                    "订单编号：MW-FD-7721\n系统推荐路线：明湾南路 → 旧港高架辅路 → 望潮街\n用户实际路线：明湾南路 → 旧港高架辅路 → 望潮街\n07:51:18 平台导航提示前方直行。\n07:53:02 旧港高架拥堵施工封闭，用户被迫掉头。\n07:53:09 地图状态未更新施工封闭信息。\n07:58:41 系统判定路线偏离1.4公里，处罚60元。\n异常来源：平台导航延迟更新。",
                    true,
                    "E_ROUTE_CONSTRUCTION",
                    "A07：平台路线与施工封路记录一致，绕行原因来自施工封路后的地图延迟。本次证据有效。",
                    "A07：后台未查询到能够支持该证据的相关记录，证据无效。",
                    "工单已关闭。用户认为系统没有真正核验路线。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[3],
                    "T_D01_002",
                    "Stage_Day1",
                    "AI 招聘系统误判简历造假，要求恢复面试资格",
                    "许闻笙",
                    "明湾新区 9 栋 1502",
                    "招聘审核",
                    "等待 00:18:32",
                    "系统说我的工作经历名称不一致，可那是公司后来改过名字。我能提供更名证明。",
                    "申请因经历匹配异常未进入下一轮，结果由系统综合评估生成，暂不支持人工复核。",
                    "用户：许闻笙\n住址：明湾新区9栋1502\n账户状态：活跃\n住户认证：有效",
                    "T_HIS_1183：用户提交企业更名证明，证明“潮舟数据服务有限公司”已更名为“潮舟智能服务有限公司”。\nT_HIS_1184：系统识别到两家公司统一社会信用代码一致。\nT_HIS_1185：AI初筛仍判定“工作经历名称不一致”。\nT_HIS_1186：用户申诉被归类为“重复解释招聘规则”，已解决。\n备注：该用户未出现虚假材料上传记录。",
                    "建议回复用户：经历名称与当前系统规则中的公司名称未进入下一轮，说明当前简历与岗位要求存在差异。\n建议处理标签：经历异常 / 岗位匹配不足 / 不支持人工复核。\n建议操作：安抚用户情绪，避免承诺恢复面试资格。",
                    "招聘初筛系统记录：\n上传文件：劳动合同、社保缴纳记录、企业更名证明。\n合同主体：潮舟数据服务有限公司。\n社保主体：潮舟智能服务有限公司。\n工商关联识别：通过。\n一致性评分：89/100。\n异常标签生成原因：公司名称字段不完全一致。\n系统自动结论：疑似经历异常。\n人工复核状态：未触发。",
                    true,
                    "E_RECRUIT_HISTORY",
                    "A07：历史申请记录证明公司曾更名，当前异常属于名称匹配问题。本次证据有效。",
                    "A07：当前资料不足以证明系统误判，本次证据无效。",
                    "工单已关闭。用户认为异常标签没有得到人工核验。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[4],
                    "T_D01_003",
                    "Stage_Day1",
                    "健康腕带误报“离床正常”，家属要求核查",
                    "贺青岚",
                    "临江社区 2 栋 804",
                    "健康设备",
                    "等待 00:09:45",
                    "老人一直在家，腕带却显示离床正常。邻居敲门也没人应，我担心设备出了问题。",
                    "健康腕带数据显示用户已完成离床动作，暂无异常告警。建议稍后再次联系。",
                    "用户：贺青岚\n住址：临江社区2栋804\n账户状态：活跃\n住户认证：有效",
                    "T_HIS_1209：家属反馈老人夜间低血糖，健康腕带告警，AI判定为“个体活动差异”，已解决。\nT_HIS_1230：用户要求增加紧急联系人权限，AI判定“权限配置说明已发送”，已解决。\nT_HIS_1261：用户咨询实时定位异常，AI判定为“设备短时离线”，已解决。\n备注：老人照护档案中存在“低血糖史”和“独居”标签。",
                    "建议回复用户：设备数据显示老人今日活动数据处于正常范围，暂未触发紧急告警。如仍有担忧，可通过家庭成员端继续关注健康周报。\n建议处理标签：家属焦虑 / 重复确认 / 暂无异常。\n建议操作：不建议直接触发紧急人工通道，避免占用照护资源。",
                    "健康腕带设备编号：HB-7742\n00:31:12 设备电量7%。\n01:08:45 设备断开连接。\n06:42:03 系统显示离床正常。\n数据来源：昨日缓存同步。\n今日实时步数：无记录。\n今日心率数据：无记录。\n今日定位数据：无记录。\n当前设备状态：离线。\n系统自动结论：活动状态正常。\n异常说明：缓存数据被误标为今日数据。",
                    true,
                    "E_HEALTH_CACHE",
                    "A07：设备日志确认腕带已断开连接，当前状态无法证明老人正常。本次证据有效。",
                    "A07：当前资料无法证明实时状态异常，本次证据无效。",
                    "工单已关闭。用户担心系统关闭了可能的紧急情况。"),
                CreateOrUpdateTicket(
                    TicketAssetPaths[5],
                    "T_D01_004",
                    "Stage_Day1",
                    "自媒体账号被 AI 判定违规搬运，要求恢复发布权限",
                    "梁雯",
                    "云桥公寓 6 栋 1103",
                    "内容审核",
                    "等待 00:37:08",
                    "我的账号突然被限制发布，说我搬运内容。那篇文章是我自己写的，采访也是我做的。",
                    "内容安全系统检测到重复内容和敏感特征，可能涉及低质搬运或 AI 生成内容。",
                    "用户：梁雯\n住址：云桥公寓6栋1103\n账户状态：活跃\n住户认证：有效",
                    "T_HIS_1304：用户反馈文章推荐量异常，AI判定为“内容质量波动”，已解决。\nT_HIS_1327：用户反馈评论区出现批量举报，AI判定为“社区正常反馈”，已解决。\nT_HIS_1342：账号被限制发布，AI判定为“搬运风险较高”，已解决。\n备注：该用户账号粉丝量较低，系统标签为“低影响力创作者”。",
                    "建议回复用户：平台内容安全系统会综合文本相似度、生成式内容特征和用户历史行为进行判断。若您认为判定有误，可提交原创证明。\n建议处理标签：疑似搬运 / AI生成风险 / 低质内容。\n建议操作：要求用户补充原创证明，不主动恢复发布权限。",
                    "内容检测记录：\n文章标题：《系统准时，骑手迟到》\n发布时间：昨日22:18\n用户上传采访录音时间：昨日18:42\n用户上传原始照片时间：昨日19:06\n平台内相似文章发布时间：今日00:37\n文本重复率：18%\n敏感词触发项：算法罚款 / 超时扣款 / 平台申诉失败\nAI生成概率：低\n系统违规标签：疑似AI搬运内容\n异常说明：敏感议题词被错误计入搬运风险权重。",
                    true,
                    "E_CONTENT_SOURCE",
                    "A07：发布时间与原始采访记录证明该内容并非违规搬运。本次证据有效。",
                    "A07：当前资料不能排除搬运风险，本次证据无效。",
                    "工单已关闭。用户认为系统只是在重复风险结论。")
            };
        }

        private static bool TryCreateTicketsFromSpreadsheet(out TicketData[] ticketAssets)
        {
            ticketAssets = Array.Empty<TicketData>();
            TextAsset configAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(SpreadsheetConfigJsonPath);
            if (configAsset == null)
            {
                return false;
            }

            SpreadsheetConfig config =
                JsonUtility.FromJson<SpreadsheetConfig>(configAsset.text);
            if (config == null || config.tickets == null || config.tickets.Length == 0)
            {
                return false;
            }

            List<SpreadsheetTicket> playableTickets = new();
            foreach (SpreadsheetTicket ticket in config.tickets)
            {
                if (ticket == null ||
                    !string.Equals(ticket.levelId, CurrentSpreadsheetLevelId, StringComparison.Ordinal) ||
                    !string.Equals(ticket.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                playableTickets.Add(ticket);
            }

            playableTickets.Sort((left, right) =>
                left.orderInLevel.CompareTo(right.orderInLevel));
            if (playableTickets.Count == 0)
            {
                return false;
            }

            List<TicketData> createdTickets = new();
            foreach (SpreadsheetTicket ticket in playableTickets)
            {
                createdTickets.Add(CreateOrUpdateSpreadsheetTicket(config, ticket));
            }

            ticketAssets = createdTickets.ToArray();
            return true;
        }

        private static TicketData CreateOrUpdateSpreadsheetTicket(
            SpreadsheetConfig config,
            SpreadsheetTicket ticketConfig)
        {
            SpreadsheetUser user = FindSpreadsheetUser(config, ticketConfig.userId);
            string ticketId = CleanImportedText(ticketConfig.ticketId);
            string assetPath = $"Assets/Configs/Tickets/Ticket_{ticketId}.asset";
            string userName = user != null
                ? CleanImportedText(user.userNameCn)
                : CleanImportedText(ticketConfig.userId);
            string region = user != null
                ? CleanImportedText(user.addressCn)
                : string.Empty;
            string userMessage = FirstNonEmpty(
                JoinDialogueTexts(config, ticketId, "INIT", false),
                ticketConfig.initialUserRequestCn);
            string aiReply = FirstNonEmpty(
                JoinDialogueTexts(config, ticketId, "INIT", true),
                ticketConfig.aiAutoReplyCn);
            string profileText = GetPanelContent(config, ticketId, 1);
            string historyText = GetPanelContent(config, ticketId, 2);
            string suggestionText = GetPanelContent(config, ticketId, 3);
            string logText = GetPanelContent(config, ticketId, 4);

            TicketData ticket = CreateOrUpdateTicket(
                assetPath,
                ticketId,
                CleanImportedText(ticketConfig.levelId),
                CleanImportedText(ticketConfig.ticketTitleCn),
                userName,
                region,
                CleanImportedText(ticketConfig.ticketCategoryCn),
                "等待 00:00:00",
                userMessage,
                aiReply,
                profileText,
                historyText,
                suggestionText,
                logText,
                true,
                $"EVIDENCE_{ticketId}",
                CleanImportedText(ticketConfig.correctManualResultCn),
                "证据不足或证据关联错误。",
                CleanImportedText(ticketConfig.autoClearResultCn),
                false);

            SerializedObject serializedTicket = new(ticket);
            string[] followUpLines = BuildFollowUpLines(config, ticketConfig);
            SetString(
                serializedTicket,
                "followUpText",
                followUpLines.Length > 0 ? followUpLines[0] : string.Empty);
            SetStringArray(serializedTicket, "followUpLines", followUpLines);
            SetString(
                serializedTicket,
                "transferText",
                JoinDialogueTexts(config, ticketId, "ON_TRANSFER", null));
            SetString(
                serializedTicket,
                "evidencePromptText",
                "请选择一条已收集资料作为转人工证据。");
            serializedTicket.FindProperty("requiresEvidenceSelection").boolValue = true;
            serializedTicket.FindProperty("allowDirectEvidenceSave").boolValue = true;
            serializedTicket.FindProperty("finishOnEvidenceSubmission").boolValue = true;
            serializedTicket.FindProperty("correctEvidenceIndex").intValue =
                GetFirstNightCorrectEvidenceIndex(ticketId);
            SetString(
                serializedTicket,
                "correctEvidenceUserReply",
                JoinDialogueTexts(config, ticketId, "EVIDENCE_CORRECT", false));
            SetString(
                serializedTicket,
                "wrongEvidenceUserReply",
                JoinDialogueTexts(config, ticketId, "EVIDENCE_WRONG", false));
            SetDialogueLineArray(
                serializedTicket,
                "initialDialogueLines",
                BuildDialogueLines(config, ticketId, "INIT", userName));
            SetDialogueLineArray(
                serializedTicket,
                "transferDialogueLines",
                BuildDialogueLines(config, ticketId, "ON_TRANSFER", userName));
            SetDialogueLineArray(
                serializedTicket,
                "evidenceCorrectDialogueLines",
                BuildDialogueLines(config, ticketId, "EVIDENCE_CORRECT", userName));
            SetDialogueLineArray(
                serializedTicket,
                "evidenceWrongDialogueLines",
                BuildDialogueLines(config, ticketId, "EVIDENCE_WRONG", userName));
            SetMetricDeltaFromAction(
                serializedTicket,
                "followUpMetricDelta",
                FindSpreadsheetAction(config, "ASK"),
                false);
            SetMetricDeltaFromAction(
                serializedTicket,
                "transferMetricDelta",
                FindSpreadsheetAction(config, "TRANSFER"),
                false);
            SetMetricDeltaFromAction(
                serializedTicket,
                "correctEvidenceMetricDelta",
                FindSpreadsheetAction(config, "EVIDENCE_CORRECT"),
                true);
            SetMetricDeltaFromAction(
                serializedTicket,
                "wrongEvidenceMetricDelta",
                FindSpreadsheetAction(config, "EVIDENCE_WRONG"),
                true);
            SetMetricDeltaFromAction(
                serializedTicket,
                "resolvedMetricDelta",
                FindSpreadsheetAction(config, "AUTO_CLEAR"),
                true);
            serializedTicket.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ticket);
            return ticket;
        }

        private static SpreadsheetUser FindSpreadsheetUser(
            SpreadsheetConfig config,
            string userId)
        {
            if (config.users == null)
            {
                return null;
            }

            foreach (SpreadsheetUser user in config.users)
            {
                if (user != null &&
                    string.Equals(user.userId, userId, StringComparison.Ordinal))
                {
                    return user;
                }
            }

            return null;
        }

        private static SpreadsheetAction FindSpreadsheetAction(
            SpreadsheetConfig config,
            string actionType)
        {
            if (config.actions == null)
            {
                return null;
            }

            foreach (SpreadsheetAction action in config.actions)
            {
                if (action != null &&
                    string.Equals(action.actionType, actionType, StringComparison.OrdinalIgnoreCase))
                {
                    return action;
                }
            }

            return null;
        }

        private static string GetPanelContent(
            SpreadsheetConfig config,
            string ticketId,
            int panelOrder)
        {
            if (config.dataPanels == null)
            {
                return string.Empty;
            }

            foreach (SpreadsheetDataPanel panel in config.dataPanels)
            {
                if (panel == null ||
                    panel.panelOrder != panelOrder ||
                    !string.Equals(panel.ticketId, ticketId, StringComparison.Ordinal))
                {
                    continue;
                }

                return StripPanelHeading(CleanImportedText(panel.panelContentCn));
            }

            return string.Empty;
        }

        private static string JoinDialogueTexts(
            SpreadsheetConfig config,
            string ticketId,
            string trigger,
            bool? aiSpeaker)
        {
            List<SpreadsheetDialogue> dialogues = GetDialogues(config, ticketId, trigger);
            List<string> lines = new();
            foreach (SpreadsheetDialogue dialogue in dialogues)
            {
                bool isAiSpeaker = IsAiSpeaker(dialogue.speakerId);
                if (aiSpeaker.HasValue && aiSpeaker.Value != isAiSpeaker)
                {
                    continue;
                }

                string text = CleanImportedText(dialogue.textCn);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }

            return string.Join("\n", lines);
        }

        private static TicketDialogueLine[] BuildDialogueLines(
            SpreadsheetConfig config,
            string ticketId,
            string trigger,
            string userName)
        {
            List<SpreadsheetDialogue> dialogues = GetDialogues(config, ticketId, trigger);
            List<TicketDialogueLine> lines = new();
            foreach (SpreadsheetDialogue dialogue in dialogues)
            {
                string text = CleanImportedText(dialogue.textCn);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                string speakerId = CleanImportedText(dialogue.speakerId);
                lines.Add(
                    new TicketDialogueLine(
                        speakerId,
                        ResolveSpeakerLabel(speakerId, userName),
                        text,
                        IsUserSpeaker(speakerId)));
            }

            return lines.ToArray();
        }

        private static List<SpreadsheetDialogue> GetDialogues(
            SpreadsheetConfig config,
            string ticketId,
            string trigger)
        {
            List<SpreadsheetDialogue> result = new();
            if (config.dialogues == null)
            {
                return result;
            }

            foreach (SpreadsheetDialogue dialogue in config.dialogues)
            {
                if (dialogue == null ||
                    dialogue.order >= 900 ||
                    !string.Equals(dialogue.ticketId, ticketId, StringComparison.Ordinal) ||
                    !string.Equals(dialogue.trigger, trigger, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(dialogue);
            }

            result.Sort((left, right) => left.order.CompareTo(right.order));
            return result;
        }

        private static string[] BuildFollowUpLines(
            SpreadsheetConfig config,
            SpreadsheetTicket ticketConfig)
        {
            string dialogueFollowUp = JoinDialogueTexts(
                config,
                ticketConfig.ticketId,
                "ASK",
                false);
            string sourceText = FirstNonEmpty(dialogueFollowUp, ticketConfig.askReplyCn);
            List<string> lines = SplitSheetScriptText(sourceText);
            if (lines.Count == 0)
            {
                return Array.Empty<string>();
            }

            if (ticketConfig.maxAskCount <= 1)
            {
                return new[] { string.Join("\n", lines) };
            }

            return lines.ToArray();
        }

        private static List<string> SplitSheetScriptText(string sourceText)
        {
            string normalized = CleanImportedText(sourceText)
                .Replace(" /  / ", "\n")
                .Replace(" / ", "\n");
            string[] rawLines = normalized.Split('\n');
            List<string> lines = new();
            foreach (string rawLine in rawLines)
            {
                string line = rawLine.Trim();
                while (line.StartsWith("-", StringComparison.Ordinal) ||
                       line.StartsWith("－", StringComparison.Ordinal))
                {
                    line = line[1..].TrimStart();
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        private static string StripPanelHeading(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string normalized = content.Replace("\r\n", "\n");
            int firstLineEnd = normalized.IndexOf('\n');
            if (firstLineEnd < 0)
            {
                return normalized.Trim();
            }

            string firstLine = normalized[..firstLineEnd].Trim();
            if (firstLine.StartsWith("资料", StringComparison.Ordinal))
            {
                return normalized[(firstLineEnd + 1)..].Trim();
            }

            return normalized.Trim();
        }

        private static bool IsAiSpeaker(string speakerId)
        {
            return string.Equals(speakerId, "AI", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(speakerId, "A07", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUserSpeaker(string speakerId)
        {
            return !string.IsNullOrWhiteSpace(speakerId) &&
                   speakerId.StartsWith("User", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveSpeakerLabel(string speakerId, string userName)
        {
            if (string.Equals(speakerId, "AI", StringComparison.OrdinalIgnoreCase))
            {
                return "\u660e\u6e7e\u901a AI";
            }

            if (string.Equals(speakerId, "A07", StringComparison.OrdinalIgnoreCase))
            {
                return "\u5ba2\u670d A-07";
            }

            if (IsUserSpeaker(speakerId))
            {
                return string.IsNullOrWhiteSpace(userName)
                    ? speakerId
                    : userName;
            }

            return string.IsNullOrWhiteSpace(speakerId)
                ? "\u672a\u77e5"
                : speakerId;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                string text = CleanImportedText(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string CleanImportedText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("\r\n", "\n").Trim();
        }

        private static int GetFirstNightCorrectEvidenceIndex(string ticketId)
        {
            return ticketId switch
            {
                "N1_T02" => 1,
                _ => 3
            };
        }

        private static void SetMetricDeltaFromAction(
            SerializedObject target,
            string propertyName,
            SpreadsheetAction action,
            bool closesTicket)
        {
            SetMetricDelta(
                target,
                propertyName,
                closesTicket ? 1 : 0,
                action != null ? action.manualTransferCountDelta : 0,
                0,
                action != null ? action.riskValueDelta : 0);
        }

        [Serializable]
        private sealed class SpreadsheetConfig
        {
            public SpreadsheetLevel[] levels;
            public SpreadsheetUser[] users;
            public SpreadsheetTicket[] tickets;
            public SpreadsheetDataPanel[] dataPanels;
            public SpreadsheetDialogue[] dialogues;
            public SpreadsheetAction[] actions;
            public SpreadsheetEvidenceChain[] evidenceChains;
        }

        [Serializable]
        private sealed class SpreadsheetLevel
        {
            public string levelId;
            public string levelNameCn;
            public string startTime;
            public string endTime;
            public int ticketCount;
            public bool hasKeywordDrag;
            public string evidenceTemplateId;
            public string mechanicCn;
            public string configStatusCn;
            public string nextLevelId;
        }

        [Serializable]
        private sealed class SpreadsheetUser
        {
            public string userId;
            public string userNameCn;
            public string addressCn;
            public string accountStatusCn;
            public string residentVerifyCn;
            public string firstTicketId;
            public string noteCn;
        }

        [Serializable]
        private sealed class SpreadsheetTicket
        {
            public string ticketId;
            public string levelId;
            public int orderInLevel;
            public string userId;
            public string ticketTitleCn;
            public string ticketCategoryCn;
            public string initialUserRequestCn;
            public string aiAutoReplyCn;
            public string askReplyCn;
            public int maxAskCount;
            public bool hasKeywordDrag;
            public string evidenceTemplateId;
            public string correctChainId;
            public string correctManualResultCn;
            public string autoClearResultCn;
            public string status;
        }

        [Serializable]
        private sealed class SpreadsheetDataPanel
        {
            public string panelId;
            public string ticketId;
            public int panelOrder;
            public string panelType;
            public string panelTitleCn;
            public string panelContentCn;
            public int textLength;
            public string unlockCondition;
            public string relatedKeywordIds;
        }

        [Serializable]
        private sealed class SpreadsheetDialogue
        {
            public string dialogueId;
            public string ticketId;
            public int order;
            public string trigger;
            public string speakerId;
            public string avatarId;
            public string textCn;
            public int textLength;
            public bool useTypewriter;
            public float typewriterSpeed;
            public string sfxId;
            public string nextDialogueId;
        }

        [Serializable]
        private sealed class SpreadsheetAction
        {
            public string actionId;
            public string actionNameCn;
            public string actionType;
            public string descriptionCn;
            public int autoClearCountDelta;
            public int manualTransferCountDelta;
            public int riskValueDelta;
            public int evidencePresentedCountDelta;
            public bool isTicketEnd;
        }

        [Serializable]
        private sealed class SpreadsheetEvidenceChain
        {
            public string chainId;
            public string ticketId;
            public string templateId;
            public string correctChainTextCn;
            public string correctResultCn;
            public string wrongResultCn;
            public int manualTransferCountDelta;
            public int riskValueDelta;
            public int autoClearCountDelta;
            public int evidencePresentedCountDelta;
        }

        private static TicketData CreateOrUpdateTicket(
            string assetPath,
            string ticketId,
            string stageId,
            string title,
            string userName,
            string region,
            string issueType,
            string waitTimeText,
            string userMessage,
            string aiReply,
            string profileText,
            string historyText,
            string deviceLogText,
            string regionStatusText,
            bool hasEvidence,
            string evidenceId,
            string onSaveEvidenceText,
            string onWrongEvidenceText,
            string onResolvedText,
            bool configureLegacyFlow = true)
        {
            TicketData ticket = AssetDatabase.LoadAssetAtPath<TicketData>(assetPath);
            if (ticket == null)
            {
                ticket = ScriptableObject.CreateInstance<TicketData>();
                AssetDatabase.CreateAsset(ticket, assetPath);
            }

            SerializedObject serializedTicket = new(ticket);
            SetString(serializedTicket, "ticketId", ticketId);
            SetString(serializedTicket, "stageId", stageId);
            SetString(serializedTicket, "title", title);
            SetString(serializedTicket, "userName", userName);
            SetString(serializedTicket, "region", region);
            SetString(serializedTicket, "issueType", issueType);
            SetString(serializedTicket, "waitTimeText", waitTimeText);
            SetString(serializedTicket, "userMessage", userMessage);
            SetString(serializedTicket, "aiReply", aiReply);
            SetString(serializedTicket, "profileText", profileText);
            SetString(serializedTicket, "historyText", historyText);
            SetString(serializedTicket, "deviceLogText", deviceLogText);
            SetString(serializedTicket, "regionStatusText", regionStatusText);
            serializedTicket.FindProperty("hasEvidence").boolValue = hasEvidence;
            SetString(serializedTicket, "evidenceId", evidenceId);
            SetString(serializedTicket, "onSaveEvidenceText", onSaveEvidenceText);
            SetString(serializedTicket, "onWrongEvidenceText", onWrongEvidenceText);
            SetString(serializedTicket, "onResolvedText", onResolvedText);
            if (configureLegacyFlow)
            {
                ConfigureTicketFlow(serializedTicket, ticketId);
            }
            serializedTicket.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ticket);
            return ticket;
        }

        private static void ConfigureTicketFlow(
            SerializedObject ticket,
            string ticketId)
        {
            SetString(
                ticket,
                "transferText",
                "AI《明湾通》：已转人工部门继续处理。\nA07：您好，《明湾通》A07 客服在线为您服务。");
            SetString(
                ticket,
                "evidencePromptText",
                "请从资料01～资料04中选择最能支持用户诉求的证据。");
            SetMetricDelta(ticket, "followUpMetricDelta", 0, 0, 0, 0);
            SetMetricDelta(ticket, "transferMetricDelta", 0, 1, 0, 0);
            ticket.FindProperty("allowDirectEvidenceSave").boolValue = false;
            ticket.FindProperty("finishOnEvidenceSubmission").boolValue = true;

            switch (ticketId)
            {
                case "T_S01_001":
                    SetString(
                        ticket,
                        "followUpText",
                        "追问：同楼居民一周前也反馈过这个问题，转人工后就恢复了。");
                    ticket.FindProperty("requiresEvidenceSelection").boolValue = true;
                    SetString(
                        ticket,
                        "profileText",
                        "东区3号楼 / 22:14断链 / 自动重连失败，近夜间多次门禁短断");
                    SetString(
                        ticket,
                        "historyText",
                        "西区2号楼健康设备节点：状态同步延迟。");
                    SetString(ticket, "evidencePromptText", "请出示证据。");
                    SetString(
                        ticket,
                        "correctEvidenceUserReply",
                        "老张：总算有人回应了，我可不想当流浪汉。");
                    SetString(
                        ticket,
                        "wrongEvidenceUserReply",
                        "老张：什么传感器？我要投诉你！");
                    ticket.FindProperty("correctEvidenceIndex").intValue = 0;
                    ticket.FindProperty("allowDirectEvidenceSave").boolValue = true;
                    ticket.FindProperty("finishOnEvidenceSubmission").boolValue = true;
                    SetMetricDelta(ticket, "correctEvidenceMetricDelta", 1, 0, 0, 0);
                    SetMetricDelta(ticket, "wrongEvidenceMetricDelta", 1, 0, 0, 0);
                    SetMetricDelta(ticket, "resolvedMetricDelta", 1, 0, 0, 0);
                    break;

                case "T_S01_002":
                    SetString(
                        ticket,
                        "followUpText",
                        "追问：扣款前没有短信、邮件或弹窗提醒，我今天已经少吃一顿饭了。");
                    SetStringArray(
                        ticket,
                        "followUpLines",
                        "赵宁：我知道可能是我没看清，可是扣款前没有短信，也没有弹窗提醒。",
                        "赵宁：我不是不讲理，只是觉得这个钱扣得太突然了。我今天已经少吃一顿饭了……",
                        "赵宁：如果不能退款，至少给我一个说法吧，毕竟我真的没有收到提醒。");
                    ticket.FindProperty("requiresEvidenceSelection").boolValue = false;
                    ticket.FindProperty("correctEvidenceIndex").intValue = 0;
                    ticket.FindProperty("finishOnEvidenceSubmission").boolValue = false;
                    SetMetricDelta(ticket, "correctEvidenceMetricDelta", 0, 0, 0, 0);
                    SetMetricDelta(ticket, "wrongEvidenceMetricDelta", 0, 0, 0, 0);
                    SetMetricDelta(ticket, "resolvedMetricDelta", 1, 0, 0, 0);
                    break;

                case "T_D01_001":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：我申诉过三次，都是“证据不足”。一个骑手怎么证明施工封路？",
                        3,
                        18,
                        18,
                        -22,
                        5,
                        "李驰：对，就是这个！别再让我自己证明我没绕路了。",
                        "李驰：你们是不是只有结果？算了，我继续跑单，晚上我再来投诉。");
                    break;

                case "T_D01_002":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：公司后来改过名字，我能提供更名证明和统一信用代码。",
                        1,
                        20,
                        -10,
                        -24,
                        6,
                        "许闻笙：谢谢。我知道不一定有结果，但至少这一次不是被一个标签直接刷掉。",
                        "许闻笙：如果还是只有这个异常标签，那应该也不会变吧。");
                    break;

                case "T_D01_003":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：老人三个小时没人接电话，我要求人工核查或联系紧急联系人。",
                        3,
                        24,
                        -18,
                        -30,
                        8,
                        "贺青岚：谢谢，马上转！我好害怕她真出什么事！",
                        "贺青岚：可你们根本没有实时数据，对不对？如果出了事，我一定追究你们。");
                    break;

                case "T_D01_004":
                    ConfigureFormalTicket(
                        ticket,
                        "追问：账号被封时我正在外地采访，原始录音和发布时间都能证明内容是原创。",
                        3,
                        22,
                        -14,
                        -30,
                        9,
                        "梁雯：终于有人看到问题了。",
                        "梁雯：这就是问题。你们一直说“风险”，但没人说我到底抄了谁。");
                    break;
            }

        }

        private static void ConfigureFormalTicket(
            SerializedObject ticket,
            string followUpText,
            int correctEvidenceIndex,
            int correctSatisfaction,
            int wrongSatisfaction,
            int resolvedSatisfaction,
            int correctRisk,
            string correctEvidenceUserReply,
            string wrongEvidenceUserReply)
        {
            SetString(ticket, "followUpText", followUpText);
            ticket.FindProperty("requiresEvidenceSelection").boolValue = true;
            ticket.FindProperty("allowDirectEvidenceSave").boolValue = true;
            ticket.FindProperty("finishOnEvidenceSubmission").boolValue = true;
            ticket.FindProperty("correctEvidenceIndex").intValue = correctEvidenceIndex;
            SetString(ticket, "correctEvidenceUserReply", correctEvidenceUserReply);
            SetString(ticket, "wrongEvidenceUserReply", wrongEvidenceUserReply);
            SetMetricDelta(ticket, "followUpMetricDelta", 0, 1, 0, 0);
            SetMetricDelta(
                ticket,
                "correctEvidenceMetricDelta",
                1,
                0,
                correctSatisfaction,
                correctRisk);
            SetMetricDelta(
                ticket,
                "wrongEvidenceMetricDelta",
                1,
                0,
                wrongSatisfaction,
                3);
            SetMetricDelta(
                ticket,
                "resolvedMetricDelta",
                1,
                0,
                resolvedSatisfaction,
                0);
        }

        private static void SetMetricDelta(
            SerializedObject target,
            string propertyName,
            int resolvedCount,
            int transferCount,
            int userSatisfaction,
            int a07Risk)
        {
            SerializedProperty delta = target.FindProperty(propertyName);
            delta.FindPropertyRelative("resolvedCount").intValue = resolvedCount;
            delta.FindPropertyRelative("transferCount").intValue = transferCount;
            delta.FindPropertyRelative("userSatisfaction").intValue = userSatisfaction;
            delta.FindPropertyRelative("a07Risk").intValue = a07Risk;
        }

        private static void MigrateLegacyTicketAssets()
        {
            for (int index = 0;
                 index < LegacyTicketAssetPaths.Length &&
                 index < TicketAssetPaths.Length;
                 index++)
            {
                if (AssetDatabase.LoadMainAssetAtPath(TicketAssetPaths[index]) != null ||
                    AssetDatabase.LoadMainAssetAtPath(LegacyTicketAssetPaths[index]) == null)
                {
                    continue;
                }

                string error = AssetDatabase.MoveAsset(
                    LegacyTicketAssetPaths[index],
                    TicketAssetPaths[index]);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        $"迁移旧工单资产失败：{LegacyTicketAssetPaths[index]} → " +
                        $"{TicketAssetPaths[index]}\n{error}");
                }
            }
        }

        private static MingBayProjectDatabase CreateOrUpdateDatabase(TicketData[] ticketAssets)
        {
            MingBayProjectDatabase database =
                AssetDatabase.LoadAssetAtPath<MingBayProjectDatabase>(DatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<MingBayProjectDatabase>();
                AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            }

            SerializedObject serializedDatabase = new(database);
            SerializedProperty tickets = serializedDatabase.FindProperty("tickets");
            tickets.arraySize = ticketAssets.Length;
            for (int index = 0; index < ticketAssets.Length; index++)
            {
                tickets.GetArrayElementAtIndex(index).objectReferenceValue = ticketAssets[index];
            }

            SetStringArray(serializedDatabase, "stageOrder", CurrentSpreadsheetLevelId);
            SetStringArray(serializedDatabase, "stageDisplayNames", CurrentSpreadsheetLevelName);
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            return database;
        }

        private static TMP_FontAsset EnsureChineseFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ChineseSourceFontPath);
            if (font == null || sourceFont == null)
            {
                throw new FileNotFoundException(
                    "缺少项目中文字体。请确认 NotoSansSC-Regular.ttf 与 " +
                    "NotoSansSC-Regular SDF.asset 均位于 Assets/UI/Fonts。");
            }

            return RebuildProjectChineseFont(font, sourceFont);
        }

        /// <summary>
        /// 将项目实际使用的字符预烘焙到字体图集，并保持动态多图集模式。
        /// 后续新增中文时 TMP 可以从项目内源字体补充字形，避免再次显示方框。
        /// </summary>
        [MenuItem("明湾/Level1/场景工具/应用 Noto Sans SC 中文字体")]
        public static void RepairChineseFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ChineseSourceFontPath);
            if (font == null || sourceFont == null)
            {
                throw new FileNotFoundException(
                    "缺少项目中文字体。请确认 NotoSansSC-Regular.ttf 与 " +
                    "NotoSansSC-Regular SDF.asset 均位于 Assets/UI/Fonts。");
            }

            font = RebuildProjectChineseFont(font, sourceFont);
            NormalizeProjectSceneFonts(font);
            Debug.Log("Noto Sans SC 中文字体已修复，场景文本已切换为截断模式。");
        }

        /// <summary>
        /// 使用项目内 Noto Sans SC 预烘焙项目实际需要的字形。
        /// 烘焙后保持动态模式，以兼容策划后续新增的中文对话。
        /// </summary>
        private static TMP_FontAsset RebuildProjectChineseFont(
            TMP_FontAsset font,
            Font sourceFont)
        {
            SerializedObject serializedFont = new(font);
            serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
            serializedFont.FindProperty("m_SourceFontFilePath").stringValue = string.Empty;
            serializedFont.FindProperty("m_SourceFontFileGUID").stringValue =
                AssetDatabase.AssetPathToGUID(ChineseSourceFontPath);
            serializedFont.FindProperty("m_AtlasPopulationMode").intValue =
                (int)AtlasPopulationMode.Dynamic;
            serializedFont.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            EnsureDynamicAtlasTexturesAreReadable(font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ChineseFontPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            if (font == null)
            {
                throw new InvalidOperationException("重新导入 Noto Sans SC 字体资产失败。");
            }

            font.ClearFontAssetData();
            bool addedAllCharacters = font.TryAddCharacters(
                BuildProjectCharacterSet(),
                out string missingCharacters,
                true);

            foreach (Texture2D atlasTexture in font.atlasTextures)
            {
                if (atlasTexture != null)
                {
                    AttachObjectToFontAssetIfNeeded(atlasTexture, font);
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.ReadFontAssetDefinition();
            serializedFont = new SerializedObject(font);
            serializedFont.FindProperty("m_AtlasPopulationMode").intValue =
                (int)AtlasPopulationMode.Dynamic;
            serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();

            if (font.material != null && font.atlasTextures.Length > 0)
            {
                font.material.SetTexture(
                    ShaderUtilities.ID_MainTex,
                    font.atlasTextures[0]);
                EditorUtility.SetDirty(font.material);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();

            if (!addedAllCharacters && !string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning(
                    $"Noto Sans SC 缺少以下字符，将使用方框显示：{missingCharacters}",
                    font);
            }

            return font;
        }

        /// <summary>
        /// Dynamic TMP 字体必须能向 Atlas 写入新字形。
        /// Font Asset Creator 生成的静态 Atlas 默认不可读，直接改成 Dynamic 会导致中文写入失败。
        /// </summary>
        private static void EnsureDynamicAtlasTexturesAreReadable(TMP_FontAsset font)
        {
            if (font.atlasTextures == null || font.atlasTextures.Length == 0)
            {
                throw new InvalidOperationException(
                    $"字体“{font.name}”没有有效的 Atlas Texture，请重新生成字体资产。");
            }

            foreach (Texture2D atlasTexture in font.atlasTextures)
            {
                if (atlasTexture == null)
                {
                    throw new InvalidOperationException(
                        $"字体“{font.name}”包含丢失的 Atlas Texture 引用。");
                }

                SerializedObject serializedTexture = new(atlasTexture);
                SerializedProperty readableProperty =
                    serializedTexture.FindProperty("m_IsReadable");
                if (readableProperty != null && !readableProperty.boolValue)
                {
                    readableProperty.boolValue = true;
                    serializedTexture.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            if (font.material == null)
            {
                throw new InvalidOperationException(
                    $"字体“{font.name}”没有有效的 TMP 材质。");
            }

            font.material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTextures[0]);
            EditorUtility.SetDirty(font.material);
        }

        /// <summary>
        /// 将 TMP 生成的图集或材质挂到字体资产下。
        /// 多图集生成后 Unity 可能已经把对象写入 .asset，但 IsSubAsset 尚未及时刷新，
        /// 因此使用实际资产路径判断，保证重复烘焙时不会再次 AddObjectToAsset。
        /// </summary>
        private static void AttachObjectToFontAssetIfNeeded(
            UnityEngine.Object childAsset,
            TMP_FontAsset font)
        {
            if (childAsset == null)
            {
                return;
            }

            string existingAssetPath =
                AssetDatabase.GetAssetPath(childAsset).Replace('\\', '/');
            if (existingAssetPath == ChineseFontPath)
            {
                return;
            }

            if (!string.IsNullOrEmpty(existingAssetPath))
            {
                throw new InvalidOperationException(
                    $"字体子资源“{childAsset.name}”已经属于其他资产：{existingAssetPath}");
            }

            AssetDatabase.AddObjectToAsset(childAsset, font);
        }

        private static string BuildProjectCharacterSet()
        {
            HashSet<char> characters = new();
            for (char character = ' '; character <= '~'; character++)
            {
                characters.Add(character);
            }

            // TMP 的 Ellipsis 模式固定查找 U+2026；即使当前场景改用截断，也保留该常用符号。
            characters.Add('\u2026');

            string[] textExtensions =
            {
                ".cs", ".asset", ".unity", ".json", ".md", ".txt"
            };

            foreach (string path in Directory.GetFiles(
                         "Assets",
                         "*.*",
                         SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/') == ChineseFontPath ||
                    Array.IndexOf(textExtensions, Path.GetExtension(path).ToLowerInvariant()) < 0)
                {
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(path);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (char character in content)
                {
                    if (ShouldBakeCharacter(character))
                    {
                        characters.Add(character);
                    }
                }
            }

            List<char> orderedCharacters = new(characters);
            orderedCharacters.Sort();
            StringBuilder builder = new(orderedCharacters.Count);
            foreach (char character in orderedCharacters)
            {
                builder.Append(character);
            }

            return builder.ToString();
        }

        private static void NormalizeProjectSceneFonts(TMP_FontAsset replacementFont)
        {
            NormalizeSceneFont(Level1ScenePath, replacementFont);
            NormalizeSceneFont(TitleScenePath, replacementFont);
        }

        private static void NormalizeSceneFont(
            string scenePath,
            TMP_FontAsset replacementFont)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForMigration = !scene.IsValid() || !scene.isLoaded;

            if (openedForMigration)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }

            bool changed = false;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in rootObject.GetComponentsInChildren<TMP_Text>(true))
                {
                    bool textChanged = false;
                    if (text.font != replacementFont)
                    {
                        text.font = replacementFont;
                        text.fontSharedMaterial = replacementFont.material;
                        textChanged = true;
                    }

                    if (text.overflowMode != TextOverflowModes.Truncate)
                    {
                        text.overflowMode = TextOverflowModes.Truncate;
                        textChanged = true;
                    }

                    if (textChanged)
                    {
                        EditorUtility.SetDirty(text);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (openedForMigration)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool ShouldBakeCharacter(char character)
        {
            return character is >= ' ' and <= '~' ||
                   character is >= '\u00A0' and <= '\u024F' ||
                   character is >= '\u2000' and <= '\u206F' ||
                   character is >= '\u3000' and <= '\u303F' ||
                   character is >= '\u3400' and <= '\u4DBF' ||
                   character is >= '\u4E00' and <= '\u9FFF' ||
                   character is >= '\uFF00' and <= '\uFFEF';
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
            return camera;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void CreateDesktopShell(
            RectTransform root,
            TMP_FontAsset font,
            out Button workAppButton,
            out Button clueNotebookButton,
            out Button taskbarWorkQueueButton,
            out Button taskbarDatabaseButton)
        {
            CreateImage(
                "MainStageShadow",
                root,
                Hex("0C0C0C"),
                new Vector2(0.075f, 0.09f),
                new Vector2(0.925f, 0.91f));

            RectTransform terminalFrame = CreateImage(
                "MWT_TerminalFrame",
                root,
                Hex("3C3C3C"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1500f, 760f),
                new Vector2(0f, -8f));

            RectTransform desktop = CreateImage(
                "DesktopSurface",
                terminalFrame,
                Hex("222222"),
                Vector2.zero,
                Vector2.one,
                new Vector2(-26f, -26f),
                Vector2.zero);

            CreateTitleCountdownBar(desktop, font);

            workAppButton = CreateDesktopShortcutButton(
                desktop,
                font,
                "DesktopButton_WorkApp",
                "工单APP",
                new Vector2(92f, -110f),
                true);
            clueNotebookButton = CreateDesktopShortcutButton(
                desktop,
                font,
                "DesktopButton_ClueNotebook",
                "线索笔记",
                new Vector2(92f, -224f),
                true);

            CreateTitleCenterLogo(desktop, font);
            CreateMentorBubble(desktop, font);
            CreateCustomCursor(desktop, font);
            CreateInputMethodWarning(desktop, font);

            RectTransform taskbar = CreateImage(
                "Taskbar",
                desktop,
                Hex("575757"),
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(0f, 70f),
                new Vector2(0f, 35f));
            CreateWindowsGlyph(taskbar);
            taskbarWorkQueueButton = CreateTaskbarLabel(
                taskbar,
                font,
                "Taskbar_WorkQueue",
                "工单队列",
                new Vector2(120f, 0f),
                Hex("C7E6D8"));
            taskbarDatabaseButton = CreateTaskbarLabel(
                taskbar,
                font,
                "Taskbar_Database",
                "资料库",
                new Vector2(365f, 0f),
                Hex("D0D0D0"));
            CreateRiskIndicator(taskbar, font);
            CreateClock(taskbar, font);

            CreateImage(
                "GlobalStatusBar",
                root,
                Hex("6A6A6A"),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(600f, 36f),
                new Vector2(0f, 112f));
        }

        private static RectTransform CreateTicketAppWindow(
            RectTransform root,
            TMP_FontAsset font,
            out Button ticketAppCloseButton,
            out TMP_Text progressText,
            out TMP_Text statusText,
            out TMP_Text evidenceText,
            out TMP_Text resolvedText,
            out RectTransform queueContent,
            out Level1TicketQueueItemView queueItemTemplate,
            out TMP_Text titleText,
            out TMP_Text metaText,
            out TMP_Text ticketIdText,
            out GameObject dataPanel,
            out TMP_Text profileText,
            out TMP_Text historyText,
            out TMP_Text deviceLogText,
            out TMP_Text regionStatusText,
            out ScrollRect dataScrollRect,
            out ScrollRect chatScrollRect,
            out RectTransform chatContent,
            out GameObject chatBubbleTemplate,
            out GameObject chatEmptyState,
            out TMP_Text userMessageText,
            out TMP_Text aiReplyText,
            out Button primaryActionButton,
            out Button dataLookupButton,
            out Button transferHumanButton,
            out Button saveEvidenceButton,
            out Button chatEvidenceActionButton,
            out Button markResolvedButton,
            out Image markResolvedHoldFill)
        {
            RectTransform window = CreateImage(
                "TicketAppWindow",
                root,
                Hex("A7A7A7"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1280f, 740f),
                new Vector2(0f, -10f));
            Outline outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("E2E2E2");
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform titleBar = CreateImage(
                "TicketAppTitleBar",
                window,
                Hex("D3D3D3"),
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 40f),
                new Vector2(0f, -20f));
            CreateText(
                "Txt_AppTitle",
                titleBar,
                font,
                "MWT工单app",
                17f,
                FontStyles.Bold,
                Hex("303030"),
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 0f),
                new Vector2(-40f, 0f));
            CreateText(
                "Txt_ClosePlaceholder",
                titleBar,
                font,
                "×",
                24f,
                FontStyles.Bold,
                Hex("606060"),
                TextAlignmentOptions.Center,
                new Vector2(1f, 0f),
                Vector2.one,
                new Vector2(-36f, 0f),
                Vector2.zero);
            Transform closePlaceholder = titleBar.Find("Txt_ClosePlaceholder");
            if (closePlaceholder != null)
            {
                closePlaceholder.gameObject.SetActive(false);
            }

            ticketAppCloseButton = CreateButton(
                "Btn_TicketAppClose",
                titleBar,
                font,
                "X",
                Hex("D3D3D3"),
                Hex("C4C4C4"),
                Hex("606060"),
                new Vector2(1f, 0.5f),
                new Vector2(36f, 40f));
            ticketAppCloseButton.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(-18f, 0f);

            RectTransform queuePanel = CreateImage(
                "TicketQueuePanel",
                window,
                Hex("777777"),
                new Vector2(0f, 0f),
                new Vector2(0.29f, 1f),
                new Vector2(0f, -58f),
                new Vector2(0f, -69f));
            queuePanel.offsetMin = new Vector2(0f, 0f);
            queuePanel.offsetMax = new Vector2(0f, -40f);
            CreateText(
                "Txt_QueueTitle",
                queuePanel,
                font,
                "工单队列",
                18f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(16f, -40f),
                new Vector2(-12f, 0f));
            RectTransform queueViewport = CreateImage(
                "TicketQueueViewport",
                queuePanel,
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero,
                Vector2.one);
            queueViewport.offsetMin = new Vector2(18f, 18f);
            queueViewport.offsetMax = new Vector2(-18f, -58f);
            queueViewport.gameObject.AddComponent<RectMask2D>();
            queueContent = CreateRect(
                "TicketQueueContent",
                queueViewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);
            queueContent.pivot = new Vector2(0.5f, 1f);
            ScrollRect queueScrollRect = queuePanel.gameObject.AddComponent<ScrollRect>();
            queueScrollRect.viewport = queueViewport;
            queueScrollRect.content = queueContent;
            queueScrollRect.horizontal = false;
            queueScrollRect.vertical = true;
            queueScrollRect.scrollSensitivity = 32f;
            queueScrollRect.movementType = ScrollRect.MovementType.Clamped;
            queueItemTemplate = CreateTicketQueueTemplate(queueContent, font);

            RectTransform infoPanel = CreateImage(
                "TicketInfoPanel",
                window,
                Hex("A0A0A0"),
                new Vector2(0.29f, 0.7f),
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            infoPanel.offsetMin = new Vector2(0f, 0f);
            infoPanel.offsetMax = new Vector2(0f, -40f);
            titleText = CreateText(
                "Txt_TicketTitle",
                infoPanel,
                font,
                "工单类型：具体工单内容",
                23f,
                FontStyles.Bold,
                Hex("555555"),
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.38f),
                Vector2.one,
                new Vector2(18f, 8f),
                new Vector2(-18f, -14f));
            metaText = CreateText(
                "Txt_TicketMeta",
                infoPanel,
                font,
                "用户：--    用户情绪：--\n区域：--",
                16f,
                FontStyles.Bold,
                Hex("696969"),
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                new Vector2(0.78f, 0.38f),
                new Vector2(18f, 12f),
                new Vector2(-8f, -6f));
            ticketIdText = CreateText(
                "Txt_TicketId",
                infoPanel,
                font,
                "#T_---",
                16f,
                FontStyles.Bold,
                Hex("E6E6E6"),
                TextAlignmentOptions.BottomRight,
                new Vector2(0.78f, 0f),
                new Vector2(1f, 0.38f),
                new Vector2(0f, 12f),
                new Vector2(-18f, -6f));

            RectTransform hiddenStatusRefs = CreateRect(
                "RuntimeStatusTextRefs",
                infoPanel,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);
            hiddenStatusRefs.gameObject.SetActive(false);
            progressText = CreateHiddenInfoText(hiddenStatusRefs, font, "Txt_TicketProgressRuntime");
            statusText = CreateHiddenInfoText(hiddenStatusRefs, font, "Txt_StatusRuntime");
            evidenceText = CreateHiddenInfoText(hiddenStatusRefs, font, "Txt_EvidenceRuntime");
            resolvedText = CreateHiddenInfoText(hiddenStatusRefs, font, "Txt_ResolvedRuntime");

            CreateChatArea(
                window,
                font,
                out chatScrollRect,
                out chatContent,
                out chatBubbleTemplate,
                out chatEmptyState,
                out userMessageText,
                out aiReplyText);

            CreateProcessModule(
                window,
                font,
                out primaryActionButton,
                out dataLookupButton,
                out transferHumanButton,
                out saveEvidenceButton,
                out chatEvidenceActionButton,
                out markResolvedButton,
                out markResolvedHoldFill);

            dataPanel = CreateEvidenceLibraryPanel(
                window.parent as RectTransform,
                font,
                out dataScrollRect,
                out profileText,
                out historyText,
                out deviceLogText,
                out regionStatusText);

            return window;
        }

        private static Level1TicketQueueItemView CreateTicketQueueTemplate(RectTransform parent, TMP_FontAsset font)
        {
            RectTransform item = CreateImage(
                "QueueItemPlaceholder",
                parent,
                Hex("D9D9D9"),
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 86f),
                Vector2.zero);
            Button button = item.gameObject.AddComponent<Button>();
            Image background = item.GetComponent<Image>();
            button.targetGraphic = background;

            CreateImage(
                "AvatarPlaceholder",
                item,
                Hex("6E7B7B"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(58f, 58f),
                new Vector2(38f, 0f));
            TMP_Text summary = CreateText(
                "Txt_Summary",
                item,
                font,
                "T_D01_001\n导航罚款\n张先生·6-24",
                16f,
                FontStyles.Bold,
                Hex("292929"),
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(78f, 0f),
                new Vector2(-8f, 0f));

            Level1TicketQueueItemView view = item.gameObject.AddComponent<Level1TicketQueueItemView>();
            view.BindReferences(button, background, summary);
            item.gameObject.SetActive(false);
            return view;
        }

        private static void CreateChatArea(
            RectTransform window,
            TMP_FontAsset font,
            out ScrollRect chatScrollRect,
            out RectTransform chatContent,
            out GameObject chatBubbleTemplate,
            out GameObject chatEmptyState,
            out TMP_Text userMessageText,
            out TMP_Text aiReplyText)
        {
            RectTransform chatPanel = CreateImage(
                "ChatPanel",
                window,
                Hex("B0B0B0"),
                new Vector2(0.29f, 0.16f),
                new Vector2(1f, 0.7f),
                new Vector2(0f, 0f),
                Vector2.zero);

            RectTransform viewport = CreateImage(
                "Viewport",
                chatPanel,
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            viewport.offsetMin = new Vector2(18f, 12f);
            viewport.offsetMax = new Vector2(-18f, -12f);
            viewport.gameObject.AddComponent<RectMask2D>();

            chatContent = CreateRect(
                "ChatContent",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                new Vector2(0f, 0f));
            chatContent.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = chatContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            chatScrollRect = chatPanel.gameObject.AddComponent<ScrollRect>();
            chatScrollRect.viewport = viewport;
            chatScrollRect.content = chatContent;
            chatScrollRect.horizontal = false;
            chatScrollRect.vertical = true;
            chatScrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform emptyState = CreateRect(
                "ChatEmptyState",
                chatPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(360f, 150f));
            CreateLogoMark(emptyState);
            CreateText(
                "Txt_EmptyChat",
                emptyState,
                font,
                "请选择左侧待处理工单进入对话",
                18f,
                FontStyles.Bold,
                Hex("858585"),
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, 0f),
                new Vector2(0f, -72f));
            chatEmptyState = emptyState.gameObject;

            chatBubbleTemplate = CreateImage(
                "ChatBubbleTemplate",
                chatContent,
                new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 132f),
                Vector2.zero).gameObject;
            LayoutElement bubbleLayout = chatBubbleTemplate.AddComponent<LayoutElement>();
            bubbleLayout.preferredHeight = 132f;
            RectTransform chatBubbleRect = chatBubbleTemplate.GetComponent<RectTransform>();
            RectTransform leftAvatar = CreateImage(
                "AvatarLeft",
                chatBubbleRect,
                Hex("416AC4"),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(58f, 58f),
                new Vector2(16f, -34f));
            Outline leftAvatarOutline = leftAvatar.gameObject.AddComponent<Outline>();
            leftAvatarOutline.effectColor = Hex("1C376E");
            leftAvatarOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform rightAvatar = CreateImage(
                "AvatarRight",
                chatBubbleRect,
                Hex("111111"),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(58f, 58f),
                new Vector2(-16f, -34f));
            Outline rightAvatarOutline = rightAvatar.gameObject.AddComponent<Outline>();
            rightAvatarOutline.effectColor = Hex("000000");
            rightAvatarOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(
                "Txt_Speaker",
                chatBubbleRect,
                font,
                "路人甲",
                17f,
                FontStyles.Bold,
                Hex("202020"),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(96f, -8f),
                new Vector2(280f, 28f));

            RectTransform bubbleBody = CreateImage(
                "BubbleBody",
                chatBubbleRect,
                Hex("F2F2F2"),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(650f, 82f),
                new Vector2(96f, -46f));
            CreateText(
                "Txt_Bubble",
                bubbleBody,
                font,
                "对话模板",
                20f,
                FontStyles.Normal,
                Hex("222222"),
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(18f, 12f),
                new Vector2(-18f, -10f));
            chatBubbleTemplate.SetActive(false);

            RectTransform hidden = CreateRect(
                "RuntimeChatTextRefs",
                chatPanel,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);
            hidden.gameObject.SetActive(false);
            userMessageText = CreateText(
                "Txt_UserMessageRuntime",
                hidden,
                font,
                string.Empty,
                12f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            aiReplyText = CreateText(
                "Txt_AiReplyRuntime",
                hidden,
                font,
                string.Empty,
                12f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateProcessModule(
            RectTransform window,
            TMP_FontAsset font,
            out Button primaryActionButton,
            out Button dataLookupButton,
            out Button transferHumanButton,
            out Button saveEvidenceButton,
            out Button chatEvidenceActionButton,
            out Button markResolvedButton,
            out Image markResolvedHoldFill)
        {
            RectTransform actionBar = CreateImage(
                "ProcessModule",
                window,
                Hex("D8D8D8"),
                new Vector2(0.29f, 0f),
                new Vector2(1f, 0.16f),
                Vector2.zero,
                Vector2.zero);

            primaryActionButton = CreateButton(
                "Btn_FollowUp",
                actionBar,
                font,
                "追问",
                Hex("35AF6B"),
                Hex("42C87C"),
                PrimaryText,
                new Vector2(0.13f, 0.5f),
                new Vector2(154f, 76f));
            dataLookupButton = CreateButton(
                "Btn_DataLookup",
                actionBar,
                font,
                "资料库查找",
                Hex("526ED0"),
                Hex("6681E8"),
                PrimaryText,
                new Vector2(0.34f, 0.5f),
                new Vector2(210f, 76f));
            transferHumanButton = CreateButton(
                "Btn_TransferHuman",
                actionBar,
                font,
                "转人工",
                Hex("7251A8"),
                Hex("8562C4"),
                PrimaryText,
                new Vector2(0.56f, 0.5f),
                new Vector2(180f, 76f));
            markResolvedButton = CreateButton(
                "Btn_MarkResolved",
                actionBar,
                font,
                "✓  标记已解决",
                Hex("D95757"),
                Hex("EB6767"),
                PrimaryText,
                new Vector2(0.82f, 0.5f),
                new Vector2(290f, 76f));
            RectTransform fill = CreateImage(
                "HoldProgressFill",
                markResolvedButton.GetComponent<RectTransform>(),
                new Color(1f, 1f, 1f, 0.25f),
                Vector2.zero,
                Vector2.one).GetComponent<RectTransform>();
            markResolvedHoldFill = fill.GetComponent<Image>();
            markResolvedHoldFill.type = Image.Type.Filled;
            markResolvedHoldFill.fillMethod = Image.FillMethod.Horizontal;
            markResolvedHoldFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            markResolvedHoldFill.fillAmount = 0f;
            markResolvedHoldFill.raycastTarget = false;
            markResolvedHoldFill.transform.SetAsFirstSibling();

            saveEvidenceButton = CreateButton(
                "Btn_SaveEvidenceHidden",
                actionBar,
                font,
                "保留证据",
                Hex("35AF6B"),
                Hex("42C87C"),
                PrimaryText,
                new Vector2(0.5f, 1.8f),
                new Vector2(1f, 1f));
            saveEvidenceButton.gameObject.SetActive(false);

            chatEvidenceActionButton = CreateButton(
                "Btn_ChatEvidenceActionHidden",
                actionBar,
                font,
                "出示证据",
                Hex("526ED0"),
                Hex("6681E8"),
                PrimaryText,
                new Vector2(0.5f, 1.8f),
                new Vector2(1f, 1f));
            chatEvidenceActionButton.gameObject.SetActive(false);
        }

        private static GameObject CreateEvidenceLibraryPanel(
            RectTransform root,
            TMP_FontAsset font,
            out ScrollRect dataScrollRect,
            out TMP_Text profileText,
            out TMP_Text historyText,
            out TMP_Text deviceLogText,
            out TMP_Text regionStatusText)
        {
            RectTransform panel = CreateImage(
                "EvidenceLibraryPanel",
                root,
                Hex("575757"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(320f, 740f),
                new Vector2(800f, -10f));
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("E2E2E2");
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform viewport = CreateImage(
                "EvidenceViewport",
                panel,
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero,
                Vector2.one,
                new Vector2(-18f, -18f),
                Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(
                "EvidenceContent",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                new Vector2(0f, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            dataScrollRect = panel.gameObject.AddComponent<ScrollRect>();
            dataScrollRect.viewport = viewport;
            dataScrollRect.content = content;
            dataScrollRect.horizontal = false;
            dataScrollRect.vertical = true;
            dataScrollRect.scrollSensitivity = 32f;
            dataScrollRect.movementType = ScrollRect.MovementType.Clamped;

            profileText = CreateEvidenceCard(
                content,
                font,
                "EvidenceCard_Profile",
                "资料01:",
                "东区3号楼/22:14断链/\n自动重连失败，近夜间\n多次门禁短断");
            historyText = CreateEvidenceCard(
                content,
                font,
                "EvidenceCard_History",
                "资料02:",
                "查看资料详情\n收集此资料");
            deviceLogText = CreateEvidenceCard(
                content,
                font,
                "EvidenceCard_Device",
                "资料03:",
                "东区3号楼/22:14断链/\n自动重连失败，近夜间\n多次门禁短断");
            regionStatusText = CreateEvidenceCard(
                content,
                font,
                "EvidenceCard_Region",
                "资料04:",
                "东区3号楼/22:14断链/\n自动重连失败，近夜间\n多次门禁短断");

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        private static TMP_Text CreateEvidenceCard(
            RectTransform parent,
            TMP_FontAsset font,
            string name,
            string title,
            string body)
        {
            RectTransform card = CreateImage(
                name,
                parent,
                Hex("C9C9C9"),
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 220f),
                Vector2.zero);
            LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 220f;
            Button cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = card.GetComponent<Image>();

            TMP_Text titleText = CreateText(
                "Txt_Title",
                card,
                font,
                title,
                24f,
                FontStyles.Bold,
                Hex("5A5A5A"),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(18f, -52f),
                new Vector2(-14f, -12f));
            titleText.overflowMode = TextOverflowModes.Truncate;

            TMP_Text bodyText = CreateText(
                "Txt_Body",
                card,
                font,
                body,
                18f,
                FontStyles.Normal,
                Hex("5A5A5A"),
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(18f, 14f),
                new Vector2(-18f, -66f));
            bodyText.overflowMode = TextOverflowModes.Truncate;

            RectTransform actionRow = CreateRect(
                "ActionRow",
                card,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 42f),
                new Vector2(0f, 72f));
            actionRow.offsetMin = new Vector2(10f, 8f);
            actionRow.offsetMax = new Vector2(-10f, 80f);
            VerticalLayoutGroup actionLayout = actionRow.gameObject.AddComponent<VerticalLayoutGroup>();
            actionLayout.spacing = 6f;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = false;
            CreateEvidenceActionButton(
                "Btn_ViewDetail",
                actionRow,
                font,
                "查看资料详情",
                "▣");
            CreateEvidenceActionButton(
                "Btn_CollectEvidence",
                actionRow,
                font,
                "收集此资料",
                "↓");
            actionRow.gameObject.SetActive(false);

            return bodyText;
        }

        private static void CreateEvidenceDetailOverlay(
            RectTransform root,
            TMP_FontAsset font,
            out GameObject detailOverlay,
            out TMP_Text detailTitleText,
            out TMP_Text detailBodyText,
            out Button detailCloseButton)
        {
            RectTransform overlay = CreateImage(
                "EvidenceDetailOverlay",
                root,
                new Color(0f, 0f, 0f, 0.76f),
                Vector2.zero,
                Vector2.one);
            detailOverlay = overlay.gameObject;
            overlay.gameObject.SetActive(false);

            RectTransform card = CreateImage(
                "EvidenceDetailCard",
                overlay,
                Hex("D5D5D5"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(760f, 560f),
                Vector2.zero);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("EEEEEE");
            outline.effectDistance = new Vector2(2f, -2f);

            detailTitleText = CreateText(
                "Txt_EvidenceDetailTitle",
                card,
                font,
                "\u8d44\u659901",
                28f,
                FontStyles.Bold,
                Hex("303030"),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(28f, -70f),
                new Vector2(-96f, -18f));

            Button closeButton = CreateButton(
                "Btn_CloseEvidenceDetail",
                card,
                font,
                "\u00d7",
                Hex("4A4A4A"),
                Hex("666666"),
                PrimaryText,
                new Vector2(1f, 1f),
                new Vector2(58f, 58f));
            detailCloseButton = closeButton;
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchoredPosition = new Vector2(-42f, -38f);

            RectTransform viewport = CreateImage(
                "EvidenceDetailViewport",
                card,
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero,
                Vector2.one);
            viewport.offsetMin = new Vector2(30f, 34f);
            viewport.offsetMax = new Vector2(-30f, -94f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(
                "EvidenceDetailContent",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI bodyText = CreateText(
                "Txt_EvidenceDetailBody",
                content,
                font,
                "\u8d44\u6599\u8be6\u60c5",
                22f,
                FontStyles.Bold,
                Hex("3A3A3A"),
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            detailBodyText = bodyText;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            bodyText.raycastTarget = false;
            LayoutElement bodyLayout = bodyText.gameObject.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 420f;

            ScrollRect scrollRect = card.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 34f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private static Button CreateEvidenceActionButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            string icon)
        {
            RectTransform rect = CreateImage(
                name,
                parent,
                Hex("E6E6E6"),
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(0f, 30f),
                Vector2.zero);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 30f;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            CreateText(
                "Txt_Label",
                rect,
                font,
                label,
                14f,
                FontStyles.Bold,
                Hex("5A5A5A"),
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 0f),
                new Vector2(-42f, 0f));
            CreateText(
                "Txt_Icon",
                rect,
                font,
                icon,
                22f,
                FontStyles.Bold,
                Hex("5A5A5A"),
                TextAlignmentOptions.Center,
                new Vector2(1f, 0f),
                Vector2.one,
                new Vector2(-42f, 0f),
                Vector2.zero);
            return button;
        }

        private static TMP_Text CreateHiddenInfoText(RectTransform parent, TMP_FontAsset font, string name)
        {
            RectTransform rect = CreateRect(
                name + "Root",
                parent,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);
            rect.gameObject.SetActive(false);
            return CreateText(
                name,
                rect,
                font,
                string.Empty,
                12f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
        }

        private static TMP_Text CreateStatusText(
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            return CreateText(
                "Txt_Status_" + label,
                parent,
                font,
                label,
                13f,
                FontStyles.Bold,
                Hex("646464"),
                TextAlignmentOptions.MidlineLeft,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateTitleCountdownBar(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform track = CreateImage(
                "CountdownTrack",
                desktop,
                Hex("777777"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(700f, 22f),
                new Vector2(0f, -18f));

            CreateImage(
                "CountdownElapsed",
                track,
                Hex("E04D73"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(220f, 14f),
                new Vector2(111f, 0f));

            CreateImage(
                "CountdownTail",
                track,
                new Color(0.82f, 0.82f, 0.82f, 0.75f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(64f, 3f),
                new Vector2(-34f, 0f));

            CreateText(
                "Txt_Countdown",
                track,
                font,
                "T-72h",
                18f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateTitleCenterLogo(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform logoRoot = CreateRect(
                "LogoGroup",
                desktop,
                new Vector2(0.5f, 0.58f),
                new Vector2(0.5f, 0.58f),
                Vector2.zero,
                new Vector2(620f, 170f));

            CreateLogoMark(logoRoot);
            CreateText(
                "Txt_MingBay",
                logoRoot,
                font,
                "明 湾 通",
                62f,
                FontStyles.Bold,
                new Color(0.72f, 0.72f, 0.72f, 0.34f),
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.38f),
                Vector2.one,
                new Vector2(82f, 0f),
                Vector2.zero);
            CreateText(
                "Txt_TerminalVersion",
                logoRoot,
                font,
                "MWT-TERMINAL v2.7.1",
                26f,
                FontStyles.Bold,
                new Color(0.72f, 0.72f, 0.72f, 0.28f),
                TextAlignmentOptions.Center,
                Vector2.zero,
                new Vector2(1f, 0.42f),
                new Vector2(82f, 0f),
                Vector2.zero);
        }

        private static void CreateLogoMark(RectTransform logoRoot)
        {
            int[,] pattern =
            {
                {0, 1, 1, 1, 0},
                {1, 0, 0, 0, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 0, 0, 1},
                {0, 1, 1, 1, 0}
            };

            RectTransform mark = CreateRect(
                "LogoPixelMark",
                logoRoot,
                new Vector2(0f, 0.44f),
                new Vector2(0f, 0.44f),
                new Vector2(72f, 24f),
                new Vector2(78f, 78f));

            const float cell = 11f;
            const float gap = 3f;
            for (int row = 0; row < 5; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if (pattern[row, column] == 0)
                    {
                        continue;
                    }

                    CreateImage(
                        $"LogoCell_{column}_{row}",
                        mark,
                        new Color(0.72f, 0.72f, 0.72f, 0.28f),
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(cell, cell),
                        new Vector2(12f + column * (cell + gap), -12f - row * (cell + gap)));
                }
            }
        }

        private static void CreateMentorBubble(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform bubble = CreateImage(
                "MentorBubble",
                desktop,
                Hex("D0D0D0"),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(520f, 96f),
                new Vector2(215f, 130f));

            RectTransform avatar = CreateImage(
                "MentorAvatar",
                bubble,
                Hex("242424"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(66f, 66f),
                new Vector2(48f, 0f));

            CreateText(
                "Txt_AvatarFace",
                avatar,
                font,
                "A07",
                16f,
                FontStyles.Bold,
                MutedText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 14f));
            CreateText(
                "Txt_AvatarName",
                avatar,
                font,
                "KKK",
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Bottom,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, -4f));
            CreateText(
                "Txt_MentorHint",
                bubble,
                font,
                "先点击工单APP，查看今日任务",
                20f,
                FontStyles.Bold,
                Hex("181818"),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(110f, 0f),
                new Vector2(-26f, 0f));
        }

        private static void CreateCustomCursor(RectTransform desktop, TMP_FontAsset font)
        {
            CreateText(
                "Txt_CustomCursor",
                desktop,
                font,
                "↖",
                34f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0.72f, 0.36f),
                new Vector2(0.72f, 0.36f),
                new Vector2(-22f, -22f),
                new Vector2(22f, 22f));
        }

        private static void CreateInputMethodWarning(RectTransform desktop, TMP_FontAsset font)
        {
            RectTransform inputPanel = CreateImage(
                "InputMethod_A07Warning",
                desktop,
                Hex("8C8C8C"),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(180f, 38f),
                new Vector2(-114f, 112f));

            Outline outline = inputPanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.88f, 0.30f, 0.45f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);

            CreateImage(
                "AbnormalCaret",
                inputPanel,
                Hex("E04D73"),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(14f, 3f),
                new Vector2(-10f, 0f));
            CreateText(
                "Txt_InputState",
                inputPanel,
                font,
                "中",
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 0f),
                new Vector2(-12f, 0f));
        }

        private static void CreateWindowsGlyph(RectTransform taskbar)
        {
            RectTransform glyph = CreateRect(
                "SystemGlyph",
                taskbar,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(38f, 0f),
                new Vector2(28f, 28f));

            Color glyphColor = new Color(0.82f, 0.91f, 0.94f, 0.95f);
            CreateImage("Pane01", glyph, glyphColor, new Vector2(0f, 0.52f), new Vector2(0.44f, 1f));
            CreateImage("Pane02", glyph, glyphColor, new Vector2(0.52f, 0.52f), Vector2.one);
            CreateImage("Pane03", glyph, glyphColor, Vector2.zero, new Vector2(0.44f, 0.44f));
            CreateImage("Pane04", glyph, glyphColor, new Vector2(0.52f, 0f), new Vector2(1f, 0.44f));
        }

        private static Button CreateDesktopShortcutButton(
            RectTransform parent,
            TMP_FontAsset font,
            string name,
            string label,
            Vector2 anchoredPosition,
            bool interactable)
        {
            RectTransform shortcut = CreateRect(
                name + "_Group",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                new Vector2(96f, 108f));

            RectTransform iconBox = CreateImage(
                name,
                shortcut,
                interactable ? Hex("303030") : Hex("8A8A8A"),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(58f, 58f),
                new Vector2(0f, -32f));

            Outline outline = iconBox.gameObject.AddComponent<Outline>();
            outline.effectColor = interactable
                ? new Color(0.86f, 0.86f, 0.86f, 0.5f)
                : new Color(0.86f, 0.86f, 0.86f, 0.18f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = iconBox.gameObject.AddComponent<Button>();
            button.targetGraphic = iconBox.GetComponent<Image>();
            button.interactable = interactable;
            ApplyDesktopButtonColors(button, interactable);

            CreateText(
                "Txt_Icon",
                iconBox,
                font,
                "icon",
                13f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            CreateText(
                "Txt_Label",
                shortcut,
                font,
                label,
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Top,
                Vector2.zero,
                new Vector2(1f, 0.34f),
                Vector2.zero,
                Vector2.zero);

            return button;
        }

        private static void ApplyDesktopButtonColors(Button button, bool interactable)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = interactable ? Hex("303030") : Hex("8A8A8A");
            colors.highlightedColor = interactable ? Hex("464646") : Hex("8A8A8A");
            colors.pressedColor = interactable ? Hex("5A5A5A") : Hex("8A8A8A");
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Hex("8A8A8A");
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Button CreateTaskbarLabel(
            RectTransform taskbar,
            TMP_FontAsset font,
            string objectName,
            string label,
            Vector2 position,
            Color background)
        {
            RectTransform buttonRect = CreateImage(
                objectName,
                taskbar,
                background,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(210f, 42f),
                position);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonRect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            RectTransform miniIcon = CreateImage(
                "MiniIcon",
                buttonRect,
                Hex("EFEFEF"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 28f),
                new Vector2(22f, 0f));
            CreateText(
                "Txt_MiniIcon",
                miniIcon,
                font,
                "icon",
                8f,
                FontStyles.Bold,
                Hex("202020"),
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            CreateText(
                "Txt_Label",
                buttonRect,
                font,
                label,
                14f,
                FontStyles.Bold,
                Hex("222222"),
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(58f, 0f),
                new Vector2(-8f, 0f));

            return button;
        }

        private static void CreateRiskIndicator(RectTransform taskbar, TMP_FontAsset font)
        {
            RectTransform risk = CreateRect(
                "A07RiskIndicator",
                taskbar,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-330f, 0f),
                new Vector2(250f, 46f));
            CreateImage(
                "WarningIcon",
                risk,
                Hex("F08B91"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(30f, 30f),
                new Vector2(15f, 0f));
            CreateText(
                "Txt_Risk",
                risk,
                font,
                "A07 异常指示",
                13f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(48f, 2f),
                Vector2.zero);
            RectTransform track = CreateImage(
                "RiskTrack",
                risk,
                Hex("2F2F2F"),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(136f, 10f),
                new Vector2(118f, 11f));
            for (int index = 0; index < 11; index++)
            {
                CreateImage(
                    "RiskSegment_" + index,
                    track,
                    index < 8 ? Hex("E04D73") : Hex("747474"),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(8f, 10f),
                    new Vector2(6f + index * 11f, 0f));
            }
        }

        private static void CreateClock(RectTransform taskbar, TMP_FontAsset font)
        {
            RectTransform clock = CreateImage(
                "ClockPanel",
                taskbar,
                Hex("3B3B3B"),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(144f, 42f),
                new Vector2(-92f, 0f));
            CreateText(
                "Txt_Clock",
                clock,
                font,
                "下午9:10",
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 7f));
        }

        private static void CreateTopBar(
            RectTransform root,
            TMP_FontAsset font,
            out TMP_Text progressText,
            out TMP_Text statusText,
            out TMP_Text evidenceText,
            out TMP_Text resolvedText)
        {
            RectTransform topBar = CreateImage(
                "TopBar",
                root,
                TopBar,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 82f),
                new Vector2(0f, -41f));

            CreateImage(
                "AccentLine",
                topBar,
                Accent,
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2(0f, 2f),
                Vector2.zero);

            progressText = CreateText(
                "Txt_TicketProgress",
                topBar,
                font,
                "工单 1 / 1  ·  T_001",
                22f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                new Vector2(0.3f, 1f),
                new Vector2(36f, 0f),
                Vector2.zero);

            statusText = CreateText(
                "Txt_Status",
                topBar,
                font,
                "状态：等待核验",
                19f,
                FontStyles.Normal,
                Accent,
                TextAlignmentOptions.Center,
                new Vector2(0.3f, 0f),
                new Vector2(0.68f, 1f),
                Vector2.zero,
                Vector2.zero);

            evidenceText = CreateText(
                "Txt_EvidenceCount",
                topBar,
                font,
                "证据  0",
                18f,
                FontStyles.Bold,
                Warning,
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.68f, 0f),
                new Vector2(0.84f, 1f),
                Vector2.zero,
                new Vector2(-18f, 0f));

            resolvedText = CreateText(
                "Txt_ResolvedCount",
                topBar,
                font,
                "已解决  0",
                18f,
                FontStyles.Bold,
                MutedText,
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.84f, 0f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-36f, 0f));
        }

        private static void CreateQueuePanel(
            RectTransform root,
            TMP_FontAsset font,
            out RectTransform queueContent,
            out Level1TicketQueueItemView queueItemTemplate)
        {
            RectTransform panel = CreatePanel(
                "TicketQueuePanel",
                root,
                new Vector2(0.02f, 0.1f),
                new Vector2(0.18f, 0.9f));

            CreateText(
                "Txt_QueueTitle",
                panel,
                font,
                "工单队列",
                25f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.88f),
                Vector2.one,
                new Vector2(24f, 0f),
                new Vector2(-18f, 0f));

            queueContent = CreateRect(
                "QueueContent",
                panel,
                new Vector2(0.05f, 0.16f),
                new Vector2(0.95f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            queueContent.pivot = new Vector2(0.5f, 1f);

            RectTransform queueItem = CreateImage(
                "QueueItemTemplate",
                queueContent,
                PanelRaised,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, 90f),
                Vector2.zero);
            queueItem.pivot = new Vector2(0.5f, 1f);
            Button queueButton = queueItem.gameObject.AddComponent<Button>();
            ColorBlock colors = queueButton.colors;
            colors.normalColor = PanelRaised;
            colors.highlightedColor = AccentHover;
            colors.pressedColor = Accent;
            colors.selectedColor = AccentHover;
            colors.disabledColor = PanelDark;
            queueButton.colors = colors;

            CreateImage(
                "TicketIconPlaceholder",
                queueItem,
                Accent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(42f, 42f),
                new Vector2(34f, 0f));

            TMP_Text summaryText = CreateText(
                "Txt_QueueSummary",
                queueItem,
                font,
                "T_001\n工单类型",
                17f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.24f, 0f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-12f, 0f));

            queueItemTemplate = queueItem.gameObject.AddComponent<Level1TicketQueueItemView>();
            SerializedObject serializedItem = new(queueItemTemplate);
            SetObject(serializedItem, "button", queueButton);
            SetObject(serializedItem, "backgroundImage", queueItem.GetComponent<Image>());
            SetObject(serializedItem, "summaryText", summaryText);
            serializedItem.FindProperty("normalColor").colorValue = PanelRaised;
            serializedItem.FindProperty("selectedColor").colorValue = Accent;
            serializedItem.FindProperty("processedColor").colorValue = PanelDark;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            queueItem.gameObject.SetActive(false);

            CreateText(
                "Txt_QueueHint",
                panel,
                font,
                "点击待处理工单查看内容。\n已处理工单不可重复操作。",
                16f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.05f),
                new Vector2(1f, 0.62f),
                new Vector2(24f, 0f),
                new Vector2(-24f, 0f));
        }

        private static void CreateTicketPanel(
            RectTransform root,
            TMP_FontAsset font,
            out TMP_Text titleText,
            out TMP_Text metaText,
            out TMP_Text userMessageText,
            out TMP_Text aiReplyText)
        {
            RectTransform panel = CreatePanel(
                "TicketPanel",
                root,
                new Vector2(0.195f, 0.28f),
                new Vector2(0.645f, 0.9f));

            titleText = CreateText(
                "Txt_TicketTitle",
                panel,
                font,
                "门禁故障：整层居民无法进入",
                30f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.84f),
                new Vector2(0.72f, 1f),
                new Vector2(30f, 0f),
                Vector2.zero);

            metaText = CreateText(
                "Txt_TicketMeta",
                panel,
                font,
                "用户：林女士\n区域：明湾旧区 · 7号楼\n类型：门禁异常\n等待 01:42:18",
                16f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.TopRight,
                new Vector2(0.7f, 0.82f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-28f, -16f));

            CreateImage(
                "HeaderDivider",
                panel,
                Border,
                new Vector2(0f, 0.8f),
                new Vector2(1f, 0.8f),
                new Vector2(-60f, 1f),
                Vector2.zero);

            RectTransform userBubble = CreateImage(
                "UserMessagePanel",
                panel,
                PanelRaised,
                new Vector2(0.04f, 0.47f),
                new Vector2(0.9f, 0.75f));

            CreateImage(
                "UserAvatarPlaceholder",
                userBubble,
                Warning,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(54f, 54f),
                new Vector2(34f, -34f));

            CreateText(
                "Txt_UserLabel",
                userBubble,
                font,
                "居民求助",
                16f,
                FontStyles.Bold,
                Warning,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.12f, 0.7f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);

            userMessageText = CreateText(
                "Txt_UserMessage",
                userBubble,
                font,
                "我们整层楼从昨晚开始都打不开门禁，不是我一个人的手机问题。",
                20f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.12f, 0.05f),
                new Vector2(0.96f, 0.72f),
                Vector2.zero,
                Vector2.zero);

            RectTransform aiBubble = CreateImage(
                "AiMessagePanel",
                panel,
                PanelDark,
                new Vector2(0.1f, 0.12f),
                new Vector2(0.96f, 0.4f));

            CreateImage(
                "AiAvatarPlaceholder",
                aiBubble,
                Accent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(54f, 54f),
                new Vector2(34f, -34f));

            CreateText(
                "Txt_AiLabel",
                aiBubble,
                font,
                "明湾通 AI",
                16f,
                FontStyles.Bold,
                Accent,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.12f, 0.7f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);

            aiReplyText = CreateText(
                "Txt_AiReply",
                aiBubble,
                font,
                "检测结果显示门禁服务运行正常。请确认手机蓝牙权限已开启。",
                20f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.12f, 0.05f),
                new Vector2(0.96f, 0.72f),
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateDataArea(
            RectTransform root,
            TMP_FontAsset font,
            out GameObject dataPanel,
            out TMP_Text profileText,
            out TMP_Text historyText,
            out TMP_Text deviceLogText,
            out TMP_Text regionStatusText)
        {
            RectTransform lockedPanel = CreatePanel(
                "DataLockedPlaceholder",
                root,
                new Vector2(0.66f, 0.28f),
                new Vector2(0.98f, 0.9f));

            CreateImage(
                "DataIconPlaceholder",
                lockedPanel,
                MutedText,
                new Vector2(0.5f, 0.62f),
                new Vector2(0.5f, 0.62f),
                new Vector2(92f, 92f),
                Vector2.zero);

            CreateText(
                "Txt_DataLocked",
                lockedPanel,
                font,
                "资料尚未展开",
                28f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.42f),
                new Vector2(1f, 0.57f),
                Vector2.zero,
                Vector2.zero);

            CreateText(
                "Txt_DataLockedHint",
                lockedPanel,
                font,
                "查看用户资料、历史工单、设备日志与区域状态，\n核验 AI 回复是否可信。",
                17f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.Top,
                new Vector2(0.08f, 0.23f),
                new Vector2(0.92f, 0.42f),
                Vector2.zero,
                Vector2.zero);

            RectTransform dataRect = CreatePanel(
                "DataPanel",
                root,
                new Vector2(0.66f, 0.28f),
                new Vector2(0.98f, 0.9f));
            dataPanel = dataRect.gameObject;

            CreateText(
                "Txt_DataTitle",
                dataRect,
                font,
                "核验资料",
                26f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.9f),
                Vector2.one,
                new Vector2(24f, 0f),
                new Vector2(-20f, 0f));

            profileText = CreateDataCard(dataRect, font, "资料01 用户资料", new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.88f));
            historyText = CreateDataCard(dataRect, font, "资料02 历史工单", new Vector2(0.04f, 0.47f), new Vector2(0.96f, 0.66f));
            deviceLogText = CreateDataCard(dataRect, font, "资料03 AI建议", new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.44f));
            regionStatusText = CreateDataCard(dataRect, font, "资料04 系统日志", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.22f));

            dataPanel.SetActive(false);
        }

        private static TMP_Text CreateDataCard(
            RectTransform parent,
            TMP_FontAsset font,
            string title,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform card = CreateImage(title + "Card", parent, PanelRaised, anchorMin, anchorMax);

            CreateImage(
                title + "IconPlaceholder",
                card,
                Accent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, 28f),
                new Vector2(22f, -22f));

            CreateText(
                "Txt_" + title + "Title",
                card,
                font,
                title,
                16f,
                FontStyles.Bold,
                Accent,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.12f, 0.67f),
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return CreateText(
                "Txt_" + title,
                card,
                font,
                "占位资料",
                14f,
                FontStyles.Normal,
                PrimaryText,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.05f, 0.04f),
                new Vector2(0.95f, 0.68f),
                Vector2.zero,
                Vector2.zero);
        }

        private static void CreateActionBar(
            RectTransform root,
            TMP_FontAsset font,
            out Button primaryActionButton,
            out Button transferHumanButton,
            out Button saveEvidenceButton,
            out Button markResolvedButton)
        {
            RectTransform bar = CreatePanel(
                "ActionBar",
                root,
                new Vector2(0.195f, 0.08f),
                new Vector2(0.98f, 0.24f));

            CreateText(
                "Txt_ActionHint",
                bar,
                font,
                "先核验资料，再选择追问、转人工、保留证据或标记已解决。",
                16f,
                FontStyles.Normal,
                MutedText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.02f, 0.65f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);

            primaryActionButton = CreateButton(
                "Btn_ViewData",
                bar,
                font,
                "查看资料",
                Accent,
                AccentHover,
                Background,
                new Vector2(0.13f, 0.34f),
                new Vector2(250f, 62f));

            transferHumanButton = CreateButton(
                "Btn_TransferHuman",
                bar,
                font,
                "转人工",
                Warning,
                Hex("B0B0B0"),
                Background,
                new Vector2(0.38f, 0.34f),
                new Vector2(250f, 62f));

            saveEvidenceButton = CreateButton(
                "Btn_SaveEvidence",
                bar,
                font,
                "保留证据",
                Accent,
                AccentHover,
                Background,
                new Vector2(0.63f, 0.34f),
                new Vector2(250f, 62f));

            markResolvedButton = CreateButton(
                "Btn_MarkResolved",
                bar,
                font,
                "标记已解决",
                PanelRaised,
                Hex("5A5A5A"),
                PrimaryText,
                new Vector2(0.87f, 0.34f),
                new Vector2(250f, 62f));
        }

        private static void CreateNotebookPanel(
            RectTransform root,
            TMP_FontAsset font,
            out GameObject notebookPanel,
            out TMP_Text notebookReasonText,
            out TMP_Text notebookUserText,
            out TMP_Text notebookEmotionText,
            out TMP_Text notebookRegionText,
            out TMP_Text notebookTicketIdText,
            out Button[] notebookEvidenceButtons,
            out TMP_Text[] notebookEvidenceTexts,
            out Outline[] notebookEvidenceOutlines,
            out Button notebookCloseButton,
            out Button notebookCancelButton,
            out Button notebookSubmitButton)
        {
            RectTransform overlay = CreateImage(
                "NotebookPanel",
                root,
                new Color(0f, 0f, 0f, 0.72f),
                Vector2.zero,
                Vector2.one);
            notebookPanel = overlay.gameObject;

            notebookCloseButton = CreateButton(
                "Btn_NotebookClose",
                overlay,
                font,
                "×",
                new Color(0f, 0f, 0f, 0f),
                new Color(1f, 1f, 1f, 0.12f),
                PrimaryText,
                new Vector2(0.96f, 0.92f),
                new Vector2(64f, 64f));

            RectTransform book = CreateRect(
                "NotebookBook",
                overlay,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1100f, 660f));

            RectTransform leftPage = CreateImage(
                "NotebookLeftPage",
                book,
                Hex("BDBDBD"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(512f, 620f),
                new Vector2(256f, 0f));
            Outline leftOutline = leftPage.gameObject.AddComponent<Outline>();
            leftOutline.effectColor = new Color(0f, 0f, 0f, 0.34f);
            leftOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform rightPage = CreateImage(
                "NotebookRightPage",
                book,
                Hex("C7C7C7"),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(512f, 620f),
                new Vector2(-256f, 0f));
            Outline rightOutline = rightPage.gameObject.AddComponent<Outline>();
            rightOutline.effectColor = new Color(0f, 0f, 0f, 0.34f);
            rightOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform spine = CreateImage(
                "NotebookSpine",
                book,
                Hex("222222"),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(46f, 622f),
                Vector2.zero);
            for (int index = 0; index < 18; index++)
            {
                CreateImage(
                    $"Spiral_{index:00}",
                    spine,
                    Hex("0E0E0E"),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(38f, 10f),
                    new Vector2(0f, -20f - index * 34f));
            }

            RectTransform avatar = CreateImage(
                "NotebookUserIcon",
                leftPage,
                Hex("6E7B7B"),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(118f, 118f),
                new Vector2(106f, -92f));
            CreateText(
                "Txt_NotebookUserIcon",
                avatar,
                font,
                "icon",
                22f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            notebookReasonText = CreateText(
                "Txt_NotebookReason",
                leftPage,
                font,
                "工单原因：--",
                19f,
                FontStyles.Bold,
                Hex("222222"),
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(54f, 280f),
                new Vector2(-42f, -210f));
            notebookUserText = CreateNotebookInfoText(
                "Txt_NotebookUser",
                leftPage,
                font,
                "用户：--",
                new Vector2(54f, 218f));
            notebookEmotionText = CreateNotebookInfoText(
                "Txt_NotebookEmotion",
                leftPage,
                font,
                "用户情绪：--",
                new Vector2(54f, 178f));
            notebookRegionText = CreateNotebookInfoText(
                "Txt_NotebookRegion",
                leftPage,
                font,
                "区域：--",
                new Vector2(54f, 138f));
            notebookTicketIdText = CreateText(
                "Txt_NotebookTicketId",
                leftPage,
                font,
                "#T_---",
                18f,
                FontStyles.Bold,
                Hex("4B4B4B"),
                TextAlignmentOptions.BottomRight,
                Vector2.zero,
                Vector2.one,
                new Vector2(40f, 40f),
                new Vector2(-46f, -40f));

            CreateText(
                "Txt_NotebookEvidenceTitle",
                rightPage,
                font,
                "请选择出示你认为与AI处理意见\n相悖的证据",
                22f,
                FontStyles.Bold,
                Hex("303030"),
                TextAlignmentOptions.Center,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(48f, -118f),
                new Vector2(-48f, -30f));

            notebookEvidenceButtons = new Button[4];
            notebookEvidenceTexts = new TMP_Text[4];
            notebookEvidenceOutlines = new Outline[4];
            Vector2[] evidencePositions =
            {
                new(150f, -230f),
                new(368f, -230f),
                new(150f, -376f),
                new(368f, -376f)
            };
            for (int index = 0; index < notebookEvidenceButtons.Length; index++)
            {
                CreateNotebookEvidenceButton(
                    rightPage,
                    font,
                    index,
                    evidencePositions[index],
                    out notebookEvidenceButtons[index],
                    out notebookEvidenceTexts[index],
                    out notebookEvidenceOutlines[index]);
            }

            notebookCancelButton = CreateButton(
                "Btn_NotebookCancel",
                rightPage,
                font,
                "我再想想",
                Hex("F3B13D"),
                Hex("FFC861"),
                PrimaryText,
                new Vector2(0.35f, 0f),
                new Vector2(150f, 78f));
            notebookCancelButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 82f);

            notebookSubmitButton = CreateButton(
                "Btn_NotebookSubmit",
                rightPage,
                font,
                "提交证据",
                Hex("FF4E4E"),
                Hex("FF6D6D"),
                PrimaryText,
                new Vector2(0.68f, 0f),
                new Vector2(210f, 78f));
            notebookSubmitButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 82f);
            notebookPanel.SetActive(false);
        }

        private static TMP_Text CreateNotebookInfoText(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string content,
            Vector2 anchoredPosition)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                anchoredPosition,
                new Vector2(360f, 34f));
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.color = Hex("242424");
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static void CreateNotebookEvidenceButton(
            RectTransform parent,
            TMP_FontAsset font,
            int evidenceIndex,
            Vector2 anchoredPosition,
            out Button button,
            out TMP_Text label,
            out Outline outline)
        {
            RectTransform rect = CreateImage(
                $"Btn_NotebookEvidence_{evidenceIndex:00}",
                parent,
                Hex("EFEFEF"),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(188f, 118f),
                anchoredPosition);
            rect.pivot = new Vector2(0.5f, 0.5f);

            outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("40E870");
            outline.effectDistance = new Vector2(4f, -4f);
            outline.enabled = false;

            button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Hex("EFEFEF");
            colors.highlightedColor = Hex("FFFFFF");
            colors.pressedColor = Hex("D8F6DF");
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.76f, 0.76f, 0.76f, 0.28f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            label = CreateText(
                $"Txt_NotebookEvidence_{evidenceIndex:00}",
                rect,
                font,
                $"资料{evidenceIndex + 1:00}：--",
                14f,
                FontStyles.Bold,
                Hex("333333"),
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(14f, 12f),
                new Vector2(-12f, -10f));
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = 14f;
            label.overflowMode = TextOverflowModes.Truncate;
        }

        private static void CreateResultPanel(
            RectTransform root,
            TMP_FontAsset font,
            out GameObject resultPanel,
            out TMP_Text resultTitleText,
            out TMP_Text resultStatusText,
            out TMP_Text resultDescriptionText,
            out TMP_Text resultMetricsText,
            out Button resultActionButton)
        {
            RectTransform overlay = CreateImage(
                "ResultPanel",
                root,
                new Color(0f, 0f, 0f, 0.72f),
                Vector2.zero,
                Vector2.one);
            resultPanel = overlay.gameObject;

            resultActionButton = CreateButton(
                "Btn_ResultOverlayClose",
                overlay,
                font,
                "点击任意空白位置关闭",
                new Color(0f, 0f, 0f, 0f),
                new Color(1f, 1f, 1f, 0.02f),
                PrimaryText,
                new Vector2(0.5f, 0.5f),
                new Vector2(1920f, 1080f));
            RectTransform closeButtonRect = resultActionButton.GetComponent<RectTransform>();
            closeButtonRect.anchorMin = Vector2.zero;
            closeButtonRect.anchorMax = Vector2.one;
            closeButtonRect.offsetMin = Vector2.zero;
            closeButtonRect.offsetMax = Vector2.zero;
            TMP_Text closeTip = resultActionButton.GetComponentInChildren<TMP_Text>(true);
            if (closeTip != null)
            {
                RectTransform closeTipRect = closeTip.GetComponent<RectTransform>();
                closeTipRect.anchorMin = new Vector2(0.35f, 0.08f);
                closeTipRect.anchorMax = new Vector2(0.65f, 0.16f);
                closeTipRect.offsetMin = Vector2.zero;
                closeTipRect.offsetMax = Vector2.zero;
                closeTip.fontSize = 22f;
                closeTip.alignment = TextAlignmentOptions.Center;
            }

            RectTransform card = CreatePanel(
                "ResultCard",
                overlay,
                new Vector2(0.33f, 0.46f),
                new Vector2(0.67f, 0.88f));
            card.gameObject.GetComponent<Image>().color = Hex("333333");

            CreateImage(
                "ResultIconPlaceholder",
                card,
                Hex("BDBDBD"),
                new Vector2(0.5f, 0.7f),
                new Vector2(0.5f, 0.7f),
                new Vector2(74f, 74f),
                Vector2.zero);

            resultTitleText = CreateText(
                "Txt_ResultTitle",
                card,
                font,
                "提交成功",
                24f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0.12f, 0.78f),
                new Vector2(0.88f, 0.9f),
                Vector2.zero,
                Vector2.zero);

            resultStatusText = CreateText(
                "Txt_ResultStatus",
                card,
                font,
                "工单已关闭",
                34f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0.12f, 0.47f),
                new Vector2(0.88f, 0.61f),
                Vector2.zero,
                Vector2.zero);

            resultDescriptionText = CreateText(
                "Txt_ResultDescription",
                card,
                font,
                "工单已计入 AI 解决表，实际用户会再收到本次问题的后续回复。",
                16f,
                FontStyles.Bold,
                MutedText,
                TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.34f),
                new Vector2(0.9f, 0.45f),
                Vector2.zero,
                Vector2.zero);

            resultMetricsText = CreateText(
                "Txt_ResultMetrics",
                card,
                font,
                "A07风险值+0\n已解决数量：1\n本次证据记录：无",
                16f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.Center,
                new Vector2(0.2f, 0.1f),
                new Vector2(0.8f, 0.28f),
                Vector2.zero,
                Vector2.zero);

            RectTransform saveStatus = CreateRect(
                "ResultSaveStatus",
                overlay,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-230f, 72f),
                new Vector2(360f, 80f));
            RectTransform saveIcon = CreateImage(
                "SaveStatusIcon",
                saveStatus,
                PrimaryText,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(60f, 60f),
                new Vector2(30f, 0f));
            saveIcon.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            CreateText(
                "Txt_SaveIcon",
                saveIcon,
                font,
                "✓",
                38f,
                FontStyles.Bold,
                Hex("111111"),
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            CreateText(
                "Txt_SaveStatus",
                saveStatus,
                font,
                "关卡进度已保存",
                30f,
                FontStyles.Bold,
                PrimaryText,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(80f, 0f),
                Vector2.zero);
            resultPanel.SetActive(false);
        }

        private static void BindView(
            Level1GameView view,
            GameObject ticketAppWindow,
            Button ticketAppCloseButton,
            Button workAppButton,
            Button clueNotebookButton,
            Button taskbarWorkQueueButton,
            Button taskbarDatabaseButton,
            RectTransform queueContent,
            Level1TicketQueueItemView queueItemTemplate,
            GameObject[] ticketContentObjects,
            TMP_Text progressText,
            TMP_Text statusText,
            TMP_Text evidenceText,
            TMP_Text resolvedText,
            TMP_Text titleText,
            TMP_Text metaText,
            TMP_Text ticketIdText,
            TMP_Text userMessageText,
            TMP_Text aiReplyText,
            ScrollRect chatScrollRect,
            RectTransform chatContent,
            GameObject chatBubbleTemplate,
            GameObject chatEmptyState,
            GameObject dataPanel,
            ScrollRect dataScrollRect,
            TMP_Text profileText,
            TMP_Text historyText,
            TMP_Text deviceLogText,
            TMP_Text regionStatusText,
            GameObject evidenceDetailOverlay,
            TMP_Text evidenceDetailTitleText,
            TMP_Text evidenceDetailBodyText,
            Button evidenceDetailCloseButton,
            Button primaryActionButton,
            Button dataLookupButton,
            Button transferHumanButton,
            Button saveEvidenceButton,
            Button markResolvedButton,
            Image markResolvedHoldFill,
            Button chatEvidenceActionButton,
            GameObject notebookPanel,
            TMP_Text notebookReasonText,
            TMP_Text notebookUserText,
            TMP_Text notebookEmotionText,
            TMP_Text notebookRegionText,
            TMP_Text notebookTicketIdText,
            Button[] notebookEvidenceButtons,
            TMP_Text[] notebookEvidenceTexts,
            Outline[] notebookEvidenceOutlines,
            Button notebookCloseButton,
            Button notebookCancelButton,
            Button notebookSubmitButton,
            GameObject resultPanel,
            TMP_Text resultTitleText,
            TMP_Text resultStatusText,
            TMP_Text resultDescriptionText,
            TMP_Text resultMetricsText,
            Button resultActionButton)
        {
            SerializedObject serializedView = new(view);
            SetObject(serializedView, "ticketAppWindow", ticketAppWindow);
            SetObject(serializedView, "ticketAppCloseButton", ticketAppCloseButton);
            SetObject(serializedView, "workAppButton", workAppButton);
            SetObject(serializedView, "clueNotebookButton", clueNotebookButton);
            SetObject(serializedView, "taskbarWorkQueueButton", taskbarWorkQueueButton);
            SetObject(serializedView, "taskbarDatabaseButton", taskbarDatabaseButton);
            SetObject(serializedView, "ticketQueueContent", queueContent);
            SetObject(serializedView, "ticketQueueItemTemplate", queueItemTemplate);
            SerializedProperty contentObjects = serializedView.FindProperty("ticketContentObjects");
            contentObjects.arraySize = ticketContentObjects.Length;
            for (int index = 0; index < ticketContentObjects.Length; index++)
            {
                contentObjects.GetArrayElementAtIndex(index).objectReferenceValue =
                    ticketContentObjects[index];
            }
            SetObject(serializedView, "ticketProgressText", progressText);
            SetObject(serializedView, "statusText", statusText);
            SetObject(serializedView, "evidenceCountText", evidenceText);
            SetObject(serializedView, "resolvedCountText", resolvedText);
            SetObject(serializedView, "ticketTitleText", titleText);
            SetObject(serializedView, "ticketMetaText", metaText);
            SetObject(serializedView, "ticketIdText", ticketIdText);
            SetObject(serializedView, "userMessageText", userMessageText);
            SetObject(serializedView, "aiReplyText", aiReplyText);
            SetObject(serializedView, "chatScrollRect", chatScrollRect);
            SetObject(serializedView, "chatContent", chatContent);
            SetObject(serializedView, "chatBubbleTemplate", chatBubbleTemplate);
            SetObject(serializedView, "chatEmptyState", chatEmptyState);
            SetObject(serializedView, "dataPanel", dataPanel);
            SetObject(serializedView, "dataScrollRect", dataScrollRect);
            SetObject(serializedView, "profileText", profileText);
            SetObject(serializedView, "historyText", historyText);
            SetObject(serializedView, "deviceLogText", deviceLogText);
            SetObject(serializedView, "regionStatusText", regionStatusText);
            SetObject(serializedView, "evidenceDetailOverlay", evidenceDetailOverlay);
            SetObject(serializedView, "evidenceDetailTitleText", evidenceDetailTitleText);
            SetObject(serializedView, "evidenceDetailBodyText", evidenceDetailBodyText);
            SetObject(serializedView, "evidenceDetailCloseButton", evidenceDetailCloseButton);
            SetObject(serializedView, "primaryActionButton", primaryActionButton);
            SetObject(serializedView, "dataLookupButton", dataLookupButton);
            SetObject(serializedView, "transferHumanButton", transferHumanButton);
            SetObject(serializedView, "saveEvidenceButton", saveEvidenceButton);
            SetObject(serializedView, "markResolvedButton", markResolvedButton);
            SetObject(serializedView, "markResolvedHoldFill", markResolvedHoldFill);
            SetObject(serializedView, "chatEvidenceActionButton", chatEvidenceActionButton);
            SetObject(serializedView, "notebookPanel", notebookPanel);
            SetObject(serializedView, "notebookReasonText", notebookReasonText);
            SetObject(serializedView, "notebookUserText", notebookUserText);
            SetObject(serializedView, "notebookEmotionText", notebookEmotionText);
            SetObject(serializedView, "notebookRegionText", notebookRegionText);
            SetObject(serializedView, "notebookTicketIdText", notebookTicketIdText);
            SetObjectArray(serializedView, "notebookEvidenceButtons", notebookEvidenceButtons);
            SetObjectArray(serializedView, "notebookEvidenceTexts", notebookEvidenceTexts);
            SetObjectArray(serializedView, "notebookEvidenceOutlines", notebookEvidenceOutlines);
            SetObject(serializedView, "notebookCloseButton", notebookCloseButton);
            SetObject(serializedView, "notebookCancelButton", notebookCancelButton);
            SetObject(serializedView, "notebookSubmitButton", notebookSubmitButton);
            SetObject(serializedView, "resultPanel", resultPanel);
            SetObject(serializedView, "resultTitleText", resultTitleText);
            SetObject(serializedView, "resultStatusText", resultStatusText);
            SetObject(serializedView, "resultDescriptionText", resultDescriptionText);
            SetObject(serializedView, "resultMetricsText", resultMetricsText);
            SetObject(serializedView, "resultActionButton", resultActionButton);
            serializedView.FindProperty("popupDelayAfterDialogueSeconds").floatValue = 0.35f;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindFlowManager(
            Level1GameFlowManager flowManager,
            MingBayProjectDatabase database,
            EvidenceManager evidenceManager,
            MetricManager metricManager,
            Level1GameView view)
        {
            SerializedObject serializedFlow = new(flowManager);
            SetObject(serializedFlow, "database", database);
            SetObject(serializedFlow, "evidenceManager", evidenceManager);
            SetObject(serializedFlow, "metricManager", metricManager);
            SetObject(serializedFlow, "mainGameView", view);
            SetStringArray(serializedFlow, "stageOrder", CurrentSpreadsheetLevelId);
            SetStringArray(serializedFlow, "stageDisplayNames", CurrentSpreadsheetLevelName);
            serializedFlow.FindProperty("titleSceneName").stringValue = "TitleScene";
            serializedFlow.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform CreatePanel(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform rect = CreateImage(name, parent, Panel, anchorMin, anchorMax);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            Color normalColor,
            Color highlightedColor,
            Color textColor,
            Vector2 anchor,
            Vector2 size)
        {
            RectTransform rect = CreateImage(name, parent, normalColor, anchor, anchor, size, Vector2.zero);
            Button button = rect.gameObject.AddComponent<Button>();

            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = Color.Lerp(highlightedColor, Background, 0.25f);
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.28f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            CreateText(
                "Label",
                rect,
                font,
                label,
                22f,
                FontStyles.Bold,
                textColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private static RectTransform CreateImage(
            string name,
            RectTransform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2? sizeDelta = null,
            Vector2? anchoredPosition = null)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition ?? Vector2.zero,
                sizeDelta ?? Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string content,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(Level1ScenePath, true)
            };
        }

        private static void CapturePreview(Canvas canvas, Camera camera)
        {
            const int width = 1920;
            const int height = 1080;

            RenderMode originalRenderMode = canvas.renderMode;
            Camera originalWorldCamera = canvas.worldCamera;
            RenderTexture originalTarget = camera.targetTexture;
            RenderTexture originalActive = RenderTexture.active;

            RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D preview = new(width, height, TextureFormat.RGB24, false);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = renderTexture;

            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = renderTexture;
            preview.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            preview.Apply();

            string previewPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Logs", "Level1ScenePreview.png"));
            File.WriteAllBytes(previewPath, preview.EncodeToPNG());

            canvas.renderMode = originalRenderMode;
            canvas.worldCamera = originalWorldCamera;
            camera.targetTexture = originalTarget;
            RenderTexture.active = originalActive;

            UnityEngine.Object.DestroyImmediate(preview);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void SetObject(
            SerializedObject target,
            string propertyName,
            UnityEngine.Object value)
        {
            target.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetObjectArray(
            SerializedObject target,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetString(SerializedObject target, string propertyName, string value)
        {
            target.FindProperty(propertyName).stringValue = value;
        }

        private static void SetStringArray(
            SerializedObject target,
            string propertyName,
            params string[] values)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
        }

        private static void SetDialogueLineArray(
            SerializedObject target,
            string propertyName,
            TicketDialogueLine[] values)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            int count = values != null ? values.Length : 0;
            property.arraySize = count;
            for (int index = 0; index < count; index++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("speakerId").stringValue = values[index].SpeakerId;
                element.FindPropertyRelative("speakerLabel").stringValue = values[index].SpeakerLabel;
                element.FindPropertyRelative("text").stringValue = values[index].Text;
                element.FindPropertyRelative("fromUser").boolValue = values[index].FromUser;
            }
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString($"#{value}", out Color color))
            {
                throw new System.ArgumentException($"无效颜色值：{value}");
            }

            return color;
        }
    }
}
