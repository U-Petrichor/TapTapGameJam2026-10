using System.Collections.Generic;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class PlayerRepo
    {
        public List<PickableItem> StoreItems;

        public int StoreWidth = 5;
        public int StoreHeight = 6;

        public void MoveItemToRepo(PickableItem item)
        {
            StoreItems.Add(item);
            item.SetLocation(PickableItem.ItemLocation.InRepo);
        }

        public void MoveItemToBag(PickableItem item, PlayerBag bag, Vector2Int origin)
        {
            if (bag.TryPlace(item, origin))
            {
                StoreItems.Remove(item);
                item.SetLocation(PickableItem.ItemLocation.InBag);    
            }
        }
        
    }
}