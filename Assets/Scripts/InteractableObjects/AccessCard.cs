using TapTapGameJam.Audio;
using TapTapGameJam.Core;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEngine;

/// <summary>夜晚的一次性交互物：立即授权并消失，不进入物品和背包系统。</summary>
public sealed class AccessCard : Interactable
{
    private AccessCardSpawner _spawner;
    private bool _collected;
    public override string InteractionPrompt => "获得门禁卡";
    public void Initialize(AccessCardSpawner spawner) => _spawner = spawner;
    public override bool CanInteract(PlayerBasic player) =>
        !_collected && GameManager.IsNight && _spawner != null && _spawner.CanCollect(this);
    public override void OnTriggerEnter(Collider other)
    {
        if (CanInteract(null)) base.OnTriggerEnter(other);
    }
    public override void Interact(PlayerBasic player, InteractKey key)
    {
        if (key != InteractKey.E)
        {
            InteractState = InteractState.InteractPending;
            return;
        }
        if (!CanInteract(player)) return;
        if (!_spawner.TryCollect(this)) return;
        _collected = true;
        Sfx.At(SoundId.Pickup, transform.position, player, SoundBus.Interface);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    private void OnDisable() => ClearAllRegister();
}
