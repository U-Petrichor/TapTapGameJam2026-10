using System;
using System.Collections;
using NUnit.Framework;
using TapTapGameJam.Items;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// "待拾取物"位：一个只装一件东西的小容器。该物品应该在世界上
    ///
    /// - 作为容器（<see cref="ItemContainer"/>）：持有那一件，打开时给它建块、关掉时销毁它的块；
    /// - 作为槽位（<see cref="IItemSlot"/>）：可以往上丢（标记点空着就行），也可以把这件再拖走。
    ///
    /// 它和背包是独立容器：背包关掉时把它一起关掉即可，不需要谁去记录它的块。
    /// </summary>
    public class UnpickItemContainer : ItemContainer
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

        [Tooltip("对应的底盘空槽位")]
        [SerializeField] private EmptySlot _emptySlot;
        
        /// <summary>当前停在这里的那件（容器持有的唯一物品）</summary>
        private PickableItem _item;

        /// <summary>离场动画要销毁的那件。开始关的时候就定下来：动画还没播完时 _item 可能已经被换成新的了</summary>
        private PickableItem _closingItem;

        private CanvasGroup _canvasGroup;

        /// <summary>停留位置 = 场景里摆好的位置 + _shownOffset</summary>
        private Vector2 _shownPosition;

        private Coroutine _anim;

        /// <summary>离场动画还没播完（收尾还没跑）</summary>
        private bool _closePending;

        public PickableItem Item => _item;

        /// <summary>标题本来配的颜色。清空时还原成它（标题颜色跟着物品分类走）</summary>
        private Color _titleBaseColor = Color.white;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_infoTextTitle != null)
            {
                _titleBaseColor = _infoTextTitle.color;
            }

            _shownPosition = ((RectTransform)transform).anchoredPosition + _shownOffset;

            // 一开始就停在隐藏位（屏幕下方）、全透明、不吃射线
            ((RectTransform)transform).anchoredPosition = _shownPosition + _hiddenOffset;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }


        #region ItemContainer

        /// <summary>
        /// 将内容换成某Item并打开；已经打开时调用无效
        /// </summary>
        public void OpenWithItem(PickableItem item)
        {
            Assert.IsTrue(item != null, "UnpickItemSlot.OpenWithItem：item 是 null");
            if (IsOpen) return;
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

            // 要销毁哪一件在这里定下来（下面 FinishClose 是 0.25s 之后才跑的，
            // 中间 _item 可能已经被 OpenWithItem 换成新的那件了）
            _closingItem = _item;

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            base.CloseContainer();

            PlayExitAnimation(FinishClose);
        }

        /// <summary>离场动画播完之后的收尾：销毁关店时那件的块，并清掉提示文字</summary>
        private void FinishClose()
        {
            if (_closingItem != null)
            {
                // 块可能已经不在了（被拖出去、被别的容器接手），所以先问一句再销毁
                if (UIManager.Instance.TryGetBlock(_closingItem, out BagItemBlock block))
                {
                    UIManager.Instance.DestroyBlock(block);
                }

                // 关的就是当前这件才清空（已经被换成新的那件就别动它）
                if (_item == _closingItem)
                {
                    _item = null;
                }

                _closingItem = null;
            }

            _infoTextTitle.text = "";
            _infoTextContent.text = "";

            _canvasGroup.alpha = 0f;
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

        /// <summary>把块停到标记点上，并显示提示（标题颜色 = 物品分类的代表色）</summary>
        public void SetItemBlock(BagItemBlock block)
        {
            if (block == null)
            {
                _item = null;
                _infoTextContent.text = "";
                _infoTextTitle.text = "";
                _infoTextTitle.color = _titleBaseColor;
                return;
            }
            _item = block.boundedItem;
            _infoTextContent.text = _item.ItemDesc;
            _infoTextTitle.text = _item.ItemTitle;
            _infoTextTitle.color = UIManager.Instance.CategoryColor(_item.Category);

            // 从仓库过来的块要先恢复成背包形态（显示格子框、缩放归 1）
            block.CurrentSlot = _emptySlot;
            block.ApplyBagPresentation();
            // 挂在容器自己下面：待拾取位滑上来 / 淡入时物品跟着一起走
            block.transform.SetParent(transform, false);
            block.transform.position = _itemSpritePivot.position;
        }
    }
}
