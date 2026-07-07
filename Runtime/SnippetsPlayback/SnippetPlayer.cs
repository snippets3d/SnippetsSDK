using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Snippets.Sdk
{
    [DefaultExecutionOrder(+1)] //so that when Awake is executed, all the associated sub-players are already initialized
    public class SnippetPlayer : MonoBehaviour, IPlayablePresenter<SnippetData>
    {
        [SerializeField]
        private SnippetTextPlayer m_snippetTextPlayer;

        [SerializeField]
        private SnippetSoundPlayer m_snippetSoundPlayer;

        [SerializeField]
        private SnippetAvatarPlayer m_snippetAvatarPlayer;

        [SerializeField]
        private bool m_playOnEnable;

        [SerializeField]
        private SnippetData m_value;

        private const int kPlayOnEnableWarmupFrames = 1;
        private Coroutine m_pendingAutoPlayCoroutine;
        private Coroutine m_pendingSynchronizedStartCoroutine;
        private int m_controllerManagementClaims;

        /// <inheritdoc />
        public SnippetData Value
        {
            get => m_value;
            set
            {
                m_value = value;
                StopInternal(false); //if we are changing the snippet, we need to stop the currently playing one, if any

                // set the value of the components
                if (m_snippetTextPlayer != null)
                    m_snippetTextPlayer.SetSnippetData(m_value);

                if (m_snippetSoundPlayer != null)
                    m_snippetSoundPlayer.Value = m_value.Sound;

                if (m_snippetAvatarPlayer != null)
                {
                    m_snippetAvatarPlayer.SetAnimationDrivenExternally(IsControllerManaged);
                    m_snippetAvatarPlayer.Value = m_value.Animation;
                }
            }
        }

        /// <inheritdoc />
        public bool IsPlaying { get; protected set; }

        /// <summary>
        /// True while one or more runtime controllers own this snippet player's avatar animation.
        /// </summary>
        public bool IsControllerManaged => m_controllerManagementClaims > 0;

        /// <inheritdoc />
        [field: SerializeField]
        public UnityEvent PlaybackStarted { get; set; } = new UnityEvent();

        /// <inheritdoc />
        [field: SerializeField]
        public UnityEvent PlaybackStopped { get; set; } = new UnityEvent();

        /// <summary>
        /// Claims controller ownership for this player. While claimed, Play On Enable is suppressed
        /// and avatar animation is treated as externally driven.
        /// </summary>
        public void AcquireControllerManagement()
        {
            m_controllerManagementClaims++;
            ApplyControllerManagementState();
        }

        /// <summary>
        /// Releases a previously claimed controller ownership.
        /// </summary>
        public void ReleaseControllerManagement()
        {
            if (m_controllerManagementClaims == 0)
                return;

            m_controllerManagementClaims--;
            ApplyControllerManagementState();
        }

        /// <inheritdoc />
        public void Play()
        {
            CancelPendingAutoPlay();
            CancelPendingSynchronizedStart();

            // play all the components of the snippet
            if (m_value != null && m_value.IsValid)
            {
                IsPlaying = true;

                if (m_snippetAvatarPlayer != null)
                    m_snippetAvatarPlayer.Play();

                bool needsSynchronizedStart =
                    m_snippetAvatarPlayer != null &&
                    (m_snippetSoundPlayer != null || m_snippetTextPlayer != null);

                if (needsSynchronizedStart)
                {
                    m_pendingSynchronizedStartCoroutine = StartCoroutine(StartSynchronizedContentRoutine());
                    return;
                }

                StartNonAvatarPlayback();
                PlaybackStarted?.Invoke();
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            CancelPendingAutoPlay();
            CancelPendingSynchronizedStart();
            StopInternal();
        }

        private void StopInternal(bool notifyEvent = true)
        {
            CancelPendingAutoPlay();
            CancelPendingSynchronizedStart();

            // stop all the components of the snippet
            if (m_snippetTextPlayer != null && m_snippetTextPlayer.IsPlaying)
                m_snippetTextPlayer.Stop();

            if (m_snippetSoundPlayer != null && m_snippetSoundPlayer.IsPlaying)
                m_snippetSoundPlayer.Stop();

            if (m_snippetAvatarPlayer != null && m_snippetAvatarPlayer.IsPlaying)
                m_snippetAvatarPlayer.Stop();

            IsPlaying = false;

            if(notifyEvent)
                PlaybackStopped?.Invoke();
        }

        private void Awake()
        {
            ApplyControllerManagementState();

            // set the initial value, if any
            if (m_value != null && m_value.IsValid)
                Value = m_value;
            // if there is no value, still initialize the system by stopping all playbacks
            else
                StopInternal(false);
        }

        private void OnEnable()
        {
            // Subscribe to the PlaybackStopped event of the sub-players
            if (m_snippetSoundPlayer != null)
            {
                m_snippetSoundPlayer.PlaybackStopped.AddListener(OnSubPlayerStopped);
            }
            else if (m_snippetAvatarPlayer != null)
            {
                m_snippetAvatarPlayer.PlaybackStopped.AddListener(OnSubPlayerStopped);
            }
            else if (m_snippetTextPlayer != null)
            {
                m_snippetTextPlayer.PlaybackStopped.AddListener(OnSubPlayerStopped);
            }

            if (m_playOnEnable && !IsControllerManaged)
            {
                m_pendingAutoPlayCoroutine = StartCoroutine(PlayOnEnableRoutine());
            }
        }

        private void OnDisable()
        {
            CancelPendingAutoPlay();
            CancelPendingSynchronizedStart();

            // Unsubscribe from the PlaybackStopped event of the sub-players
            if (m_snippetSoundPlayer != null)
            {
                m_snippetSoundPlayer.PlaybackStopped.RemoveListener(OnSubPlayerStopped);
            }
            else if (m_snippetAvatarPlayer != null)
            {
                m_snippetAvatarPlayer.PlaybackStopped.RemoveListener(OnSubPlayerStopped);
            }
            else if (m_snippetTextPlayer != null)
            {
                m_snippetTextPlayer.PlaybackStopped.RemoveListener(OnSubPlayerStopped);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || m_snippetAvatarPlayer == null)
                return;

            m_snippetAvatarPlayer.SetAnimationDrivenExternally(IsControllerManaged);

            if (m_value != null && m_value.IsValid)
                m_snippetAvatarPlayer.Value = m_value.Animation;
        }
#endif

        private void OnSubPlayerStopped()
        {
            Stop();
        }

        private IEnumerator PlayOnEnableRoutine()
        {
            for (int i = 0; i < kPlayOnEnableWarmupFrames; i++)
            {
                yield return null;
            }

            m_pendingAutoPlayCoroutine = null;

            if (!isActiveAndEnabled || IsPlaying)
            {
                yield break;
            }

            Play();
        }

        private IEnumerator StartSynchronizedContentRoutine()
        {
            yield return null;

            m_pendingSynchronizedStartCoroutine = null;

            if (!isActiveAndEnabled || !IsPlaying)
            {
                yield break;
            }

            StartNonAvatarPlayback();
            PlaybackStarted?.Invoke();
        }

        private void StartNonAvatarPlayback()
        {
            if (m_snippetSoundPlayer != null)
                m_snippetSoundPlayer.Play();

            if (m_snippetTextPlayer != null)
                m_snippetTextPlayer.Play();
        }

        private void ApplyControllerManagementState()
        {
            if (m_snippetAvatarPlayer != null)
                m_snippetAvatarPlayer.SetAnimationDrivenExternally(IsControllerManaged);

            if (IsControllerManaged)
                CancelPendingAutoPlay();
        }

        private void CancelPendingAutoPlay()
        {
            if (m_pendingAutoPlayCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_pendingAutoPlayCoroutine);
            m_pendingAutoPlayCoroutine = null;
        }

        private void CancelPendingSynchronizedStart()
        {
            if (m_pendingSynchronizedStartCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_pendingSynchronizedStartCoroutine);
            m_pendingSynchronizedStartCoroutine = null;
        }
    }
}
