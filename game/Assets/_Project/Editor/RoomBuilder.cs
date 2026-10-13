using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Xem trước phòng cấp 2 / cấp 3 ngay trong Editor (không cần Play): dựng bằng đúng EnvironmentBuilder runtime.
    /// Menu: Cafe/Tier 2/Preview Room và Cafe/Tier 2/Clear Preview. Đối tượng xem trước không được lưu vào scene (DontSave).
    /// Lưu ý: tường chỉ tự hạ thấp khi Play (RoomWall chạy LateUpdate), ở đây hiện đủ cao.
    public static class RoomBuilder
    {
        const string PreviewName = "Preview_Room_T2";

        [MenuItem("Cafe/Tier 2/Preview Room")]
        public static void Preview()
        {
            Preview(RoomLayout.Tier2);
        }

        [MenuItem("Cafe/Tier 3/Preview Restaurant")]
        public static void PreviewTier3()
        {
            Preview(RoomLayout.Tier3);
        }

        private static void Preview(RoomSpec spec)
        {
            Clear();
            Material mat = ArtImporter.EnsureMaterial();
            GameObject root = EnvironmentBuilder.BuildRoom(mat, null, spec);
            root.name = PreviewName;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("[Cafe] Đã dựng xem trước phòng cấp " + spec.Tier + " (không lưu vào scene).");
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
