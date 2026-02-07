using System;
using UnityEngine;

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

        [Header("Control")]
        [Tooltip("ON by default. When enabled, this component will NOT touch the legacy Animation component " +
                 "(no AddClip/RemoveClip/Play/Stop/CrossFade). Another system is expected to drive animation.")]
        [SerializeField] private bool m_externalAnimationControl = true;

        // Standalone-only settings (kept, but hidden from Inspector by not serializing).
        // If you ever need standalone behavior again, flip external control off and tweak these constants.
        private const float kStandaloneCrossFadeDuration = 0.25f;
        private const bool kStandaloneUseCrossFade = true;

        // Cache the last assigned clip so Value still works in external mode.
        private AnimationClip _cachedClip;

        public override AnimationClip Value
        {
            get
            {
                if (m_externalAnimationControl)
                    return _cachedClip;

                return m_animation != null ? m_animation.clip : null;
            }
            set
            {
                _cachedClip = value;

                // External mode: do NOT modify the Animation component at all.
                if (m_externalAnimationControl)
                {
                    // Still ensure a visible avatar exists when a clip is assigned.
                    EnsureAvatarInitialized(value);
                    return;
                }

                if (m_animation == null)
                    InitializeAnimationComponentForClip(value);

                if (m_animation == null)
                    return;

                // Remove previous clip (if any)
                if (m_animation.clip != null)
                    m_animation.RemoveClip(m_animation.clip);

                m_animation.clip = value;

                if (value != null)
                {
#if UNITY_EDITOR
                    if (!value.legacy)
                        Debug.LogWarning($"[SnippetAvatarAnimatorPlayer] Clip '{value.name}' is NOT marked as Legacy. " +
                                         $"Legacy Animation component may not play it. " +
                                         $"Select the FBX/clip import settings and enable 'Legacy'.", this);
#endif
                    // NOTE: Standalone mode uses clip.name as the state name.
                    // External controllers should register unique aliases themselves.
                    m_animation.AddClip(value, value.name);
                }
            }
        }

        public override bool IsPlaying { get; protected set; }

        public override void Play()
        {
            // External mode: only maintain IsPlaying + events (do NOT touch Animation component)
            if (m_externalAnimationControl)
            {
                IsPlaying = true;
                PlaybackStarted?.Invoke();
                return;
            }

            if (m_animation == null || m_animation.clip == null)
                return;

            if (kStandaloneUseCrossFade && kStandaloneCrossFadeDuration > 0f)
                m_animation.CrossFade(m_animation.clip.name, kStandaloneCrossFadeDuration);
            else
                m_animation.Play(m_animation.clip.name);

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
            if (m_externalAnimationControl)
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
            if (m_externalAnimationControl)
                return;

            if (m_animation != null && IsPlaying && !m_animation.isPlaying)
                Stop();
        }

        private void Awake()
        {
            // External mode: don't Stop() legacy Animation on awake (can clear queues from other controllers)
            if (!m_externalAnimationControl)
                StopInternal(false);
            else
                IsPlaying = false;
        }

        private void InitializeAnimationComponentForClip(AnimationClip clip)
        {
            var childTransform = transform.childCount > 0 ? transform.GetChild(0) : null;

            if (childTransform == null || childTransform.GetComponentInChildren<SkinnedMeshRenderer>() == null)
            {
#if UNITY_EDITOR
                var animationClipAssetPath = UnityEditor.AssetDatabase.GetAssetPath(clip);

                if (UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(animationClipAssetPath) != typeof(GameObject))
                    throw new ArgumentException("No Avatar Provided in the Snippet Player and it is impossible to reconstruct an avatar from the AnimationClip");

                var animationPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(animationClipAssetPath);
                childTransform = GameObject.Instantiate(animationPrefab, transform).transform;
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

            // If any child already contains a skinned mesh, assume an avatar is present.
            var existingSkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>(true);
            var existingAnimation = GetComponentInChildren<Animation>(true);

            if (existingSkinnedMesh != null)
            {
                if (m_animation == null && existingAnimation != null)
                    m_animation = existingAnimation;
                return;
            }

            // No avatar found, create one from the clip.
            InitializeAnimationComponentForClip(clip);
        }
    }
}
