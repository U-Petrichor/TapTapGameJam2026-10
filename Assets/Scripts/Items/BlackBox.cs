using System;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.Items
{
    public class BlackBox: Interactable
    {
        // 初始隐藏内部道具，扫描完成后黑盒删除，生成内部道具
        [Tooltip("扫描所需时间")]
        public float scanReqTime = 0.5f;

        [Tooltip("黑盒内部道具")]
        public PickableItem hidedItem;

        [Tooltip("气泡UI")]
        public Image bubbleImage;
        
        [Tooltip("扫描进度UI（整体）")]
        public Image scanUI;
        
        [Tooltip("扫描进度UI（process bar）")]
        public Image scanUIProcess;
        
        [Tooltip("已经扫描了多少")]
        public float scannedTime;
        public float ScannedRate => scannedTime / scanReqTime;

        private bool _isScanning = false;
        
        public override string InteractionPrompt => "按E扫描";

        public override void Interact(PlayerBasic player, InteractKey key)
        {
            ;
        }

        protected void OnEnable()
        {
            _isScanning = false;
            scanUI.gameObject.SetActive(false);
            bubbleImage.enabled = true;
            scannedTime = 0;
        }

        private void Update()
        {
            if (InteractState == InteractState.Interacting)
            {
                if (!_isScanning)
                {
                    scanUI.gameObject.SetActive(true);
                    bubbleImage.enabled = false;
                    _isScanning = true;
                }
                scanUIProcess.transform.localScale = new Vector3(ScannedRate, 1, 1);
                
                scannedTime += Time.deltaTime;
                if (scannedTime >= scanReqTime)
                {
                    Instantiate(hidedItem, transform.position, Quaternion.identity);
                    Destroy(gameObject);
                }
            }
            else
            {
                if (_isScanning)
                {
                    scanUI.gameObject.SetActive(false);
                    bubbleImage.enabled = true;
                    scannedTime = 0;
                    _isScanning = false;
                }

                bool pending = (InteractState == InteractState.InteractPending); 
                bubbleImage.color = pending ? new Color(0.0f, 1.0f, 1.0f) : Color.white;
            }
        }
    }
}