using UnityEngine;

namespace Cafe
{
    /// Hình dạng khách: prefab chibi modular (Hips/Head/Arm_L/Arm_R/Leg_L/Leg_R) hoạt họa bằng code,
    /// hoặc capsule tạm nếu không có prefab. Mọi trục xoay tính theo hướng thế giới của Model nên không phụ thuộc
    /// việc FBX importer có xoay thêm các node con hay không.
    public class CustomerVisual : MonoBehaviour
    {
        private static readonly int SwatchId = Shader.PropertyToID("_SwatchIndex");

        /// Khi ngồi, khớp hông cách mặt ghế (gốc của Customer đặt ở SitPoint) chừng này mét.
        private const float SeatHipHeight = 0.08f;
        private const float HappyDuration = 0.6f;
        private const float AngryDuration = 1.2f;
        private const float DrinkCycle = 4.5f;

        private class Joint
        {
            public Transform t;
            public Quaternion restRot;
            public Vector3 restPos;
            public Vector3 sideAxis; // trục "phải" của Model, trong không gian local của parent
            public Vector3 upAxis;   // trục lên của Model
            public Vector3 fwdAxis;  // trục trước của Model

            public void Setup(Transform model)
            {
                restRot = t.localRotation;
                restPos = t.localPosition;
                Transform p = t.parent;
                sideAxis = p.InverseTransformDirection(model.right).normalized;
                upAxis = p.InverseTransformDirection(model.up).normalized;
                fwdAxis = p.InverseTransformDirection(model.forward).normalized;
            }

            /// deg dương = vung ra trước (chi buông xuống -> đưa ra trước). roll: quanh trục trước. yaw: quanh trục lên.
            public void Swing(float deg, float rollDeg = 0f, float yawDeg = 0f)
            {
                Quaternion q = Quaternion.AngleAxis(-deg, sideAxis)
                             * Quaternion.AngleAxis(rollDeg, fwdAxis)
                             * Quaternion.AngleAxis(yawDeg, upAxis);
                t.localRotation = q * restRot;
            }
        }

        private Transform model; // wrapper để squash/hop/lắc
        private Joint hips, head, armL, armR, legL, legR;
        private bool rigged;
        private float standHipY; // cao khớp hông khi đứng (không gian Model)
        private float phase;
        private float clock;
        private float walkBlend;
        private float sitBlend;
        private bool drinking;
        private float happyT = -1f;
        private float angryT = -1f;
        private float drinkClock;
        private bool carrying;
        private float carryBlend;
        private Transform carryAnchor;

        /// Chỗ gắn ly khi cầm (HandSocket_R nếu prefab có, không thì điểm trước ngực). Null nếu chưa Build.
        public Transform CarryAnchor { get { return carryAnchor; } }
        public bool IsRigged { get { return rigged; } }

        // ---------------------------------------------------------------- Build

        /// Xóa mô hình hiện tại (để Build lại với prefab khác, vd. đổi dáng chủ quán).
        public void Clear()
        {
            if (model != null) Destroy(model.gameObject);
            model = null;
            carryAnchor = null;
            rigged = false;
            hips = head = armL = armR = legL = legR = null;
            walkBlend = sitBlend = carryBlend = 0f;
            drinking = false;
            happyT = angryT = -1f;
        }

        public void Build(GameObject prefab, Material fallbackMaterial)
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            clock = Random.value * 10f;

            if (prefab == null)
            {
                BuildFallback(fallbackMaterial);
                return;
            }

            GameObject inst = Instantiate(prefab, model, false);
            inst.name = "Char";
            // Collider chạm đã nằm trên Customer; bỏ collider của prefab để khỏi trùng.
            foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Destroy(c);

            hips = Find(inst.transform, "Hips");
            head = Find(inst.transform, "Head");
            armL = Find(inst.transform, "Arm_L");
            armR = Find(inst.transform, "Arm_R");
            legL = Find(inst.transform, "Leg_L");
            legR = Find(inst.transform, "Leg_R");
            rigged = hips != null && head != null && armL != null && armR != null && legL != null && legR != null;
            if (!rigged)
            {
                Debug.LogWarning("[Cafe] Prefab khách thiếu Hips/Head/Arm_*/Leg_*: " + prefab.name);
                return;
            }

            hips.Setup(model);
            head.Setup(model);
            armL.Setup(model);
            armR.Setup(model);
            legL.Setup(model);
            legR.Setup(model);
            standHipY = model.InverseTransformPoint(hips.t.position).y;

