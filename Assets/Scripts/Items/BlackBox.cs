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

        [Tooltip("扫描进度UI（整体）")]
        public Image scanUI;
        
        [Tooltip("扫描进度UI（process bar）")]
        public Image scanUIProcess;
        
        [Tooltip("已经扫描了多少")]
        public float scannedTime;
        public float ScannedRate => scannedTime / scanReqTime;

        public override string InteractionPrompt => "按E扫描";

        public override void Interact(PlayerBasic player, InteractKey key)
        {
            ;
        }

        protected void OnEnable()
        {
            scanUI.gameObject.SetActive(false);
            scannedTime = 0;
        }

        private void Update()
        {
            if (InteractState == InteractState.Interacting)
            {
                if (!scanUI.IsActive()) scanUI.gameObject.SetActive(true);
                
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
                if (scanUI.IsActive())
                {
                    scanUI.gameObject.SetActive(false);
                    scannedTime = 0;
                }
            }
        }
    }
}