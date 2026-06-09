using UnityEngine;

namespace CyberMinefield.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip scanClip;
        [SerializeField] private AudioClip defuserClip;
        [SerializeField] private AudioClip errorClip;
        [SerializeField] private AudioClip explosionClip;
        [SerializeField] private AudioClip missionCompleteClip;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public void PlayScan()
        {
            PlayOneShot(scanClip);
        }

        public void PlayDefuser()
        {
            PlayOneShot(defuserClip);
        }

        public void PlayError()
        {
            PlayOneShot(errorClip);
        }

        public void PlayExplosion()
        {
            PlayOneShot(explosionClip);
        }

        public void PlayMissionComplete()
        {
            PlayOneShot(missionCompleteClip);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
