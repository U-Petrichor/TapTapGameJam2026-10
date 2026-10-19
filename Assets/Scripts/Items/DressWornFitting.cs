using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 调参辅助：运行时生成出来的穿戴物会自动挂上这个组件。
    /// 选中它 → 右键组件标题 → <b>复制挂点配置到剪贴板</b>，
    /// 拿到的文本可以直接替换 <see cref="DressWornVisual"/> 里 <c>_attach</c> /
    /// <c>_rightFootAttach</c> 的那几行，不用手抄数字。
    ///
    /// 【为什么要它】穿戴物的原点是模型作者随便定的，8 件衣服没有一件能直接对齐，
    /// 全靠在 Play 模式下拖位置/转角度/调缩放，再把数值搬回预制体。
    ///
    /// 【只在 Play 模式有意义】这些实例是运行时就地生成的，不会存进场景。
    /// 打印出来的 scale 是 <b>世界尺寸</b>，正好对应 <c>compensateBoneScale = true</c> 时要填的值。
    /// </summary>
    [DisallowMultipleComponent]
    public class DressWornFitting : MonoBehaviour
    {
        [SerializeField] private string _dressName;
        [SerializeField] private WornBone _bone;
        [SerializeField] private bool _rightFoot;
        [SerializeField] private bool _compensateBoneScale = true;

        public void Initialize(string dressName, WornBone bone, bool rightFoot, bool compensateBoneScale)
        {
            _dressName = dressName;
            _bone = bone;
            _rightFoot = rightFoot;
            _compensateBoneScale = compensateBoneScale;
        }

        [ContextMenu("复制挂点配置到剪贴板")]
        public void CopyAttachToClipboard()
        {
            string field = _rightFoot ? "_rightFootAttach" : "_attach";
            Vector3 position = transform.localPosition;
            Vector3 euler = transform.localEulerAngles;
            Vector3 scale = transform.lossyScale;

            string text =
                $"# {_dressName} {( _rightFoot ? "右脚" : "主挂点" )} → DressWornVisual.{field}\n" +
                "  " + field + ":\n" +
                $"    bone: {(int)_bone}\n" +
                $"    localPosition: {{x: {position.x:0.####}, y: {position.y:0.####}, z: {position.z:0.####}}}\n" +
                $"    localEuler: {{x: {euler.x:0.####}, y: {euler.y:0.####}, z: {euler.z:0.####}}}\n" +
                $"    scale: {{x: {scale.x:0.####}, y: {scale.y:0.####}, z: {scale.z:0.####}}}\n" +
                $"    compensateBoneScale: {(_compensateBoneScale ? 1 : 0)}\n";

            GUIUtility.systemCopyBuffer = text;
            Debug.Log(text, this);
        }
    }
}
