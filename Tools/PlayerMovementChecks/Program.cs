using System;
using TapTapGameJam.Player;
using UnityEngine;

// Exercise the production states without requiring a running Unity editor.
internal static class Program
{
    private static void Main()
    {
        var player = new PlayerBasic();
        player.StateMachine.ChangeState(player.Idle);
        Time.deltaTime = 11f;
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.HappyIdle, "Long minigame enters HappyIdle");
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.HappyIdle, "No animation event while idle");

        player.InputManager.MoveInput = new Vector2(1f, 0f);
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.Move, "Movement resumes without animation event");
        Check(player.PlayerMovement.Input.x == 1f, "Move state submits input to movement");

        player.InputManager.MoveInput = Vector2.zero;
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.Idle, "Releasing movement returns to idle");
        Check(player.PlayerMovement.Input.x == 0f, "Releasing movement stops player");

        player.StateMachine.Update(); // Timer has crossed the idle threshold again.
        player.InputManager.MoveInput = new Vector2(0f, 1f);
        player.AnimationManager.IsHappyIdleFinished = true;
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.Move, "Movement wins over animation completion");

        player.InputManager.MoveInput = Vector2.zero;
        player.StateMachine.ChangeState(player.Idle);
        Time.deltaTime = 9f;
        player.StateMachine.Update();
        player.InputManager.MoveInput = new Vector2(1f, 0f);
        Time.deltaTime = 2f;
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.Move, "Idle timer cannot override movement in the same frame");

        player.StateMachine.ChangeState(player.HappyIdle);
        player.InputManager.MoveInput = Vector2.zero;
        player.AnimationManager.IsHappyIdleFinished = true;
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.Idle, "Animation completion still returns to idle");

        player.StateMachine.ChangeState(player.HappyIdle);
        player.IsCarry = true;
        player.InputManager.MoveInput = new Vector2(1f, 0f);
        player.StateMachine.Update();
        Check(player.StateMachine.CurrentState == player.CarryAndMove, "Carrying keeps its movement state");
        Console.WriteLine("PASS: 10 player movement regression checks.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
    }
    public static class Time { public static float deltaTime; }
    public static class Debug { public static void LogError(object value) => throw new Exception(value.ToString()); }
}

namespace TapTapGameJam.Player
{
    public class PlayerInputManager
    {
        public Vector2 MoveInput;
        public bool HasMoveInput => MoveInput.x != 0f || MoveInput.y != 0f;
    }
    public class PlayerMovement
    {
        public Vector2 Input;
        public void SetMoveInput(Vector2 input) => Input = input;
    }
    public class PlayerAnimationManager
    {
        public bool IsHappyIdleFinished;
        public void PlayHappyIdle() { }
        public void PlayIdle() { }
        public void PlayMove() { }
        public void PlayCarryIdle(int type) { }
        public void PlayCarryAndMove(int type) { }
    }
    public class PlayerBasic
    {
        public PlayerStateMachine StateMachine = new();
        public PlayerInputManager InputManager = new();
        public PlayerMovement PlayerMovement = new();
        public PlayerAnimationManager AnimationManager = new();
        public bool IsCarry;
        public int CarryType;
        public IdleState Idle;
        public HappyIdleState HappyIdle;
        public MoveState Move;
        public CarryIdleState CarryIdle;
        public CarryAndMoveState CarryAndMove;
        public PlayerBasic()
        {
            Idle = new(this);
            HappyIdle = new(this);
            Move = new(this);
            CarryIdle = new(this);
            CarryAndMove = new(this);
        }
        public void SyncCarryState() => StateMachine.ChangeState(InputManager.HasMoveInput ? CarryAndMove : CarryIdle);
    }
}
