using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Утилиты для чистки блок-аута. Отрицательный масштаб у BoxCollider
    /// вызывает предупреждение Unity и переворачивает эффективный размер коллайдера.
    /// </summary>
    public static class SceneSanitizer
    {
        [MenuItem("Tools/GameLimon/Fix Negative BoxCollider Scales")]
        public static void FixNegativeBoxColliderScales()
        {
            int fixedCount = 0;

            foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include))
            {
                if (box == null)
                    continue;

                // Трогаем только примитивы-кубы: у них отражение по оси визуально
                // ничего не меняет, а коллайдер становится корректным.
                MeshFilter filter = box.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.name != "Cube")
                    continue;

                Vector3 scale = box.transform.localScale;
                if (scale.x >= 0f && scale.y >= 0f && scale.z >= 0f)
                    continue;

                Undo.RecordObject(box.transform, "Fix negative collider scale");
                box.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                Scene scene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[SceneSanitizer] Исправлено кубов с отрицательным масштабом: {fixedCount}. Сохраните сцену.");
            }
            else
            {
                Debug.Log("[SceneSanitizer] Кубы с отрицательным масштабом не найдены.");
            }
        }
    }
}
