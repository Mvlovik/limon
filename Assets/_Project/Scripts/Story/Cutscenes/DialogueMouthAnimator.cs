using UnityEngine;

namespace GameLimon.Story.Cutscenes
{
    /// <summary>Subtle mouth movement while dialogue or speech clips are playing.</summary>
    public sealed class DialogueMouthAnimator : MonoBehaviour
    {
        private static readonly string[] SpeechWords = { "dialogue", "speech", "talk", "phone", "alex", "chief", "rustam", "rita" };
        private Animator[] _animators = System.Array.Empty<Animator>();
        private SkinnedMeshRenderer[] _meshes = System.Array.Empty<SkinnedMeshRenderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindObjectOfType<DialogueMouthAnimator>() == null)
                new GameObject("[DialogueMouthAnimator]").AddComponent<DialogueMouthAnimator>();
        }

        private void Update()
        {
            bool speaking = false;
            foreach (AudioSource source in FindObjectsOfType<AudioSource>())
            {
                if (!source.isPlaying || source.clip == null) continue;
                string name = source.clip.name.ToLowerInvariant();
                foreach (string word in SpeechWords)
                    if (name.Contains(word)) { speaking = true; break; }
                if (speaking) break;
            }

            if (_animators.Length == 0) _animators = FindObjectsOfType<Animator>();
            foreach (Animator animator in _animators)
            {
                if (animator == null) continue;
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.name == "Talking" && parameter.type == AnimatorControllerParameterType.Bool)
                        animator.SetBool("Talking", speaking);
            }

            if (_meshes.Length == 0) _meshes = FindObjectsOfType<SkinnedMeshRenderer>();
            float amount = speaking ? 18f + Mathf.Sin(Time.unscaledTime * 18f) * 8f : 0f;
            foreach (SkinnedMeshRenderer mesh in _meshes)
            {
                if (mesh == null || mesh.sharedMesh == null) continue;
                for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                {
                    string name = mesh.sharedMesh.GetBlendShapeName(i).ToLowerInvariant();
                    if (name.Contains("mouth") || name.Contains("jaw") || name.Contains("viseme"))
                        mesh.SetBlendShapeWeight(i, amount);
                }
            }
        }
    }
}
