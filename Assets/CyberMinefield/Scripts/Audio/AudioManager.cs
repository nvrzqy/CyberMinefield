using UnityEngine;

namespace CyberMinefield.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const string SfxVolumeKey = "CyberMinefield.Audio.SfxVolume";
        private const string MusicVolumeKey = "CyberMinefield.Audio.MusicVolume";
        private const string UserSfxRoot = "Audio/UserSfx/Sound Effects/";

        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioClip scanClip;
        [SerializeField] private AudioClip defuserClip;
        [SerializeField] private AudioClip errorClip;
        [SerializeField] private AudioClip explosionClip;
        [SerializeField] private AudioClip missionCompleteClip;
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip footstepClip;
        [SerializeField] private AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.65f;
        [SerializeField, Range(0.1f, 2.5f)] private float winVolumeBoost = 1.55f;

        public float SfxVolume => sfxVolume;
        public float MusicVolume => musicVolume;

        private void Awake()
        {
            EnsureSources();
            LoadSavedVolumes();
            LoadDefaultClips();
            ApplyVolumes();
            PlayMusic();
        }

        public void PlayScan()
        {
            PlayOneShot(scanClip);
        }

        public void PlayUiClick()
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
            PlayOneShot(missionCompleteClip, winVolumeBoost);
        }

        public void PlayJump()
        {
            PlayOneShot(jumpClip);
        }

        public void PlayFootsteps(bool moving)
        {
            if (footstepSource == null || footstepClip == null)
            {
                return;
            }

            if (!moving)
            {
                if (footstepSource.isPlaying)
                {
                    footstepSource.Stop();
                }

                return;
            }

            if (!footstepSource.isPlaying)
            {
                footstepSource.clip = footstepClip;
                footstepSource.loop = true;
                footstepSource.Play();
            }
        }

        public void StopFootsteps()
        {
            PlayFootsteps(false);
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        private void EnsureSources()
        {
            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            if (footstepSource == null)
            {
                footstepSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            footstepSource.playOnAwake = false;
            footstepSource.loop = true;
        }

        private void LoadSavedVolumes()
        {
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
        }

        private void LoadDefaultClips()
        {
            scanClip = scanClip != null ? scanClip : LoadClip("mouse click sound effect");
            defuserClip = defuserClip != null ? defuserClip : LoadClip("put flag difuser sound effect");
            errorClip = errorClip != null ? errorClip : LoadClip("mouse click sound effect");
            explosionClip = explosionClip != null ? explosionClip : LoadClip("game over sound effect");
            missionCompleteClip = missionCompleteClip != null ? missionCompleteClip : LoadClip("win sound effect");
            jumpClip = jumpClip != null ? jumpClip : LoadClip("jump");
            footstepClip = footstepClip != null ? footstepClip : LoadClip("Walking Sound Effect");
            musicClip = musicClip != null ? musicClip : LoadClip("main music backsound");
        }

        private static AudioClip LoadClip(string clipName)
        {
            return Resources.Load<AudioClip>(UserSfxRoot + clipName);
        }

        private void ApplyVolumes()
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }

            if (footstepSource != null)
            {
                footstepSource.volume = sfxVolume;
            }

            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
        }

        private void PlayMusic()
        {
            if (musicSource == null || musicClip == null)
            {
                return;
            }

            if (musicSource.clip != musicClip)
            {
                musicSource.clip = musicClip;
            }

            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        private void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null && sfxSource != null && sfxVolume > 0f)
            {
                sfxSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
            }
        }
    }
}
