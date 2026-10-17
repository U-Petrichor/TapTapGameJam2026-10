using System;
using System.Collections;
using NUnit.Framework;
using TapTapGameJam.Items;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// "待拾取物"位：一个只装一件东西的小容器 + 槽位。
    ///
    /// - 作为容器（<see cref="ItemContainer"/>）：持有那一件，打开时给它建块、关掉时销毁它的块；
    /// - 作为槽位（<see cref="IItemSlot"/>）：可以往上丢（标记点空着就行），也可以把这件再拖走。
    ///
    /// 它和背包是独立容器：背包关掉时把它一起关掉即可，不需要谁去记录它的块。
    /// </summary>
    public class UnpickItemSlot : ItemContainer, IItemSlot
    {
        [Tooltip("物品标记点：待拾取物停在这里。放在提示框中心")]
        [SerializeField] private RectTransform _itemSpritePivot;

        [Tooltip("物品提示 - 标题")]
        [SerializeField] private TextMeshProUGUI _infoTextTitle;

        [Tooltip("物品提示 - 内容")]
        [SerializeField] private TextMeshProUGUI _infoTextContent;

        [Header("出场 / 离场动画")]
        [Tooltip("动画时长（秒，真实时间，不受暂停影响）。填 0 = 不播动画、直接到位")]
        [SerializeField] private float _animDuration = 0.25f;

        [Tooltip("停留位置：相对场景里摆好的位置偏移，一般留 (0,0)")]
        [SerializeField] private Vector2 _shownOffset = Vector2.zero;

        [Tooltip("藏起来时的位置：相对停留位置的偏移。从屏幕底部升上来 → 填负 y（≈0,-650）")]
        [SerializeField] private Vector2 _hiddenOffset = new Vector2(0f, -650f);

        /// <summary>当前停在这里的那件（容器持有的唯一物品）</summary>
        private PickableItem _item;

        /// <summary>离场动画要销毁的那件。开始关的时候定下来 —— OpenWithItem 是"先关再开"，_item 会被换掉</summary>
        private PickableItem _closingItem;

        private CanvasGroup _canvasGroup;

        /// <summary>停留位置 = 场景里摆好的位置 + _shownOffset</summary>
        private Vector2 _shownPosition;

        private Coroutine _anim;

        /// <summary>离场动画还没播完（收尾还没跑）</summary>
        private bool _closePending;

        public PickableItem Item => _item;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _shownPosition = ((RectTransform)transform).anchoredPosition + _shownOffset;

            // 一开始就停在隐藏位（屏幕下方）、全透明、不吃射线
            ((RectTransform)transform).anchoredPosition = _shownPosition + _hiddenOffset;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        #region IItemSlot

        public bool TryDropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "UnpickItemSlot：block 是 null");
            return _item == null;
        }

        public bool DropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "UnpickItemSlot：block 是 null");

            if (_item != null)
            {
                return false;
            }

            SetItemBlock(block);
            return true;
        }

        /// <summary>块被拿走了：标记点空出来（块本身和 CurrentSlot 都不归这里管）</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            Assert.IsTrue(_item == block.boundedItem,
                $"UnpickItemSlot：拿走的不是我这里的那件（我是 {_item}，来的是 {block.boundedItem}）");

            _item = null;
            _infoTextTitle.text = "";
            _infoTextContent.text = "";
            CloseContainer();
            return true;
        }

        /// <summary>TODO: 待拾取位还没有试放预览的表现</summary>
        public void ClearDropPreview()
        {
        }

        #endregion

        #region ItemContainer

        /// <summary>
        /// 换一件停在标记点上（比如装备槽换下来的那件）。
        /// 换件 = 先关掉再开，所以重复调用、连着换都是安全的。
        /// </summary>
        public void OpenWithItem(PickableItem item)
        {
            Assert.IsTrue(item != null, "UnpickItemSlot.OpenWithItem：item 是 null");

            if (_item == item)
            {
                OpenContainer(); // 就是当前这件：确保它开着
                return;
            }

            // 把那件（它的块 + 提示）收掉；没开着也安全
            CloseContainer();

            _item = item;
            OpenContainer();
        }

        /// <summary>
        /// 打开：可安全重复调用。
        /// 手里没东西就没什么可开的（这时不翻状态，免得"开着但没有物品"）。
        /// 出场 = 从屏幕底部升上来 + 虚化减弱（alpha 0 → 1）。
        /// </summary>
        public override void OpenContainer()
        {
            if (IsOpen || _item == null)
            {
                return;
            }

            // 上一次离场动画还没播完：先把它的收尾（销毁块）补掉，否则下面 CreateBlock 会撞"已经有块了"
            FinishPendingClose();

            base.OpenContainer();

            BagItemBlock block = UIManager.Instance.CreateBlock(_item);
            SetItemBlock(block);

            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            PlayEnterAnimation();
        }

        /// <summary>
        /// 关闭：可安全重复调用。立刻断输入，然后播离场（滑回屏幕下方 + 淡出）。
        ///
        /// 销毁块 / 清提示通过回调传进动画协程（滑完了才跑）。
        /// 但**要销毁哪一件必须在这里记下来**：<see cref="OpenWithItem"/> 是"先关再开"，
        /// 离场动画还没播完 _item 就已经被换成新的了。
        /// 另外 _item 可能已经是 null —— 东西被拖走了、还没放回来就关容器，这时它的块归别人管。
        /// </summary>
        public override void CloseContainer()
        {
            if (!IsOpen)
            {
                return;
            }

            _closingItem = _item;

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            base.CloseContainer();

            PlayExitAnimation(FinishClose);
        }

        /// <summary>离场动画播完之后的收尾：销毁那时持有的那件的块，并清掉提示文字</summary>
        private void FinishClose()
        {
            if (_closingItem != null)
            {
                UIManager.Instance.DestroyBlock(_closingItem);
                _closingItem = null;
            }

            _infoTextTitle.text = "";
            _infoTextContent.text = "";

            _canvasGroup.alpha = 0f;

            // TODO: 把提示框整个收起来（现在是靠 alpha = 0 藏住的）
        }

        #region 出场 / 离场动画（实现）

        /// <summary>从隐藏位升到停留位，同时淡入</summary>
        private void PlayEnterAnimation()
        {
            StopAnimation();

            ((RectTransform)transform).anchoredPosition = _shownPosition + _hiddenOffset;
            _canvasGroup.alpha = 0f;

            _anim = StartCoroutine(Animate(_shownPosition, 1f, null));
        }

        /// <summary>从当前位置滑回隐藏位，同时淡出；滑完调 onDone（收尾从自己的回调里进去）</summary>
        private void PlayExitAnimation(Action onDone)
        {
            StopAnimation();

            _closePending = true;
            _anim = StartCoroutine(Animate(_shownPosition + _hiddenOffset, 0f, () =>
            {
                _closePending = false;
                onDone?.Invoke();
            }));
        }

        /// <summary>先快后慢（三次缓出）地滑到 to 并淡到 toAlpha。用真实时间，游戏暂停时也能播完</summary>
        private IEnumerator Animate(Vector2 to, float toAlpha, Action onDone)
        {
            RectTransform rect = (RectTransform)transform;
            Vector2 from = rect.anchoredPosition;
            float fromAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < _animDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float k = Mathf.Clamp01(elapsed / _animDuration);
                float ease = 1f - Mathf.Pow(1f - k, 3f); // 先快后慢

                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, ease);
                _canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, ease);

                yield return null;
            }

            rect.anchoredPosition = to;
            _canvasGroup.alpha = toAlpha;
            _anim = null;
            onDone?.Invoke();
        }

        /// <summary>上一次离场还没播完就把收尾补上（重开时用），免得块的销毁被跳过</summary>
        private void FinishPendingClose()
        {
            if (!_closePending)
            {
                return;
            }

            StopAnimation();
            _closePending = false;
            FinishClose();
        }

        private void StopAnimation()
        {
            if (_anim == null)
            {
                return;
            }

            StopCoroutine(_anim);
            _anim = null;
        }

        #endregion

        #endregion

        /// <summary>把块停到标记点上，并显示提示</summary>
        private void SetItemBlock(BagItemBlock block)
        {
            _item = block.boundedItem;
            _infoTextContent.text = _item.ItemDesc;
            _infoTextTitle.text = _item.ItemTitle;

            // 从仓库过来的块要先恢复成背包形态（显示格子框、缩放归 1）
            block.ApplyBagPresentation();
            block.CurrentSlot = this;
            // 挂在容器自己下面：待拾取位滑上来 / 淡入时物品跟着一起走
            block.transform.SetParent(transform, false);
            block.transform.position = _itemSpritePivot.position;
        }
    }
}
