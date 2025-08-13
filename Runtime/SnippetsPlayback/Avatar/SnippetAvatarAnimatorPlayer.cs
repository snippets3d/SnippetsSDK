using System;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Implements the playback of the animation of a snippet using an Animator component.
    /// </summary>
    public class SnippetAvatarAnimatorPlayer : SnippetAvatarPlayer
    {
        /// <summary>
        /// The legacy Animation component used for playing the animations.
        /// </summary>
        [SerializeField]
        private Animation m_animation;

        /// <inheritdoc />
        public override AnimationClip Value
        {
            get => m_animation.clip;
            set
            {
                // if there is no animation component, create it
                if (m_animation == null)
                    InitializeAnimationComponentForClip(value);

                // Remove the previous clip and substitute it with the new one
                if (Value != null)
                    m_animation.RemoveClip(Value);

                m_animation.clip = value;

                if (value != null)
                    m_animation.AddClip(value, value.name);
            }
        }

        /// <inheritdoc />
        public override bool IsPlaying { get; protected set; }

        /// <inheritdoc />
        public override void Play()
        {
            if (m_animation != null)
            {
                m_animation.Play();
                IsPlaying = true;
                PlaybackStarted?.Invoke();
            }
        }

        /// <inheritdoc />
        public override void Stop()
        {
            StopInternal();
        }

        /// <summary>
        /// Stops the playback of the element.
        /// </summary>
        /// <param name="notifyEvent">True to send the stop event, false otherwise</param>
        private void StopInternal(bool notifyEvent = true)
        {
            if (m_animation != null)
            {
                m_animation.Stop();
                IsPlaying = false;

                if (notifyEvent)
                    PlaybackStopped?.Invoke();
            }
        }

        /// <summary>
        /// Update is called once per frame
        /// </summary>
        private void Update()
        {
            // Check if animation has finished playing and if so, trigger the stop event
            if (m_animation != null && IsPlaying && !m_animation.isPlaying)
            {
                Stop();
            }
        }

        /// <summary>
        /// Awake
        /// </summary>
        private void Awake()
        {
            StopInternal(false);
        }

        /// <summary>
        /// Creates the animation component to play the given clip.
        /// </summary>
        /// <param name="clip">The clip that must be played</param>
        /// <exception cref="ArgumentException">If no avatar is attached to this object, and it can't be reconstructed from the provided Clip (in editor)</exception>
        /// <exception cref="InvalidOperationException">If no avatar is attached to this object (at runtime)</exception>
        private void InitializeAnimationComponentForClip(AnimationClip clip)
        {
            // We need a child avatar object to get the animation component from

            // Check that we have a child object that looks like a potential avatar root
            // (for now the criterias are: we have a child object, and it has a SkinnedMeshRenderer)
            var childTransform = transform.childCount > 0 ? transform.GetChild(0) : null;

            //if we do not have it
            if (childTransform == null || childTransform.GetComponentInChildren<SkinnedMeshRenderer>() == null)
            {
#if UNITY_EDITOR
                //in case we are in the editor, we can resort to a last hope: if the animation clip is saved as a prefab,
                //usually, it is in the prefab of the avatar.
                //So, in this case we instantiate the prefab of the avatar+animation
                //and put it as a child of the current object 

                var animationClipAssetPath = UnityEditor.AssetDatabase.GetAssetPath(clip);

                if (UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(animationClipAssetPath) != typeof(GameObject))
                    throw new ArgumentException("No Avatar Provided in the Snippet Player and it is impossible to reconstruct an avatar from the AnimationClip");

                var animationPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(animationClipAssetPath);
                childTransform = GameObject.Instantiate(animationPrefab, transform).transform;
#else
                //in case we are in runtime, we can do nothing: we have to throw an exception
                throw new InvalidOperationException("No Avatar Provided in the Snippet Player, it is impossible to configure the Snippet Player");
#endif
            }

            //at this point we are sure to have a child with an avatar, in a way or in another

            //try to see if it has already an animation component: if it has it, use it
            //otherwise add the animation component to the child object
            var avatarRoot = childTransform.gameObject;
            m_animation = avatarRoot.GetComponentInChildren<Animation>();

            if (m_animation == null)
            {
                m_animation = avatarRoot.AddComponent<Animation>();
            }
            
            //we have to play the animation manually
            m_animation.playAutomatically = false;
        }
    }
}
