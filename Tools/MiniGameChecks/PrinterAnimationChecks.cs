// Run in Unity: Invoke-UnityCommand.ps1 -CodePath Tools/MiniGameChecks/PrinterAnimationChecks.cs
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var type = typeof(TapTapGameJam.UI.PrinterRepairGame);
var root = new UnityEngine.GameObject("PrinterAnimationCheck", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasGroup));
root.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
void Check(bool result, string message) { if (!result) throw new System.Exception(message); UnityEngine.Debug.Log("PASS: " + message); }
UnityEngine.RectTransform Rect(string name, float x, float y)
{
    var item = new UnityEngine.GameObject(name, typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
    item.transform.SetParent(root.transform, false);
    var rect = (UnityEngine.RectTransform)item.transform;
    rect.localPosition = new UnityEngine.Vector3(x, y, 0);
    return rect;
}
try
{
    var game = root.AddComponent<TapTapGameJam.UI.PrinterRepairGame>();
    var hand = Rect("Hand", -230, 115);
    var target = Rect("HandFall", -203, 59);
    hand.localRotation = UnityEngine.Quaternion.Euler(0, 0, 29);
    target.localRotation = UnityEngine.Quaternion.Euler(0, 0, -9);
    var flash = Rect("Flash", -122, 27).GetComponent<UnityEngine.UI.Image>();
    var wrong = Rect("FlashWrongTime", -122, 27).GetComponent<UnityEngine.UI.Image>();
    var pointer = Rect("Pointer", 0, -76);
    var original = hand.localPosition;
    var rotation = hand.localRotation;
    void Set(string name, object value) { type.GetField(name, flags).SetValue(game, value); }
    void Call(string name, params object[] args) { type.GetMethod(name, flags).Invoke(game, args); }
    void Advance(float seconds) { Call("AdvanceStrike", seconds); }
    game.Configure(Rect("Bar", 0, 0), Rect("Zone", 0, 0), pointer, Rect("Printer", 0, 27), null);
    Set("_hand", hand); Set("_handFall", target); Set("_flash", flash); Set("_flashWrongTime", wrong);
    Set("_panelGroup", root.GetComponent<UnityEngine.CanvasGroup>());
    Call("CaptureHandPose"); Call("ResetStrike");
    int completed = 0;
    var missRound = new TapTapGameJam.UI.PrinterRepairRound(2, 1, 0.25, () => 1);
    Set("_round", missRound); Set("_running", true);
    game.Hit(); Advance(0.05f);
    Check(hand.localPosition.y < original.y && hand.localPosition.y > target.localPosition.y
        && !flash.gameObject.activeSelf && !wrong.gameObject.activeSelf, "wrong timing still lowers the hand without early flash");
    Advance(0.05f);
    Check(wrong.gameObject.activeSelf && !flash.gameObject.activeSelf && missRound.Successes == 0,
        "miss shows only FlashWrongTime and does not count");
    Advance(0.04f);
    Check(wrong.color.a > 0 && wrong.color.a < 1, "wrong flash fades rapidly");
    Advance(0.2f);
    Check(!flash.gameObject.activeSelf && !wrong.gameObject.activeSelf && UnityEngine.Vector3.Distance(hand.localPosition, original) < 0.001f,
        "hand returns and both flashes hide after miss");

    var round = new TapTapGameJam.UI.PrinterRepairRound(2, 1, 0.25, () => 0);
    Set("_round", round); Set("_onCompleted", (System.Action)(() => completed++));
    game.Hit(); Advance(0.1f);
    Check(UnityEngine.Vector3.Distance(hand.localPosition, target.localPosition) < 0.001f
        && UnityEngine.Quaternion.Angle(hand.localRotation, target.localRotation) < 0.01f
        && flash.gameObject.activeSelf && !wrong.gameObject.activeSelf, "success reaches HandFall and shows only Flash");
    round.Tick(0.5); game.Hit(); // enqueue miss
    round.Tick(1.5); game.Hit(); // enqueue final success
    Check(round.Successes == 2 && completed == 0, "mixed queued inputs count only successes and defer completion");
    Advance(0.2f); Advance(0.1f);
    Check(wrong.gameObject.activeSelf && !flash.gameObject.activeSelf,
        "queued miss keeps its original result even after pointer returns to success zone");
    Advance(0.2f); Advance(0.1f);
    Check(flash.gameObject.activeSelf && !wrong.gameObject.activeSelf && completed == 0,
        "queued final success shows the correct flash before completion");
    Advance(0.2f);
    Check(completed == 1 && !flash.gameObject.activeSelf && !wrong.gameObject.activeSelf && !target.gameObject.activeSelf
        && UnityEngine.Vector3.Distance(hand.localPosition, original) < 0.001f
        && UnityEngine.Quaternion.Angle(hand.localRotation, rotation) < 0.01f, "completion runs once after final hand return");
    game.Hit(); Advance(1f); Check(completed == 1, "stale input cannot complete twice");
    Call("Render"); Check(UnityEngine.Mathf.Abs(pointer.anchoredPosition.y + 76) < 0.001f, "render retains authored pointer Y offset");
    game.ResetView();
    Check(UnityEngine.Mathf.Abs(pointer.anchoredPosition.y + 76) < 0.001f && pointer.anchorMin.x == 0,
        "reset returns pointer horizontally without shifting it vertically");
    Set("_round", new TapTapGameJam.UI.PrinterRepairRound(1, 1, 1, () => 0));
    Set("_running", true); Set("_onCompleted", (System.Action)(() => completed++));
    game.Hit(); Advance(0.1f); game.End(); Advance(1f);
    Check(completed == 1 && !flash.gameObject.activeSelf && !wrong.gameObject.activeSelf
        && UnityEngine.Vector3.Distance(hand.localPosition, original) < 0.001f, "exit cancels completion and clears both flashes");
    Check(type.GetField("_progressText", flags) == null && type.GetField("_progressBar", flags) == null,
        "printer no longer depends on progress display components");
    // 模拟首次缓存后才补绑的新 Image，原来共用缓存标记时会得到零透明度。
    var lateWrong = Rect("LateWrongFlash", -122, 27).GetComponent<UnityEngine.UI.Image>();
    lateWrong.color = new UnityEngine.Color(1, 1, 1, 0);
    lateWrong.enabled = false;
    Set("_flashWrongTime", lateWrong);
    Call("ResetStrike");
    Set("_round", new TapTapGameJam.UI.PrinterRepairRound(5, 1, 0.25, () => 1));
    Set("_running", true);
    game.Hit(); Advance(0.3f); // 单帧越过原本只有 0.08 秒的闪光区间。
    Check(lateWrong.gameObject.activeSelf && lateWrong.enabled && lateWrong.color.a > 0.99f
        && !flash.gameObject.activeSelf && lateWrong.transform.GetSiblingIndex() == root.transform.childCount - 1,
        "late-bound transparent wrong flash becomes visible above hand even after a long frame");
    Check(UnityEngine.Vector3.Distance(hand.localPosition, target.localPosition) < 0.001f,
        "long-frame impact still matches hand contact pose");
    Advance(0.04f);
    Check(lateWrong.color.a > 0 && lateWrong.color.a < 1, "late-bound wrong flash fades normally");
    game.End();
    Check(!lateWrong.gameObject.activeSelf, "late-bound wrong flash hides on exit");
    // 未保存新字段的旧场景，也能按同级物体名称找到错误闪光。
    Set("_flashWrongTime", null);
    Call("ResetStrike");
    Check(type.GetField("_flashWrongTime", flags).GetValue(game) == wrong,
        "missing serialized wrong-flash binding resolves from scene child");
    UnityEngine.Debug.Log("Printer animation checks passed.");
}
finally { UnityEngine.Object.DestroyImmediate(root); }