            // Chỗ cầm ly: HandSocket_R, không có thì điểm trước ngực (gắn vào Model nên không lắc theo tay)
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "HandSocket_R") { carryAnchor = t; break; }
            }
            if (carryAnchor == null)
            {
                var a = new GameObject("CarryPoint").transform;
                a.SetParent(model, false);
                a.localPosition = new Vector3(0.2f, 0.55f, 0.24f);
                carryAnchor = a;
            }
        }

        /// Cầm đồ: nâng tay phải lên một chút.
        public void SetCarry(bool v)
        {
            carrying = v;
        }

        /// Áp màu chủ quán: renderer có tên GameObject bắt đầu bằng Shirt/Hair/Skin (đệ quy).
        /// Capsule tạm (không có prefab): Body = áo, Head = da, Hair = tóc.
        public void ApplyOwnerLook(OwnerLook look)
        {
            if (model == null) return;
            Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                string n = rs[i].gameObject.name;
                int swatch = -1;
                if (n.StartsWith("Shirt")) swatch = look.shirtSwatch;
                else if (n.StartsWith("Hair")) swatch = look.hairSwatch;
                else if (n.StartsWith("Skin")) swatch = look.skinSwatch;
                else if (!rigged && n == "Body") swatch = look.shirtSwatch;
                else if (!rigged && n == "Head") swatch = look.skinSwatch;
                if (swatch < 0) continue;
                var mpb = new MaterialPropertyBlock();
                rs[i].GetPropertyBlock(mpb);
                mpb.SetFloat(SwatchId, swatch);
                rs[i].SetPropertyBlock(mpb);
            }
        }

        private static Joint Find(Transform root, string namePrefix)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name.StartsWith(namePrefix))
                    return new Joint { t = t };
            }
            return null;
        }

        private void BuildFallback(Material material)
        {
            int[] shirts = { 26, 28, 30, 58, 59, 60, 61 };
            int skin = Random.Range(52, 56);
            int shirt = shirts[Random.Range(0, shirts.Length)];
            MakePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.34f, 0.4f, 0.34f), material, shirt);
            MakePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.95f, 0f), new Vector3(0.42f, 0.42f, 0.42f), material, skin);
            MakePart(PrimitiveType.Sphere, "Hair", new Vector3(0f, 1.05f, 0.02f), new Vector3(0.44f, 0.3f, 0.44f), material, 62);
        }

        private void MakePart(PrimitiveType type, string partName, Vector3 localPos, Vector3 scale, Material material, int swatch)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(model, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            var mpb = new MaterialPropertyBlock();
            mpb.SetFloat(SwatchId, swatch);
            r.SetPropertyBlock(mpb);
        }

        // ---------------------------------------------------------------- Pose

        public void SetPose(CustomerPose pose)
        {
            switch (pose)
            {
                case CustomerPose.Happy: happyT = 0f; break;
                case CustomerPose.Angry: angryT = 0f; break;
                case CustomerPose.Drink: drinking = true; drinkClock = 0f; break;
                default: drinking = false; break; // Idle / Walk / Sit
            }
        }

        // ---------------------------------------------------------------- Tick

        /// speed: m/s; sitting: hạ khớp hông xuống mặt ghế. dt = Time.deltaTime (có time scale).
        public void Tick(float speed, bool sitting, float dt)
        {
            if (model == null) return;
            clock += dt;
            bool walking = speed > 0.1f;
            if (walking) phase += dt * (5f + speed * 3f);
            walkBlend = Mathf.MoveTowards(walkBlend, walking ? 1f : 0f, dt * 6f);
            sitBlend = Mathf.MoveTowards(sitBlend, sitting ? 1f : 0f, dt * 5f);
            carryBlend = Mathf.MoveTowards(carryBlend, carrying ? 1f : 0f, dt * 5f);
            if (happyT >= 0f) { happyT += dt; if (happyT > HappyDuration) happyT = -1f; }
            if (angryT >= 0f) { angryT += dt; if (angryT > AngryDuration) angryT = -1f; }
            if (drinking && sitting) drinkClock += dt;

            if (!rigged)
            {
                TickFallback(walking);
                return;
            }

            float w = walkBlend * (1f - sitBlend);
            float s = Mathf.Sin(phase);
            float breathe = Mathf.Sin(clock * 2.2f);

            // Hạ khớp hông xuống ghế + nhấp nhô khi đi
            float drop = Mathf.Max(0f, standHipY - SeatHipHeight) * sitBlend;
            float bob = Mathf.Abs(s) * 0.035f * w;
            hips.t.localPosition = hips.restPos + hips.upAxis * (bob - drop);
            hips.Swing(5f * w + 4f * sitBlend, s * 3f * w);

            // Squash & stretch, nhảy mừng, giận trên wrapper
            float squash = 1f + 0.035f * Mathf.Cos(phase * 2f) * w + 0.012f * breathe * (1f - w);
            float hop = 0f;
            float armsUp = 0f;
            if (happyT >= 0f)
            {
                float k = happyT / HappyDuration;
                hop = Mathf.Sin(k * Mathf.PI) * 0.22f;
                squash += 0.08f * Mathf.Sin(k * Mathf.PI * 2f);
                armsUp = Mathf.Sin(Mathf.Clamp01(k * 1.2f) * Mathf.PI * 0.5f) * (1f - Mathf.Clamp01((k - 0.75f) * 4f));
            }
            float shake = 0f;
            if (angryT >= 0f)
            {
                float k = angryT / AngryDuration;
                shake = Mathf.Sin(angryT * 38f) * 9f * (1f - k);
                squash -= 0.03f * (1f - k);
            }
            float inv = 1f / Mathf.Sqrt(squash);
            model.localPosition = new Vector3(0f, hop, 0f);
            model.localScale = new Vector3(inv, squash, inv);
            model.localRotation = Quaternion.Euler(0f, shake, 0f);

            // Chân: đi đối pha; ngồi thì duỗi 88 độ ra trước, hơi choãi
            float legSwing = 38f * s * w;
            float sitLeg = 88f * sitBlend;
            legL.Swing(legSwing + sitLeg, -4f * sitBlend);
            legR.Swing(-legSwing + sitLeg, 4f * sitBlend);

            // Tay: đối pha với chân; ngồi thì đặt xuôi ra trước; uống; mừng; giận
            float armSwing = 32f * s * w;
            float armRestSit = 22f * sitBlend;
            float aL = -armSwing + armRestSit;
            float aR = armSwing + armRestSit;
            float outL = 0f, outR = 0f;

            float drinkEnv = 0f;
            if (drinking && sitBlend > 0.9f)
            {
                float c = drinkClock % DrinkCycle;
                // 0..0.6 nâng, 0.6..1.6 giữ, 1.6..2.2 hạ, phần còn lại nghỉ
                drinkEnv = Smooth(c / 0.6f) * (1f - Smooth((c - 1.6f) / 0.6f));
            }
            aR = Mathf.Lerp(aR, 128f, drinkEnv);
            if (carryBlend > 0.001f) aR = Mathf.Lerp(aR, 62f, carryBlend * (1f - sitBlend));

            if (armsUp > 0f)
            {
                aL = Mathf.Lerp(aL, 165f, armsUp);
                aR = Mathf.Lerp(aR, 165f, armsUp);
                outL = 10f * armsUp;
                outR = 10f * armsUp;
            }
            if (angryT >= 0f)
            {
                float k = 1f - angryT / AngryDuration;
                aL = Mathf.Lerp(aL, 25f + Mathf.Sin(angryT * 45f) * 6f, k);
                aR = Mathf.Lerp(aR, 25f - Mathf.Sin(angryT * 45f) * 6f, k);
            }
            armL.Swing(aL, -outL);
            armR.Swing(aR, outR);

            // Đầu: gật nhẹ khi đi, ngửa khi uống, lắc khi giận
            float headNod = -3f * Mathf.Abs(s) * w + 2f * breathe * (1f - w) - 10f * drinkEnv;
            float headYaw = angryT >= 0f ? Mathf.Sin(angryT * 30f) * 14f * (1f - angryT / AngryDuration) : 0f;
            head.Swing(headNod, 0f, headYaw);
        }

        private void TickFallback(bool walking)
        {
            float bob = walking ? Mathf.Abs(Mathf.Sin(phase)) * 0.05f : 0f;
            float lean = walking ? Mathf.Sin(phase) * 4f : 0f;
            model.localPosition = new Vector3(0f, bob - 0.15f * sitBlend, 0f);
            model.localRotation = Quaternion.Euler(0f, 0f, lean);
            model.localScale = new Vector3(1f, 1f - 0.12f * sitBlend, 1f);
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
