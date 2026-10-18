using System;
using TapTapGameJam.Core;
using UnityEngine.Assertions;
using TapTapGameJam.Interactables;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class BubbleHint: MonoBehaviour
    {
        
        [Tooltip("Bubble气泡图，被玩家选中时高亮为蓝色")]
        [SerializeField] private Image _bubbleImage;

        [Tooltip("Bubble提示按钮，被玩家选中时 scale.x 弹出到 1，平时缩回 0")]
        [SerializeField] private Transform _bubbleHintButton;
        
        [Tooltip("就绪时是否隐藏气泡")]
        [SerializeField] private bool _hideBubbleWhenPending = true;
        
        [Tooltip("就绪时是否隐藏弹框")]
        [SerializeField] private bool _hideHintWhenPending = true;
        
        [Tooltip("关联的Interactable，不挂自动向上寻找")]
        [SerializeField] private Interactable _interactable;

        [Tooltip("bubble所处的canvas，自动向上寻找")]
        [SerializeField] private Canvas _canvas;
        
        private bool _isHided = false;
        private Vector3 _orginScale;
        
        private void Awake()
        {
            if (!_interactable)  _interactable = GetComponentInParent<Interactable>();
            _canvas = GetComponentInParent<Canvas>();
            
            Assert.IsTrue(_bubbleImage);
            Assert.IsTrue(_bubbleHintButton || _hideHintWhenPending);

            if (!_hideHintWhenPending)
            {
                Vector3 scale = _bubbleHintButton.localScale;
                scale.x = 0f;
                _bubbleHintButton.localScale = scale;
            }
            _orginScale = _bubbleImage.transform.localScale;
            _isHided = false;
        }

        private void Update()
        {
            RefreshBubble();
        }

        /// <summary>
        /// 被玩家选中时气泡高亮、提示按钮弹出；取消选中时颜色复原、按钮快速缩回；交互期间不显示
        /// </summary>
        private void RefreshBubble()
        {
            InteractState state = _interactable.InteractState;
            
            if (state == InteractState.Interacting || !_interactable.CanInteract(GameManager.Player))
            {
                if (!_isHided)
                {
                    _isHided = true;
                    _bubbleImage.transform.localScale = Vector3.zero;
                }

                return;
            }
            
            if (_isHided)
            {
                _isHided = false;
                _bubbleImage.transform.localScale = _orginScale;
            }

            if (state == InteractState.InteractPending)
            {
                if (_hideBubbleWhenPending)
                {
                    // 隐藏
                    _bubbleImage.color = Color.clear;
                }
                else
                {
                    // or 高亮
                    _bubbleImage.color = new Color(0.0f, 1.0f, 1.0f);
                }

                if (!_hideHintWhenPending)
                {
                    Vector3 scale = _bubbleHintButton.localScale;
                    _canvas.sortingOrder = 200;
                    // 弹框出现时强制到最上层
                    scale.x = Mathf.MoveTowards(scale.x, 1f, 10 * Time.deltaTime);
                    _bubbleHintButton.localScale = scale;
                }
                
            } else
            {
                _bubbleImage.color = new Color(1.0f, 1.0f, 1.0f);


                if (!_hideHintWhenPending)
                {
                    Vector3 scale = _bubbleHintButton.localScale;
                    _canvas.sortingOrder = 0;
                    scale.x = Mathf.MoveTowards(scale.x, 0f, 10 * Time.deltaTime);
                    _bubbleHintButton.localScale = scale;
                }
            }
        }

        
    }
}