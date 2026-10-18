using TapTapGameJam.Audio;
using System;
using TapTapGameJam.Core;
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

        [Tooltip("黑盒内部道具，不配置即随机（受幸运值影响）")]
        public PickableItem hidedItem;

        [Tooltip("商店（随机源）")]
        public MouseStore mouseStore;
        
        [Tooltip("扫描进度UI（整体）")]
        public Image scanUI;
        
        [Tooltip("扫描进度UI（process bar）")]
        public Image scanUIProcess;
        
        [Tooltip("已经扫描了多少")]
        public float scannedTime;
        public float ScannedRate => scannedTime / scanReqTime;

        public override string InteractionPrompt => "按E扫描";

        public override bool CanInteract(PlayerBasic player)
        {
            return GameManager.IsNight && base.CanInteract(player);
        }

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
            Sfx.Emitter(this).SetPlaying(SoundId.Search, InteractState == InteractState.Interacting, SoundBus.World, true);
            if (InteractState == InteractState.Interacting)
            {
                if (!scanUI.IsActive()) scanUI.gameObject.SetActive(true);
                
                scanUIProcess.transform.localScale = new Vector3(ScannedRate, 1, 1);
                
                scannedTime += Time.deltaTime;
                if (scannedTime >= scanReqTime)
                {
                    Sfx.Emitter(this).StopAll();
                    Sfx.Play(SoundId.Achievement, this);
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