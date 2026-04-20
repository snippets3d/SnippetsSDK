#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Snippets.Sdk.Ui
{
    public class SnippetsMixamoToSnippetsWindow : EditorWindow
    {
        const float DefaultWidth = 560f;
        const float DefaultHeight = 480f;
        const float DropAreaHeight = 96f;

        readonly List<AnimationClip> _clips = new();
        Vector2 _scroll;
        string _lastResultMessage = string.Empty;
        MessageType _lastResultType = MessageType.Info;

        [MenuItem("Snippets/Extras/MixamoToSnippets")]
        public static void ShowWindow()
        {
            var window = GetWindow<SnippetsMixamoToSnippetsWindow>();
            window.titleContent = new GUIContent("MixamoToSnippets");
            window.minSize = new Vector2(DefaultWidth, DefaultHeight);
            window.Show();
        }

        void OnEnable()
        {
            minSize = new Vector2(DefaultWidth, DefaultHeight);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Mixamo To Snippets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag Mixamo AnimationClip assets here. The tool creates fixed legacy .anim assets in the same folder as each source clip, removing the 'mixamorig:' prefix from bindings and prepending an Armature path for Snippets compatibility.",
                MessageType.None);

            DrawDropArea();

            EditorGUILayout.Space(8f);
            DrawLegacyWarning();
            EditorGUILayout.Space(6f);
            DrawToolbar();
            EditorGUILayout.Space(6f);
            DrawClipList();

            if (!string.IsNullOrEmpty(_lastResultMessage))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(_lastResultMessage, _lastResultType);
            }
        }

        void DrawDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag & Drop Mixamo AnimationClip Assets Here", EditorStyles.helpBox);

            var labelRect = new Rect(dropRect.x + 12f, dropRect.y + 36f, dropRect.width - 24f, 20f);
            EditorGUI.LabelField(labelRect, "You can drop one clip or a whole batch from your library.", EditorStyles.centeredGreyMiniLabel);

            HandleDragAndDrop(dropRect);
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_clips.Count == 0))
            {
                if (GUILayout.Button("Fix And Save", GUILayout.Height(28f)))
                    FixQueuedClips();

                if (GUILayout.Button("Clear", GUILayout.Width(90f), GUILayout.Height(28f)))
                {
                    _clips.Clear();
                    _lastResultMessage = string.Empty;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{_clips.Count} clip{(_clips.Count == 1 ? string.Empty : "s")} queued", EditorStyles.miniLabel, GUILayout.Width(100f));

            EditorGUILayout.EndHorizontal();
        }

        void DrawLegacyWarning()
        {
            int nonLegacyCount = CountNonLegacyClips();
            if (nonLegacyCount <= 0)
                return;

            EditorGUILayout.HelpBox(
                $"{nonLegacyCount} queued clip{(nonLegacyCount == 1 ? " is" : "s are")} not set to Legacy. " +
                "The converted output will still be saved as a legacy .anim clip, but the original source clips themselves will not work in the Snippets legacy playback path unless their import settings are set to Legacy.",
                MessageType.Warning);
        }

        void DrawClipList()
        {
            if (_clips.Count == 0)
            {
                EditorGUILayout.HelpBox("No clips queued yet.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _clips.Count; i++)
            {
                var clip = _clips[i];

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);

                if (clip != null)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    if (!string.IsNullOrEmpty(path))
                        EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                }

                var badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };

                if (clip != null)
                {
                    bool isLegacy = SnippetsMixamoCompatibilityUtility.IsLegacyClip(clip);
                    var previousColor = GUI.color;
                    GUI.color = isLegacy ? new Color(0.25f, 0.65f, 0.25f) : new Color(0.85f, 0.55f, 0.15f);
                    GUILayout.Label(isLegacy ? "Legacy" : "Not Legacy", badgeStyle, GUILayout.Width(74f));
                    GUI.color = previousColor;
                }

                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    _clips.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        void HandleDragAndDrop(Rect dropRect)
        {
            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
                return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    if (ContainsAnimationClip(DragAndDrop.objectReferences))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    if (!ContainsAnimationClip(DragAndDrop.objectReferences))
                        break;

                    DragAndDrop.AcceptDrag();
                    AddClips(DragAndDrop.objectReferences);
                    evt.Use();
                    break;
            }
        }

        static bool ContainsAnimationClip(Object[] objects)
        {
            if (objects == null || objects.Length == 0)
                return false;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is AnimationClip)
                    return true;
            }

            return false;
        }

        void AddClips(Object[] objects)
        {
            int addedCount = 0;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is not AnimationClip clip || clip == null)
                    continue;

                if (_clips.Contains(clip))
                    continue;

                _clips.Add(clip);
                addedCount++;
            }

            if (addedCount > 0)
            {
                int nonLegacyCount = CountNonLegacyClips();
                _lastResultType = MessageType.Info;
                _lastResultMessage = $"Added {addedCount} clip{(addedCount == 1 ? string.Empty : "s")} to the queue." +
                                     (nonLegacyCount > 0
                                         ? $" {nonLegacyCount} queued clip{(nonLegacyCount == 1 ? " is" : "s are")} not set to Legacy."
                                         : string.Empty);
            }
        }

        void FixQueuedClips()
        {
            int nonLegacyCount = CountNonLegacyClips();
            var createdClips = new List<AnimationClip>();
            int createdCount = SnippetsMixamoCompatibilityUtility.NormalizeClips(_clips, createdClips);

            if (createdCount <= 0)
            {
                _lastResultType = MessageType.Warning;
                _lastResultMessage = "No fixed clips were created. Check that the queued clips are valid asset clips.";
                return;
            }

            if (createdClips.Count > 0)
            {
                EditorGUIUtility.PingObject(createdClips[createdClips.Count - 1]);

                for (int i = 0; i < createdClips.Count; i++)
                {
                    var createdClip = createdClips[i];
                    if (createdClip != null)
                        Debug.Log($"[Snippets SDK] Created fixed Mixamo clip at '{AssetDatabase.GetAssetPath(createdClip)}'.", createdClip);
                }
            }

            _lastResultType = MessageType.Info;
            _lastResultMessage = $"Created {createdCount} fixed legacy clip{(createdCount == 1 ? string.Empty : "s")} in the same folders as the source clips." +
                                 (nonLegacyCount > 0
                                     ? $" {nonLegacyCount} source clip{(nonLegacyCount == 1 ? " was" : "s were")} not legacy, so treat the generated .anim assets as the compatible versions."
                                     : string.Empty);
        }

        int CountNonLegacyClips()
        {
            int count = 0;

            for (int i = 0; i < _clips.Count; i++)
            {
                var clip = _clips[i];
                if (clip != null && !SnippetsMixamoCompatibilityUtility.IsLegacyClip(clip))
                    count++;
            }

            return count;
        }
    }
}
#endif
