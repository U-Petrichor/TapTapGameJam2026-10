using System;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 穿戴时挂到蟑螂人形骨架的哪根骨骼上。
    /// 数值和 Mixamo 骨架（<c>mixamorig:*</c>）一一对应，取值顺序刻意对齐
    /// <see cref="HumanBodyBones"/> 的常用项，方便运行时用 <c>Animator.GetBoneTransform</c> 兜底。
    ///
    /// <see cref="Auto"/> 表示"按装备槽取默认"：头 → Head，身 → Spine2，足 → LeftFoot。
    /// </summary>
    public enum WornBone
    {
        Auto = 0,
        Hips = 1,
        Spine = 2,
        Spine1 = 3,
        Spine2 = 4,
        Neck = 5,
        Head = 6,
        LeftFoot = 7,
        RightFoot = 8
    }

    /// <summary>
    /// 一份挂点配置：挂哪根骨骼、相对骨骼摆在哪、多大。
    ///
    /// 【坐标系】<c>localPosition</c> / <c>localEuler</c> 都是**骨骼的局部坐标系**，不是世界坐标。
    /// Mixamo 骨骼的局部朝向各自不同（尤其脚），所以左右脚不能共用一套数值
    /// —— 见 <see cref="DressWornVisual"/> 的镜像/单独配置选项。
    /// </summary>
    [Serializable]
    public sealed class WornAttach
    {
        [Tooltip("目标骨骼。Auto = 按槽位默认（头→Head / 身→Spine2 / 足→LeftFoot）")]
        public WornBone bone = WornBone.Auto;

        [Tooltip("相对骨骼的局部位置（骨骼坐标系，不是世界坐标）")]
        public Vector3 localPosition = Vector3.zero;

        [Tooltip("相对骨骼的局部欧拉角")]
        public Vector3 localEuler = Vector3.zero;

        [Tooltip("缩放。勾了 Compensate Bone Scale 时这里填的是世界尺寸；否则是骨骼下的局部缩放")]
        public Vector3 scale = Vector3.one;

        [Tooltip("勾上：用骨骼的世界缩放反算局部缩放。蟑螂模型根是 2.3 倍，不勾就得自己乘 1/2.3")]
        public bool compensateBoneScale = true;

        public WornAttach Clone()
        {
            return new WornAttach
            {
                bone = bone,
                localPosition = localPosition,
                localEuler = localEuler,
                scale = scale,
                compensateBoneScale = compensateBoneScale
            };
        }
    }

    /// <summary>
    /// 一件装扮的**穿戴外观**配置。挂在 Dress 预制体根上，和 <see cref="DressItem"/> 并列。
    ///
    /// 【为什么单独一个组件，而不是塞进 DressItem】
    /// - <see cref="DressItem"/> 是玩法数据（落哪个槽、给什么 Buff 见 <see cref="DressData"/>），
    ///   外观是美术数据，两者改动的时机和人完全不同；
    /// - 复制一件换皮装扮时，外观要重配、Buff 可以直接复用同一个 <see cref="DressData"/>。
    ///
    /// 【哪些衣服挂了它】目前只有头部的两件（<c>Head_HanBao</c> / <c>Head_ZhiDai</c>）。
    /// 身体（背部）和脚部只保留玩法数据，不做 3D 外观，所以那 6 件预制体上没有这个组件。
    /// 要恢复时把组件挂回对应预制体就行 —— 骨骼映射、左右脚镜像这些逻辑都还在。
    ///
    /// 【谁会读】<c>PlayerDressView</c>（挂在 Player 根上）在 PlayerBag.EquippedChanged 时
    /// 拿这里的模型 + 挂点，把模型实例化到蟑螂的骨骼下面。
    ///
    /// 【注意】这里的模型是**给穿戴用的**，不是地图上那个展示用的子物体：
    /// 装备后物品本体被 PlayerBag 藏到暂存区，穿戴件必须是独立的一份拷贝，
    /// 否则丢弃时"身上这份"和"地上那份"会打架。
    /// </summary>
    [DisallowMultipleComponent]
    public class DressWornVisual : MonoBehaviour
    {
        #region 配置

        [Header("穿戴模型")]
        [Tooltip("穿戴时挂到骨骼上的模型。留空 = 自动取预制体里第一个带 Renderer 的子物体（地图展示用的那件）")]
        [SerializeField] private GameObject _wornModelPrefab;

        [Header("挂点（头 / 身 / 左脚共用这一份）")]
        [SerializeField] private WornAttach _attach = new WornAttach();

        [Header("足部：右脚")]
        [Tooltip("勾上：右脚直接用左脚的配置镜像（位置 x 取反、欧拉 y/z 取反），不用单独调")]
        [SerializeField] private bool _mirrorToRightFoot = true;

        [Tooltip("取消镜像后用它单独调右脚。骨骼的局部坐标系左右不一定严格镜像，发现歪了就关掉镜像改这里")]
        [SerializeField] private WornAttach _rightFootAttach = new WornAttach { bone = WornBone.RightFoot };

        #endregion

        #region 只读查询

        /// <summary>
        /// 穿戴用的模型。没在 Inspector 里指定时，退化成"预制体里的展示模型"。
        /// 退化路径只是兜底（懒加载）：两条路都要求模型能被 <c>Instantiate</c>。
        /// </summary>
        public GameObject ModelPrefab
        {
            get
            {
                if (_wornModelPrefab != null)
                {
                    return _wornModelPrefab;
                }

                return ResolveModelFromChildren();
            }
        }

        /// <summary>
        /// 取某个槽 / 左脚的挂点。<paramref name="bone"/> 一定是解析后的具体骨骼，不会是 Auto。
        /// 返回的是副本，调用方随便改，不会写脏预制体上的配置。
        /// </summary>
        public WornAttach GetAttach(EquipSlot slot, bool rightFoot)
        {
            if (!rightFoot)
            {
                WornAttach attach = _attach.Clone();
                if (attach.bone == WornBone.Auto)
                {
                    attach.bone = DefaultBone(slot);
                }

                return attach;
            }

            if (!_mirrorToRightFoot)
            {
                WornAttach custom = _rightFootAttach.Clone();
                if (custom.bone == WornBone.Auto)
                {
                    custom.bone = WornBone.RightFoot;
                }

                return custom;
            }

            // 镜像：位置 x 取反、欧拉 y/z 取反。
            // 刻意**不做** scale.x = -1 —— URP 下负缩放会把面翻过来（法线反向 + 背面剔除），
            // 描边/双面材质的衣服会直接穿帮。
            WornAttach mirrored = _attach.Clone();
            mirrored.bone = WornBone.RightFoot;
            mirrored.localPosition = new Vector3(
                -mirrored.localPosition.x,
                mirrored.localPosition.y,
                mirrored.localPosition.z);
            mirrored.localEuler = new Vector3(
                mirrored.localEuler.x,
                -mirrored.localEuler.y,
                -mirrored.localEuler.z);
            return mirrored;
        }

        /// <summary>槽位的默认骨骼：头 → Head，身 → Spine2（胸/背），足 → LeftFoot</summary>
        public static WornBone DefaultBone(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Body:
                    return WornBone.Spine2;
                case EquipSlot.Legs:
                    return WornBone.LeftFoot;
                case EquipSlot.Head:
                default:
                    return WornBone.Head;
            }
        }

        #endregion

        #region 内部

        /// <summary>
        /// 兜底：找预制体里第一个带 Renderer 的子物体。
        /// 气泡 Canvas 只有 CanvasRenderer（不是 Renderer），所以不会被误选。
        /// </summary>
        private GameObject ResolveModelFromChildren()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            // 右脚配置的骨骼留 Auto 时补成 RightFoot，避免美术忘了选导致挂到左脚上
            if (_rightFootAttach != null && _rightFootAttach.bone == WornBone.Auto)
            {
                _rightFootAttach.bone = WornBone.RightFoot;
            }
        }

        #endregion
    }
}
