using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace TextureFireWorks
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class FireworkLauncher : UdonSharpBehaviour
    {
        [Header("生成済みの花火")]
        public MeshRenderer effectRenderer;
        public Transform launchPoint;
        public Transform burstPoint;
        [Min(0.1f)] public float ascentDuration = 2f;
        [Min(0.1f)] public float spreadDuration = 1.2f;
        [Min(0f)] public float holdDuration = 2f;
        [Min(0.1f)] public float fadeDuration = 2f;
        [Header("音声（任意）")]
        public AudioSource ascentAudio;
        public AudioSource burstAudio;

        [UdonSynced] private int launchSequence;
        [UdonSynced] private double launchServerTime;
        private Material instanceMaterial;
        private int observedSequence;
        private bool initialized;
        private bool serializationPending;
        private bool burstSoundPlayed;
        private bool localPreview;
        private double localStartTime;

        private void Start()
        {
            Initialize();
            ObserveLaunch();
        }

        private void Initialize()
        {
            if (initialized || effectRenderer == null) return;
            instanceMaterial = effectRenderer.material;
            effectRenderer.enabled = false;
            initialized = true;
        }

        public override void Interact()
        {
            RequestLaunch();
        }

        public void RequestLaunch()
        {
            // Unity Play Mode without a VRChat player still supports a local preview.
            if (!Utilities.IsValid(Networking.LocalPlayer))
            {
                if (localPreview && Time.timeAsDouble - localStartTime < TotalDuration()) return;
                Initialize();
                localStartTime = Time.timeAsDouble;
                localPreview = true;
                BeginAudio(0f);
                return;
            }
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OwnerRequestLaunch));
        }

        public void OwnerRequestLaunch()
        {
            if (!Networking.IsOwner(gameObject) || serializationPending) return;
            double now = Networking.GetServerTimeInSeconds();
            if (launchSequence != 0 && now - launchServerTime < TotalDuration()) return;
            launchSequence = launchSequence == int.MaxValue ? 1 : launchSequence + 1;
            launchServerTime = now;
            serializationPending = true;
            RequestSerialization();
            ObserveLaunch();
        }

        public override void OnDeserialization()
        {
            ObserveLaunch();
        }

        public override void OnPostSerialization(SerializationResult result)
        {
            if (result.success) serializationPending = false;
            else if (Networking.IsOwner(gameObject))
                SendCustomEventDelayedSeconds(nameof(RetrySerialization), 1f);
        }

        public void RetrySerialization()
        {
            if (serializationPending && Networking.IsOwner(gameObject)) RequestSerialization();
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            // Preserve the synchronized timestamp, including when the previous owner leaves.
            serializationPending = false;
            if (Networking.IsOwner(gameObject) && launchSequence != 0) RequestSerialization();
        }

        private void ObserveLaunch()
        {
            Initialize();
            if (!initialized || launchSequence == 0 || launchSequence == observedSequence) return;
            observedSequence = launchSequence;
            localPreview = false;
            float elapsed = (float)(Networking.GetServerTimeInSeconds() - launchServerTime);
            BeginAudio(elapsed);
        }

        private void BeginAudio(float elapsed)
        {
            if (ascentAudio != null) ascentAudio.Stop();
            if (burstAudio != null) burstAudio.Stop();
            burstSoundPlayed = elapsed >= ascentDuration;
            if (elapsed >= 0f && elapsed < ascentDuration && ascentAudio != null && ascentAudio.clip != null)
            {
                ascentAudio.time = Mathf.Min(elapsed, Mathf.Max(0f, ascentAudio.clip.length - 0.01f));
                ascentAudio.Play();
            }
            // Late joiners see the current frame, without replaying old explosion sounds.
        }

        private float TotalDuration()
        {
            return ascentDuration + spreadDuration + holdDuration + fadeDuration;
        }

        private void Update()
        {
            if (!initialized) return;
            if (!localPreview && launchSequence == 0) return;
            float elapsed = localPreview ? (float)(Time.timeAsDouble - localStartTime)
                : (float)(Networking.GetServerTimeInSeconds() - launchServerTime);
            bool active = elapsed >= 0f && elapsed < TotalDuration();
            effectRenderer.enabled = active;
            if (!active)
            {
                if (ascentAudio != null && ascentAudio.isPlaying) ascentAudio.Stop();
                return;
            }
            instanceMaterial.SetFloat("_Elapsed", elapsed);
            instanceMaterial.SetVector("_Durations", new Vector4(ascentDuration, spreadDuration, holdDuration, fadeDuration));
            Transform space = effectRenderer.transform;
            if (launchPoint != null) instanceMaterial.SetVector("_LaunchPoint", space.InverseTransformPoint(launchPoint.position));
            if (burstPoint != null) instanceMaterial.SetVector("_BurstPoint", space.InverseTransformPoint(burstPoint.position));
            if (!burstSoundPlayed && elapsed >= ascentDuration)
            {
                burstSoundPlayed = true;
                if (ascentAudio != null) ascentAudio.Stop();
                if (burstAudio != null && burstAudio.clip != null) burstAudio.Play();
            }
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null) Destroy(instanceMaterial);
        }
    }
}
