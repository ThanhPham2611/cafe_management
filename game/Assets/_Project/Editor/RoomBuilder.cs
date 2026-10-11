using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Xem trước phòng thuê cấp 2 ngay trong Editor (không cần Play): dựng bằng đúng EnvironmentBuilder runtime.
    /// Menu: Cafe/Tier 2/Preview Room và Cafe/Tier 2/Clear Preview. Đối tượng xem trước không được lưu vào scene (DontSave).
    /// Lưu ý: tường chỉ tự hạ thấp khi Play (RoomWall chạy LateUpdate), ở đây hiện đủ cao.
    public static class RoomBuilder
    {
        const string PreviewName = "Preview_Room_T2";

        [MenuItem("Cafe/Tier 2/Preview Room")]
        public static void Preview()
        {
            Clear();
            Material mat = ArtImporter.EnsureMaterial();
            GameObject root = EnvironmentBuilder.BuildRoom(mat, null);
            root.name = PreviewName;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("[Cafe] Đã dựng xem trước phòng thuê cấp 2 (không lưu vào scene).");
        }

        [MenuItem("Cafe/Tier 2/Clear Preview")]
        public static void Clear()
        {
            GameObject old = GameObject.Find(PreviewName);
            while (old != null)
            {
                Object.DestroyImmediate(old);
                old = GameObject.Find(PreviewName);
            }
        }
    }
}
