using UnityEngine;

namespace Cafe
{
    /// Ly để lại trên bàn/đất. Có mặt từ lúc phục vụ; chỉ thu được sau khi khách rời đi.
    /// Ly chưa thu quá 30 giây in-game sẽ chặn ghế (Seat.IsBlocked).
    public class LeftoverCup : MonoBehaviour, ITappable
    {
        private GameBootstrap game;
        private Seat seat;
        private Transform visual;
        private bool collectable;
        private float collectableSince;
        private float pulsePhase;
        private CupCollectBubble bubble;

        public bool IsCollectable { get { return collectable; } }
        public float CollectableSince { get { return collectableSince; } }
        public Seat Seat { get { return seat; } }

        public static LeftoverCup Spawn(GameBootstrap game, Seat seat)
        {
            Vector3 pos = game.Grid.GetCupPoint(seat);
            if (seat.Cup != null)
                pos += new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.08f, 0.08f));

            var go = new GameObject("LeftoverCup");
            go.transform.position = pos;
            var cup = go.AddComponent<LeftoverCup>();
            cup.Init(game, seat);
            return cup;
        }

        private void Init(GameBootstrap g, Seat s)
        {
            game = g;
            seat = s;
            pulsePhase = Random.value * 6f;

            GameObject prefab = g.CupPrefab;
            if (prefab != null)
            {
                GameObject v = Instantiate(prefab, transform.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                v.name = "Visual";
                Collider[] cols = v.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);
                visual = v.transform;
            }

            // Vùng chạm rộng hơn ly một chút cho ngón tay
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.35f, 0.35f, 0.35f);
            box.center = new Vector3(0f, 0.15f, 0f);

            seat.Cup = this;
            g.LeftoverCups.Add(this);
        }

        public void SetCollectable(float inGameNow)
        {
            collectable = true;
            collectableSince = inGameNow;
            // Báo cho người chơi: khách uống xong, ly chạm được để thu (bong bóng + chữ nổi một lần)
            if (game != null && game.Hud != null && game.Hud.BubbleLayer != null && bubble == null)
                bubble = CupCollectBubble.Create(game.Hud.BubbleLayer, transform);
            UiFx.FloatText(transform.position + new Vector3(0f, 0.4f, 0f), Strings.CupReady, HudBuilder.Mint);
        }

        private float Urgency()
        {
            float rate = game.Day != null ? game.Day.InGameRate : GameBalance.InGameSecondsPerRealSecond;
            return CupCollectRules.Urgency(game.Day.InGameSeconds, collectableSince, GameBalance.LeftoverBlockRealSeconds * rate);
        }

        private void Update()
        {
            if (!collectable) return;
            float u = Urgency();
            if (bubble != null) bubble.SetUrgency(u);
            if (visual == null) return;
            pulsePhase += Time.deltaTime * CupCollectRules.PulseSpeed(u);
            float s = 1f + 0.08f * Mathf.Sin(pulsePhase);
            visual.localScale = new Vector3(s, s, s);
        }

        public void OnTap()
        {
            if (!collectable) return;
            // Chủ quán đi thu; ly vào kho khi chủ quán về xe đẩy
            if (game.Owner != null) game.Owner.QueueCollect(this);
            else Collect(false);
        }

        /// Chủ quán nhặt ly lên tay: ly biến khỏi bàn (ghế được giải phóng), chưa về kho.
        /// Kho ly và tiền ve chai do chủ quán xử lý khi về tới xe đẩy.
        public void PickUp()
        {
            game.LeftoverCups.Remove(this);
            if (seat != null && seat.Cup == this) seat.Cup = null;
            Destroy(gameObject);
        }

        /// silent = true: thu tự động (qua đêm), không tính tiền ve chai.
        public void Collect(bool silent)
        {
            game.Stock.Return();
            if (!silent)
            {
                game.Wallet.Earn(GameBalance.ScrapXu);
                if (game.Day.Summary != null) game.Day.Summary.scrap += GameBalance.ScrapXu;
            }
            game.LeftoverCups.Remove(this);
            if (seat != null && seat.Cup == this) seat.Cup = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (bubble != null) Destroy(bubble.gameObject);
            if (game != null) game.LeftoverCups.Remove(this);
            if (seat != null && seat.Cup == this) seat.Cup = null;
        }
    }
}
