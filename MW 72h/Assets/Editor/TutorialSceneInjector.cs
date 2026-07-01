using System.Collections.Generic;
using MingBay.Core;
using MingBay.Data;
using MingBay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MingBay.Editor
{
    /// <summary>
    /// 教程关卡注入器。
    /// 在运行"生成基础工单demo"之后，点击菜单 明湾 → 注入教程关卡，
    /// 即可在不动队友代码的前提下，把教程阶段、工单和引导控制器注入当前场景。
    /// </summary>
    public static class TutorialSceneInjector
    {
        private const string DatabasePath = "Assets/Configs/MingBayProjectDatabase.asset";
        private const string Level1ScenePath = "Assets/Scenes/Level1Scene.unity";

        [MenuItem("明湾/注入教程关卡")]
        public static void Inject()
        {
            // ── 1. 打开 Level1Scene ──
            Scene scene = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);

            // ── 2. 注入教程工单到数据库 ──
            MingBayProjectDatabase database =
                AssetDatabase.LoadAssetAtPath<MingBayProjectDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError("[TutorialInject] 未找到 MingBayProjectDatabase，请先生成基础工单demo。");
                return;
            }

            InjectTutorialStage(database);

            // ── 3. 在场景中挂载 TutorialGuidanceController ──
            TutorialGuidanceController controller =
                Object.FindFirstObjectByType<TutorialGuidanceController>();
            if (controller == null)
            {
                // 找到 GameSystems 或创建一个
                GameObject systems = GameObject.Find("GameSystems");
                if (systems == null)
                {
                    systems = new GameObject("GameSystems");
                }

                controller = Undo.AddComponent<TutorialGuidanceController>(systems);
            }

            BindControllerReferences(controller, scene);

            // ── 4. MentorBubble 置顶 ──
            EnsureMentorBubbleOnTop(scene);

            // ── 5. 保存 ──
            EditorSceneManager.SaveScene(scene, Level1ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TutorialInject] 教程关卡注入完成。");
        }

        private static void InjectTutorialStage(MingBayProjectDatabase database)
        {
            SerializedObject so = new(database);

            // 添加教程工单到 tickets 列表
            string[] tutorialGuids =
            {
                "41d6ea428f0f3764cbed8a4e0497765d", // Ticket_TUTORIAL_01
                "3fd77392232790b4dbb529d91155e571"  // Ticket_TUTORIAL_02
            };

            SerializedProperty ticketsProp = so.FindProperty("tickets");
            int existingCount = ticketsProp.arraySize;

            // 检查是否已存在（去重）
            var existingGuids = new HashSet<string>();
            for (int i = 0; i < existingCount; i++)
            {
                SerializedProperty elem = ticketsProp.GetArrayElementAtIndex(i);
                if (elem.objectReferenceValue != null)
                {
                    string path = AssetDatabase.GetAssetPath(elem.objectReferenceValue);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    existingGuids.Add(guid);
                }
            }

            foreach (string guid in tutorialGuids)
            {
                if (existingGuids.Contains(guid)) continue;

                ticketsProp.arraySize++;
                SerializedProperty newElem = ticketsProp.GetArrayElementAtIndex(ticketsProp.arraySize - 1);
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TicketData ticket = AssetDatabase.LoadAssetAtPath<TicketData>(path);
                newElem.objectReferenceValue = ticket;
                Debug.Log($"[TutorialInject] 添加教程工单: {ticket?.name ?? "null"} ({guid})");
            }

            // 设置 stageOrder = [Stage_Tutorial, Stage_Day1]
            // 读取现有 stageOrder，仅在没有 Stage_Tutorial 时添加
            SerializedProperty stageOrderProp = so.FindProperty("stageOrder");
            SerializedProperty stageNamesProp = so.FindProperty("stageDisplayNames");

            // 扫描所有工单的 stageId，自动推断 stageOrder
            var stageIdOrder = new List<string>();
            var seenStageIds = new HashSet<string>();
            for (int i = 0; i < ticketsProp.arraySize; i++)
            {
                TicketData ticket = ticketsProp.GetArrayElementAtIndex(i).objectReferenceValue as TicketData;
                if (ticket != null && !string.IsNullOrEmpty(ticket.StageId) && seenStageIds.Add(ticket.StageId))
                    stageIdOrder.Add(ticket.StageId);
            }

            // TUTORIAL 必须在最前面
            var finalOrder = new List<string> { "TUTORIAL" };
            foreach (string id in stageIdOrder)
            {
                if (id != "TUTORIAL")
                    finalOrder.Add(id);
            }

            // 写入 stageOrder
            stageOrderProp.arraySize = finalOrder.Count;
            for (int i = 0; i < finalOrder.Count; i++)
                stageOrderProp.GetArrayElementAtIndex(i).stringValue = finalOrder[i];

            // 写入 stageDisplayNames（匹配 stageOrder）
            stageNamesProp.arraySize = finalOrder.Count;
            for (int i = 0; i < finalOrder.Count; i++)
            {
                string displayName = finalOrder[i] switch
                {
                    "TUTORIAL" => "教程",
                    "Stage_Day1" => "第一夜",
                    "N1" => "第一夜",
                    _ => finalOrder[i]
                };
                stageNamesProp.GetArrayElementAtIndex(i).stringValue = displayName;
            }

            Debug.Log($"[TutorialInject] stageOrder 已更新: [{string.Join(", ", finalOrder)}]");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void BindControllerReferences(
            TutorialGuidanceController controller, Scene scene)
        {
            SerializedObject so = new(controller);

            SetRef(so, "highlightImage", null); // 已改用 Outline

            // mentorBubble
            GameObject mentor = FindInScene(scene, "MentorBubble");
            SetRef(so, "mentorBubble", mentor);

            // mentorBubbleText — 必须是 Txt_MentorHint（对话框正文），不能用 GetComponentInChildren
            // 因为 MentorBubble 内有多个 TMP_Text（Txt_AvatarFace/Txt_AvatarName/Txt_MentorHint）
            GameObject hintText = FindInScene(scene, "Txt_MentorHint");
            if (hintText != null)
                SetRef(so, "mentorBubbleText", hintText.GetComponent<TMP_Text>());

            // guidanceText — 不需要底部引导文字，使用 MentorBubble 即可
            SetRef(so, "guidanceText", null);

            // mainGameView
            Level1GameView view = Object.FindFirstObjectByType<Level1GameView>();
            SetRef(so, "mainGameView", view);

            // gameFlowManager
            Level1GameFlowManager flow = Object.FindFirstObjectByType<Level1GameFlowManager>();
            SetRef(so, "gameFlowManager", flow);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            Debug.Log("[TutorialInject] TutorialGuidanceController 引用已绑定。");
        }

        private static void EnsureMentorBubbleOnTop(Scene scene)
        {
            GameObject mentor = FindInScene(scene, "MentorBubble");
            if (mentor == null) return;

            // 添加 Button 组件使对话框可点击（用于"点击对话框继续"交互）
            if (!mentor.TryGetComponent(out Button _))
            {
                Button btn = Undo.AddComponent<Button>(mentor);
                // 透明按钮：不改变外观，只接收点击
                btn.targetGraphic = mentor.GetComponent<Graphic>();
                btn.transition = Selectable.Transition.None;
            }

            // 移到 Canvas 的最后子节点（渲染在最上层）
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                mentor.transform.SetParent(canvas.transform, true);
                mentor.transform.SetAsLastSibling();
                Debug.Log("[TutorialInject] MentorBubble 已置顶并添加交互。");
            }
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == name) return rootGo;
                Transform[] children = rootGo.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in children)
                {
                    if (t.name == name) return t.gameObject;
                }
            }

            return null;
        }

        private static void SetRef(SerializedObject so, string fieldName, Object value)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }
    }
}
