using UnityEngine;

namespace TapTapGameJam.UI
{
    public sealed class ShoppingRoomView : MonoBehaviour
    {
        public void SetInteractionEnabled(bool enabled)
        {
            foreach (var item in GetComponentsInChildren<ShoppingRoomHotspot>(true))
                item.SetInteractionEnabled(enabled);
        }
    }
}
