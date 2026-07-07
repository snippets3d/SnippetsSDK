using System;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Snippets.Sdk
{
    /// <summary>
    /// Implements the playback of the animation of a snippet using a legacy Animation component.
    /// In Snippets3D SDK usage, animation blending is typically owned externally (e.g. SnippetsActorRegistry).
    /// </summary>
    public class SnippetAvatarAnimatorPlayer : SnippetAvatarPlayer
    {
        [Header("Legacy Animation")]
        [SerializeField] private Animation m_animation;

        // Legacy serialized field kept so older prefabs still deserialize cleanly.
        // Runtime ownership is now assigned automatically by SnippetPlayer/controllers.
        [SerializeField, HideInInspector] private bool m_externalAnimationControl = true;

        // Cache the last assigned clip so Value still works in external mode.
        private AnimationClip _cachedClip;
        private bool _isAnimationDrivenExternally;

        public override AnimationClip Value
        {
            get
            {
                if (_isAnimationDrivenExternally)
                    return _cachedClip;

                return m_animation != null ? m_animation.clip : _cachedClip;
            }
            set
            {
                _cachedClip = value;

                // External mode: do NOT modify the Animation component at all.
                if (_isAnimationDrivenExternally)
                {
                    // Still ensure a visible avatar exists when a clip is assigned.
                    EnsureAvatarInitialized(value);
                    return;
                }

                SyncLegacyAnimationWithCachedClip();
            }
        }

        public override void SetAnimationDrivenExternally(bool isExternallyDriven)
        {
            if (_isAnimationDrivenExternally == isExternallyDriven)
                return;

            _isAnimationDrivenExternally = isExternallyDriven;

            if (_isAnimationDrivenExternally)
            {
                EnsureAvatarInitialized(_cachedClip);
                return;
            }

            SyncLegacyAnimationWithCachedClip();
        }

        public override bool IsPlaying { get; protected set; }

        public override void Play()
        {
            // External mode: only maintain IsPlaying + events (do NOT touch Animation component)
            if (_isAnimationDrivenExternally)
            {
                IsPlaying = true;
                PlaybackStarted?.Invoke();
                return;
            }

            if (m_animation == null || m_animation.clip == null)
                return;

            // Snippet speech clips need to start on frame 0 immediately to stay aligned with audio.
            m_animation.Stop();

            AnimationState clipState = m_animation[m_animation.clip.name];
            if (clipState != null)
            {
                clipState.time = 0f;
                clipState.speed = 1f;
                clipState.enabled = true;
                clipState.weight = 1f;
            }

            m_animation.Play(m_animation.clip.name);
            m_animation.Sample();

            IsPlaying = true;
            PlaybackStarted?.Invoke();
        }

        public override void Stop()
        {
            StopInternal();
        }

        private void StopInternal(bool notifyEvent = true)
        {
            // External mode: do NOT Stop the Animation component (it clears CrossFadeQueued!)
            if (_isAnimationDrivenExternally)
            {
                IsPlaying = false;
                if (notifyEvent)
                    PlaybackStopped?.Invoke();
                return;
            }

            if (m_animation != null)
            {
                m_animation.Stop();
                IsPlaying = false;

                if (notifyEvent)
                    PlaybackStopped?.Invoke();
            }
        }

        private void Update()
        {
            // External mode: NEVER auto-stop based on legacy Animation state
            if (_isAnimationDrivenExternally)
                return;

            if (m_animation != null && IsPlaying && !m_animation.isPlaying)
                Stop();
        }

        private void Awake()
        {
            // External mode: don't Stop() legacy Animation on awake (can clear queues from other controllers)
            if (!_isAnimationDrivenExternally)
                StopInternal(false);
            else
                IsPlaying = false;
        }

        private void SyncLegacyAnimationWithCachedClip()
        {
            if (_cachedClip == null)
            {
                if (m_animation != null)
                {
                    if (m_animation.clip != null)
                        m_animation.RemoveClip(m_animation.clip);

                    m_animation.clip = null;
                }

                return;
            }

            if (m_animation == null)
                InitializeAnimationComponentForClip(_cachedClip);

            if (m_animation == null)
                return;

            // Remove previous clip (if any)
            if (m_animation.clip != null)
                m_animation.RemoveClip(m_animation.clip);

            m_animation.clip = _cachedClip;

            #if UNITY_EDITOR
            if (!_cachedClip.legacy)
                Debug.LogWarning($"[SnippetAvatarAnimatorPlayer] Clip '{_cachedClip.name}' is NOT marked as Legacy. " +
                                 $"Legacy Animation component may not play it. " +
                                 $"Select the FBX/clip import settings and enable 'Legacy'.", this);
            #endif
            // NOTE: Standalone mode uses clip.name as the state name.
            // External controllers should register unique aliases themselves.
            m_animation.AddClip(_cachedClip, _cachedClip.name);
        }

        private void InitializeAnimationComponentForClip(AnimationClip clip)
        {
            var childTransform = transform.childCount > 0 ? transform.GetChild(0) : null;

            if (!HasValidAvatar(childTransform))
            {
#if UNITY_EDITOR
                var animationClipAssetPath = UnityEditor.AssetDatabase.GetAssetPath(clip);

                if (UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(animationClipAssetPath) != typeof(GameObject))
                    throw new ArgumentException("No Avatar Provided in the Snippet Player and it is impossible to reconstruct an avatar from the AnimationClip");

                var animationPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(animationClipAssetPath);
                if (childTransform != null)
                    DestroyImmediate(childTransform.gameObject);

                // Keep a nested prefab reference to the imported animation asset when possible.
                var instantiatedAvatar = UnityEditor.PrefabUtility.InstantiatePrefab(animationPrefab, transform) as GameObject;
                childTransform = (instantiatedAvatar ?? GameObject.Instantiate(animationPrefab, transform)).transform;
#else
                throw new InvalidOperationException("No Avatar Provided in the Snippet Player, it is impossible to configure the Snippet Player");
#endif
            }

            var avatarRoot = childTransform.gameObject;
            m_animation = avatarRoot.GetComponentInChildren<Animation>();

            if (m_animation == null)
                m_animation = avatarRoot.AddComponent<Animation>();

            m_animation.playAutomatically = false;
        }

        private void EnsureAvatarInitialized(AnimationClip clip)
        {
            if (clip == null)
                return;

#if UNITY_EDITOR
            if (!CanMutateAvatarHierarchyInEditor())
                return;
#endif

            var existingSkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>(true);
            var existingAnimation = GetComponentInChildren<Animation>(true);

            if (existingSkinnedMesh != null && existingSkinnedMesh.sharedMesh != null)
            {
                if (m_animation == null && existingAnimation != null)
                    m_animation = existingAnimation;
                return;
            }

            // No avatar found, create one from the clip.
            InitializeAnimationComponentForClip(clip);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (!CanMutateAvatarHierarchyInEditor())
                return;

            AnimationClip clip = _cachedClip;
            if (clip == null && m_animation != null)
                clip = m_animation.clip;

            if (clip != null)
                EnsureAvatarInitialized(clip);
        }

        private bool CanMutateAvatarHierarchyInEditor()
        {
            // Avoid mutating prefab assets while Unity is importing/loading them.
            // We only rebuild avatar children on scene/prefab-contents instances.
            if (EditorUtility.IsPersistent(this) || EditorUtility.IsPersistent(gameObject))
                return false;

            if (PrefabUtility.IsPartOfPrefabAsset(this) || PrefabUtility.IsPartOfPrefabAsset(gameObject))
                return false;

            return true;
        }
#endif

        private static bool HasValidAvatar(Transform avatarRoot)
        {
            if (avatarRoot == null)
                return false;

            return avatarRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(renderer => renderer != null && renderer.sharedMesh != null);
        }
    }
}
