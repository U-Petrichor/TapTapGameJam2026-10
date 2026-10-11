using System;
using NUnit.Framework;
using TapTapGameJam.Interactables;
using UnityEditor.VersionControl;
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
        
        [Tooltip("关联的Interactable，不挂自动向上寻找")]
        [SerializeField] private Interactable _interactable;

        private bool _isHided = false;
        private Vector3 _orginScale;
        
        private void Awake()
        {
            if (!_interactable)  _interactable = GetComponentInParent<Interactable>();
            
            Assert.IsTrue(_bubbleImage);
            Assert.IsNotNull(_bubbleHintButton);

            Vector3 scale = _bubbleHintButton.localScale;
            scale.x = 0f;
            _bubbleHintButton.localScale = scale;
            _isHided = false;
            _orginScale = _bubbleImage.transform.localScale;
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

            if (state == InteractState.InteractPending)
            {
                _bubbleImage.color = new Color(0.0f, 1.0f, 1.0f);
                Vector3 scale = _bubbleHintButton.localScale;
                scale.x = Mathf.MoveTowards(scale.x, 1f, 10 * Time.deltaTime);
                _bubbleHintButton.localScale = scale;
                
            } else
            {
                _bubbleImage.color = new Color(1.0f, 1.0f, 1.0f);
                Vector3 scale = _bubbleHintButton.localScale;
                scale.x = Mathf.MoveTowards(scale.x, 0f, 10 * Time.deltaTime);
                _bubbleHintButton.localScale = scale;
            }

            if (state == InteractState.Interacting)
            {
                if (!_isHided)
                {
                    _isHided = true;
                    _bubbleImage.transform.localScale = Vector3.zero;
                }
            }
            else
            {
                if (_isHided)
                {
                    _isHided = false;
                    _bubbleImage.transform.localScale = _orginScale;
                }
            }
        }

        
    }
}