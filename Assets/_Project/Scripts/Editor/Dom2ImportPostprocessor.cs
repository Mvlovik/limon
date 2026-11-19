using System;
using UnityEditor;
using UnityEngine;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// The source dom2 FBX contains five large coplanar floor "Плоскость" meshes.
    /// They overlap the brick floor blocks, which causes z-fighting: at grazing
    /// angles the untextured default material bleeds through the facade.
    /// The floor blocks already provide the visible surfaces, so the redundant
    /// planes are hidden on every import.
    /// </summary>
    public sealed class Dom2ImportPostprocessor : AssetPostprocessor
    {
        private const string TargetSuffix = "/dom2_tex_embedded.fbx";

        private void OnPostprocessModel(GameObject root)
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.EndsWith(TargetSuffix, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.gameObject.name.StartsWith("Плоскость", StringComparison.Ordinal))
                    renderer.enabled = false;
            }
        }
    }
}
