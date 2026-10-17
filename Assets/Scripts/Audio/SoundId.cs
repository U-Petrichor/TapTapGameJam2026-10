namespace TapTapGameJam.Audio
{
    // Values intentionally match the sound sheet. Disabled entries keep their numbers.
    public enum SoundId
    {
        None = 0, Switch = 1, Click = 2, Panel = 3, Error = 4, Coin = 5,
        HumanFootstep = 6, Delivery = 7, Grinding = 8, PaperBag = 9, PourWater = 10,
        CoffeeMachine = 11, Printer = 12, PrinterHit = 13, Wipe = 14, PrinterQte = 15,
        CutTape = 16, DogFoodBag = 17, PourDogFood = 18, Warning = 19, Equip = 20,
        MoveItem = 21, RotateItem = 22, TalentUpgrade = 23, TalentRefund = 24,
        MerchantBubble = 25, Crawl = 26, Sprint = 27, Pickup = 28, Drop = 29,
        Gnaw = 30, Recovery = 31, Search = 32, Achievement = 33, GuardFootstep = 34,
        Spray = 35, Stomp = 36, Alert = 37, Hit = 38, Defeat = 39, Unlock = 40,
        Door = 41, DayEnd = 42, NightEnd = 43, DoorClose = 44
    }
    public enum SoundBus { World, Interface, MiniGame }
    public enum SoundStopReason { Completed, Cancelled, OwnerRemoved }
}
