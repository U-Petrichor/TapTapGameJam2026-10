using TapTapGameJam.Core;
using TapTapGameJam.Cockroach;
using UnityEngine;

// 见 Guard.cs 顶部说明：Cockroach 在 TapTapGameJam 下既是命名空间又是实体类型，必须走别名
using CockroachAgent = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 杀虫饵（DrugFood）。<see cref="FoodItem"/> 的子类，由 <see cref="CleanerGuard"/> 定期放到地图上。
    ///
    /// 关键行为和普通食物不一样：
    /// <list type="bullet">
    /// <item>蟑螂不会"啃一口"就跑 —— 只要身体进入 <see cref="_killRadius"/> 就直接暴毙，
    /// 不等它把药吃进嘴里。所以这里主要靠 <see cref="FixedUpdate"/> 的距离检测，
    /// 但 <see cref="Consume"/> 也照常覆写成致命，防止别的调用路径（背包 / 其它系统）绕过。</item>
    /// <item>蟑螂死的同时，在<b>药所在位置</b>额外爆发一圈危险信息素 ——
    /// 蟑螂自己那一发是在它倒地处炸的，药自己再留一圈味，同伴才会绕开这块区域。
    /// 药是靠危险信息素"教"蟑螂别过来的，不是靠蟑螂长了记性。</item>
    /// <item>玩家不能捡：捡起来放回地图就变成玩家的杀蟑陷阱了，和守卫的职责冲突。</item>
    /// </list>
    /// </summary>
    public class CleanerDrugFoodItem : FoodItem
    {
        [Header("杀虫饵")]
        [Tooltip("被杀半径（世界单位）。蟑螂身体中心进入这个范围就立刻死亡")]
        [SerializeField] private float _killRadius = 0.6f;

        [Tooltip("额外爆发危险信息素的强度。<= 0 时沿用蟑螂自己的死亡爆发强度")]
        [SerializeField] private float _dangerBurstIntensity;

        [Tooltip("额外爆发危险信息素的半径（世界单位）")]
        [SerializeField] private float _dangerBurstRadius = 3f;

        [Tooltip("累计毒杀多少只蟑螂后彻底失效。<= 0 表示不限次数")]
        [SerializeField] private int _maxKills = 1;

        [Tooltip("失效时是否把物体留着（false = 直接销毁）")]
        [SerializeField] private bool _keepAfterUsedUp;

        [Header("表现（可留空）")]
        [Tooltip("被杀半径的可视范围。留空则运行时自动生成一个半透明小球")]
        [SerializeField] private Transform _radiusVisual;

        [Tooltip("被杀半径可视球的颜色")]
        [SerializeField] private Color _radiusVisualColor = new Color(0.55f, 1f, 0.35f, 0.22f);

        private readonly Collider[] _overlapBuffer = new Collider[16];
        private int _kills;
        private bool _spent;

        /// <summary>本颗药已经毒杀了几只蟑螂</summary>
        public int Kills => _kills;

        /// <summary>已经失效（达到击杀上限）</summary>
        public bool IsSpent => _spent;

        /// <summary>被杀半径（世界单位）</summary>
        public float KillRadius => _killRadius;

        protected override void Awake()
        {
            base.Awake();

            // 药是"陷阱"不是"口粮"：营养值只用来占位，保证它还留在食物名单里
            totalNutrition = Mathf.Max(1f, totalNutrition);

            EnsureRadiusVisual();
        }

        private void FixedUpdate()
        {
            if (_spent) return;

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _killRadius, _overlapBuffer, CockroachLayerMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider other = _overlapBuffer[i];
                if (other == null) continue;

                CockroachAgent cockroach = other.GetComponentInParent<CockroachAgent>();
                if (cockroach == null || cockroach.IsDead) continue;

                Kill(cockroach);

                // 一颗药可能一次毒到好几只：不提前 break，同一帧把范围里的都收掉，
                // 而 _spent 之后循环里会继续读到已死的蟑螂，所以这里直接返回
                if (_spent) return;
            }
        }

        /// <summary>让蟑螂吃下这颗药 = 暴毙。</summary>
        public override float Consume(float amount)
        {
            // 返回 0 表示"没吃到营养"：蟑螂那边看到 take &lt;= 0 会跳过后续的返程逻辑。
            // 它已经被毒死了，不该再进入 Return 状态
            return 0f;
        }

        private void Kill(CockroachAgent cockroach)
        {
            Vector2 drugPos = CockroachAgent.FlatOf(transform.position);

            // 危险信号要在蟑螂被销毁之前写下去：蟑螂自己 Die() 时会在它倒地处再炸一发
            BurstDanger(drugPos);
            cockroach.Die();

            _kills++;
            if (_maxKills > 0 && _kills >= _maxKills)
            {
                Expire();
            }
        }

        private void BurstDanger(Vector2 at)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            float intensity = _dangerBurstIntensity;
            if (intensity <= 0f)
            {
                CockroachSettings cs = CockroachManager.Instance != null ? CockroachManager.Instance.cockroachSetting : null;
                intensity = cs != null ? cs.MarkerMax * cs.DangerBurstMul : 80f;
            }

            if (intensity <= 0f || _dangerBurstRadius <= 0f) return;

            grid.BurstDanger(at, intensity, _dangerBurstRadius);
        }

        /// <summary>用完了：从地图上撤掉（或留个空壳）。</summary>
        private void Expire()
        {
            _spent = true;

            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.Unregister(this);
            }

            if (!_keepAfterUsedUp)
            {
                Destroy(gameObject);
            }
        }

        #region 玩家交互

        /// <summary>药饵不给捡：捡起来再放回地图就成了玩家的杀蟑陷阱，和守卫的职责冲突</summary>
        public override bool CanInteract(Player.PlayerBasic player) => false;

        public override string HoverPrompt => "杀虫饵";

        public override string InteractionPrompt => "杀虫饵（不可拾取）";

        #endregion

        #region 表现

        private static int CockroachLayerMask => LayerMask.GetMask("Cockroach");

        /// <summary>没有美术资源时兜一个半透明小球，让玩家和调试时的自己都看得见杀死范围。</summary>
        private void EnsureRadiusVisual()
        {
            if (_radiusVisual == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "KillRadius";
                Destroy(go.GetComponent<Collider>());     // 只要视觉，不要额外的物理体
                go.transform.SetParent(transform, false);
                _radiusVisual = go.transform;
            }

            _radiusVisual.localPosition = Vector3.zero;
            _radiusVisual.localScale = Vector3.one * (_killRadius * 2f);

            Renderer r = _radiusVisual.GetComponent<Renderer>();
            if (r == null) return;

            // 运行时新建材质实例：不能直接改 sharedMaterial，否则同一预制体的所有药饵会一起变色
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;

            Material mat = new Material(shader);
            mat.color = _radiusVisualColor;
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        #endregion
    }
}
