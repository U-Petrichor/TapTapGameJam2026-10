using System.Collections.Generic;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Player
{
    /// <summary>
    /// 把背包装备槽里的饰品"穿到蟑螂身上"。
    ///
    /// 【数据从哪来】只订阅 <see cref="PlayerBag.EquippedChanged"/>。
    /// 穿 / 换 / 脱 / 丢 / 拾取自动穿，全都收敛在 PlayerBag.SetEquipped 这个唯一写入口上，
    /// 所以这里一行都不需要改装备逻辑，也不需要界面配合 —— 和 PlayerBuffs.RefreshDress 同一个套路。
    ///
    /// 【表现怎么做】**目前只给头部槽生成模型**，挂到蟑螂的 Head 骨骼下面。
    /// 身体 / 脚部的装备照常能穿、照常给 Buff，只是不再有 3D 外观（见 <see cref="VisibleSlot"/>）。
    /// 挂上之后不需要每帧同步：动画改的是骨骼，子物自然跟着动。
    ///
    /// 【要恢复身体 / 脚部】把 <see cref="VisibleSlot"/> 换成一个槽位集合，
    /// 再给对应 Dress 预制体挂回 <see cref="DressWornVisual"/> 即可 —— 骨骼、镜像、
    /// 左右脚生成这些逻辑都还留着，没有删。
    ///
    /// 【为什么必须复制一份】装备后物品本体被 PlayerBag 挪到暂存区（Y+10000）并隐藏，
    /// 那份本体是"回世界后要在地上显示的东西"，还带着 Interactable 碰撞体和气泡 Canvas。
    /// 直接把它搬到骨骼上，丢弃 / 换下时位置和显示都会乱。
    ///
    /// 【日夜不用管】蟑螂模型（zhanglang(5)）白天是 SetActive(false)，
    /// 骨骼下面的穿戴物跟着父级一起隐藏，白天天然不显示。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDressView : MonoBehaviour
    {
        /// <summary>
        /// 唯一会生成 3D 外观的槽位。
        /// 身体（背部）和脚部现在只保留玩法数据，不做外观，所以这里筛掉它们。
        /// </summary>
        private const EquipSlot VisibleSlot = EquipSlot.Head;

        #region 配置

        [Header("引用")]
        [Tooltip("装备数据源。留空则从自己身上找 PlayerBag")]
        [SerializeField] private PlayerBag _bag;

        [Tooltip("蟑螂（夜晚）模型根物体 zhanglang(5)。留空则问 PlayerAnimationManager 要")]
        [SerializeField] private Transform _nightModelRoot;

        #endregion

        #region 运行时数据

        /// <summary>当前挂在骨骼上的全部实例。槽位变化时全拆全建</summary>
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>骨骼解析缓存（按具体骨骼，不含 Auto）</summary>
        private readonly Dictionary<WornBone, Transform> _bones = new Dictionary<WornBone, Transform>();

        /// <summary>已经报过"找不到骨骼"的骨骼，避免每次换装都刷屏</summary>
        private readonly HashSet<WornBone> _reportedMissing = new HashSet<WornBone>();

        #endregion

        #region 生命周期

        private void Awake()
        {
            ResolveBag();
            ResolveNightModel();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Clear();
        }

        private void Start()
        {
            // 订阅可能比"开局就已经穿着装备"更晚（一进场景就是夜晚的情况），补一次
            Refresh();
        }

        private void OnDestroy()
        {
            Clear();
        }

        #endregion

        #region 刷新

        /// <summary>
        /// 按当前装备槽重建全部穿戴物。
        ///
        /// **必须幂等**：PlayerBag.TryEquip 换装时会连发两次事件（先清旧槽、再写新槽），
        /// 所以这里不做增量 diff，直接全拆全建 —— 少一种"残留一个旧模型"的 bug。
        /// </summary>
        [ContextMenu("重新生成穿戴物")]
        public void Refresh()
        {
            Clear();

            if (_bag == null || _nightModelRoot == null)
            {
                return;
            }

            IReadOnlyList<PickableItem> equipped = _bag.EquippedItems;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i] is not DressItem dress)
                {
                    continue;
                }

                EquipSlot slot = (EquipSlot)i;
                if (slot != VisibleSlot)
                {
                    // 身体 / 脚部：装备照常生效（Buff 由 PlayerBuffs 算），只是不生成 3D 外观
                    continue;
                }

                DressWornVisual visual = dress.GetComponent<DressWornVisual>();
                if (visual == null)
                {
                    Debug.LogWarning(
                        $"[PlayerDressView]：{dress.name} 上没有 DressWornVisual，穿上不会有外观。",
                        dress);
                    continue;
                }

                if (slot == EquipSlot.Legs)
                {
                    // 足部：左右脚各一份。VisibleSlot 现在只放行 Head，所以这段暂时走不到；
                    // 以后恢复脚部外观时把 VisibleSlot 放开就立刻生效，不用重写生成逻辑
                    Spawn(dress, visual, slot, false);
                    Spawn(dress, visual, slot, true);
                }
                else
                {
                    Spawn(dress, visual, slot, false);
                }
            }
        }

        /// <summary>拆掉当前所有穿戴物</summary>
        private void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i]);
                }
            }

            _spawned.Clear();
        }

        #endregion

        #region 生成

        private void Spawn(DressItem dress, DressWornVisual visual, EquipSlot slot, bool rightFoot)
        {
            WornAttach attach = visual.GetAttach(slot, rightFoot);

            Transform bone = ResolveBone(attach.bone);
            if (bone == null)
            {
                ReportMissingBone(attach.bone);
                return;
            }

            GameObject modelPrefab = visual.ModelPrefab;
            if (modelPrefab == null)
            {
                Debug.LogWarning(
                    $"[PlayerDressView]：{dress.name} 的 DressWornVisual 找不到穿戴模型（_wornModelPrefab 为空，子物体里也没有 Renderer）。",
                    dress);
                return;
            }

            GameObject worn = Instantiate(modelPrefab, bone, false);
            string side = slot == EquipSlot.Legs ? (rightFoot ? "_R" : "_L") : string.Empty;
            worn.name = $"{dress.name}_Worn{side}";

            Sanitize(worn, _nightModelRoot.gameObject.layer);
            ApplyAttach(worn.transform, attach, bone);

            // 调参辅助：选中它 → 右键组件 → 复制挂点配置。正式的挂点数值最终写回 DressWornVisual
            DressWornFitting fitting = worn.AddComponent<DressWornFitting>();
            fitting.Initialize(dress.name, attach.bone, rightFoot, attach.compensateBoneScale);

            _spawned.Add(worn);
        }

        /// <summary>把挂点配置写到穿戴物的局部 Transform 上</summary>
        private static void ApplyAttach(Transform target, WornAttach attach, Transform bone)
        {
            target.localPosition = attach.localPosition;
            target.localRotation = Quaternion.Euler(attach.localEuler);

            Vector3 scale = attach.scale;
            if (attach.compensateBoneScale)
            {
                // 蟑螂模型根是 2.3 倍，骨骼也带着这个缩放。
                // 反算成局部缩放后，配置里填的就是"世界尺寸"，换模型 / 改模型缩放都不用重调。
                Vector3 boneScale = bone.lossyScale;
                scale = new Vector3(
                    SafeDivide(scale.x, boneScale.x),
                    SafeDivide(scale.y, boneScale.y),
                    SafeDivide(scale.z, boneScale.z));
            }

            target.localScale = scale;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
        }

        /// <summary>
        /// 把模型弄干净：只留渲染，不要物理 / 触发器 / 动画。
        ///
        /// 1. 碰撞体全部干掉 —— 否则会被 PlayerInteractionManager 当成可交互物，抢焦点；
        /// 2. Rigidbody 干掉 —— tripo 模型偶尔带；
        /// 3. Animator 干掉 —— tripo FBX 是 Generic 导入，自带一个空 Animator，
        ///    留着会在切场景 / 重绑定时写根变换，把挂点摆好的位置顶掉；
        /// 4. 层改成玩家层 —— 和蟑螂本体一致，不进交互射线名单。
        /// </summary>
        private static void Sanitize(GameObject root, int layer)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                Destroy(colliders[i]);
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                Destroy(bodies[i]);
            }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
                Destroy(animators[i]);
            }

            SetLayerRecursive(root.transform, layer);
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursive(root.GetChild(i), layer);
            }
        }

        #endregion

        #region 骨骼

        /// <summary>
        /// 取骨骼。顺序很重要：
        /// 1. **按名字递归找**（<c>mixamorig:Head</c>）—— 白天蟑螂模型是 inactive 的，
        ///    Animator 没启用时 GetBoneTransform 会返回 null，但 Transform 层级一直都在；
        /// 2. 去掉 <c>mixamorig:</c> 前缀再找一次（有的导出会去掉前缀）；
        /// 3. 最后才用 Humanoid 的 <c>GetBoneTransform</c> 兜底（模型已激活时最稳，也不怕改名）。
        /// </summary>
        private Transform ResolveBone(WornBone bone)
        {
            if (_bones.TryGetValue(bone, out Transform cached) && cached != null)
            {
                return cached;
            }

            Transform found = DeepFind(_nightModelRoot, "mixamorig:" + bone);
            if (found == null)
            {
                found = DeepFind(_nightModelRoot, bone.ToString());
            }

            if (found == null)
            {
                Animator animator = _nightModelRoot.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman && animator.avatar != null)
                {
                    found = animator.GetBoneTransform(ToHumanBodyBone(bone));
                }
            }

            if (found != null)
            {
                _bones[bone] = found;
                _reportedMissing.Remove(bone);
            }

            return found;
        }

        private void ReportMissingBone(WornBone bone)
        {
            if (!_reportedMissing.Add(bone))
            {
                return;
            }

            Debug.LogError(
                $"[PlayerDressView]：在 {_nightModelRoot.name} 下找不到骨骼「{bone}」" +
                $"（试过 mixamorig:{bone} / {bone} / HumanBodyBones.{ToHumanBodyBone(bone)}）。" +
                "请确认 _nightModelRoot 指的是蟑螂模型，且模型是 Humanoid 导入、骨骼层级没有被 Optimize 掉。",
                this);
        }

        private static Transform DeepFind(Transform root, string boneName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, boneName, System.StringComparison.Ordinal))
                {
                    return child;
                }

                Transform hit = DeepFind(child, boneName);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        /// <summary>
        /// <see cref="WornBone"/> → Unity 的 <see cref="HumanBodyBones"/>。
        ///
        /// 注意两套命名对不上：Mixamo 的脊椎是 Hips → Spine → Spine1 → Spine2，
        /// 而 Unity humanoid 对应的是 Hips → Spine → **Chest** → **UpperChest**。
        /// 名字查找用 Mixamo 的原名（那条路最准），这个映射只给 GetBoneTransform 兜底用。
        /// </summary>
        private static HumanBodyBones ToHumanBodyBone(WornBone bone)
        {
            switch (bone)
            {
                case WornBone.Hips: return HumanBodyBones.Hips;
                case WornBone.Spine: return HumanBodyBones.Spine;
                case WornBone.Spine1: return HumanBodyBones.Chest;
                case WornBone.Spine2: return HumanBodyBones.UpperChest;
                case WornBone.Neck: return HumanBodyBones.Neck;
                case WornBone.Head: return HumanBodyBones.Head;
                case WornBone.LeftFoot: return HumanBodyBones.LeftFoot;
                case WornBone.RightFoot: return HumanBodyBones.RightFoot;
                default: return HumanBodyBones.Hips;
            }
        }

        #endregion

        #region 引用兜底

        private void ResolveBag()
        {
            if (_bag == null)
            {
                _bag = GetComponent<PlayerBag>();
            }
        }

        /// <summary>
        /// 蟑螂模型根。预制体里连了就直接用；没连就问 PlayerAnimationManager。
        /// 这样以后新做的玩家预制体忘了拖引用也不会静默没外观。
        /// </summary>
        private void ResolveNightModel()
        {
            if (_nightModelRoot != null)
            {
                return;
            }

            PlayerAnimationManager animation = GetComponent<PlayerAnimationManager>();
            if (animation != null)
            {
                _nightModelRoot = animation.NightModelRoot;
            }
        }

        private void Subscribe()
        {
            ResolveBag();
            if (_bag != null)
            {
                _bag.EquippedChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (_bag != null)
            {
                _bag.EquippedChanged -= Refresh;
            }
        }

        #endregion
    }
}
