using System;
using System.Collections.Generic;

namespace TapTapGameJam.UI
{
    // 在 UI 局部坐标内计算圆形碰撞，不依赖 Canvas 的世界缩放或全局 Physics2D。
    public sealed class DogFoodPileSimulation
    {
        public sealed class Body
        {
            public float X, Y, VelocityX, VelocityY;
            public float Radius { get; }
            public bool Landed { get; internal set; }
            public bool Lost { get; internal set; }
            internal float PreviousX, PreviousY;
            internal bool Supported;
            internal bool EnteredBowl;

            internal Body(float x, float y, float radius, float velocityX, float velocityY)
            {
                X = x; Y = y; Radius = radius; VelocityX = velocityX; VelocityY = velocityY;
            }
        }

        private readonly List<Body> _bodies = new();
        private float _accumulator;
        private const float StepTime = 1f / 120f;
        public float Left { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float Top { get; }
        public float Gravity { get; }

        public DogFoodPileSimulation(float left, float right, float bottom, float top, float gravity)
        {
            if (right <= left || top <= bottom || gravity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(right));
            Left = left; Right = right; Bottom = bottom; Top = top; Gravity = gravity;
        }

        public Body Add(float x, float y, float radius, float velocityX, float velocityY = 0f)
        {
            if (radius <= 0f || radius * 2f >= Right - Left)
                throw new ArgumentOutOfRangeException(nameof(radius));
            var body = new Body(x, y, radius, velocityX, velocityY);
            body.EnteredBowl = y - radius <= Top && y + radius >= Bottom
                && x >= Left + radius && x <= Right - radius;
            _bodies.Add(body);
            return body;
        }

        public void Advance(float deltaTime)
        {
            // 小步长防止高速穿透；长时间卡顿后不追赶数秒的物理计算。
            _accumulator += Math.Min(Math.Max(0f, deltaTime), 0.1f);
            while (_accumulator >= StepTime)
            {
                Step();
                _accumulator -= StepTime;
            }
        }

        private void Step()
        {
            foreach (Body body in _bodies)
            {
                body.PreviousX = body.X;
                body.PreviousY = body.Y;
                body.Supported = false;
                body.VelocityY -= Gravity * StepTime;
                // 碰撞步长内最多移动半径的一半，避免穿过其他颗粒。
                float speed = (float)Math.Sqrt(body.VelocityX * body.VelocityX + body.VelocityY * body.VelocityY);
                float scale = speed > 0f ? Math.Min(1f, body.Radius * 0.5f / (speed * StepTime)) : 1f;
                body.X += body.VelocityX * scale * StepTime;
                body.Y += body.VelocityY * scale * StepTime;
            }

            for (int iteration = 0; iteration < 10; iteration++)
            {
                // 先处理碗底，再由下向上传递支撑。
                foreach (Body body in _bodies) ConstrainBowl(body);
                for (int i = 0; i < _bodies.Count; i++)
                    for (int j = i + 1; j < _bodies.Count; j++)
                        ResolvePair(_bodies[i], _bodies[j]);
            }

            foreach (Body body in _bodies)
            {
                ConstrainBowl(body);
                body.VelocityX = (body.X - body.PreviousX) / StepTime;
                body.VelocityY = (body.Y - body.PreviousY) / StepTime;
                if (!body.EnteredBowl && !body.Supported && body.Y < Bottom - body.Radius * 4f)
                    body.Lost = true;
                if (!body.Supported) continue;
                // 接触摩擦与无弹性碰撞，让粮堆稳定下来。
                body.VelocityX *= 0.8f;
                body.VelocityY = Math.Min(0f, body.VelocityY) * 0.5f;
                body.Landed = true;
                body.EnteredBowl = true;
            }
            _bodies.RemoveAll(body => body.Lost);
        }

        private void ConstrainBowl(Body body)
        {
            // 开口上方不限制横向位置，颗粒仍从真实袋口出现。
            // 只有真正进入碗内才启用碗底和内壁；碗外颗粒不能吸附或瞬移进碗。
            if (body.Y - body.Radius <= Top && body.Y + body.Radius >= Bottom
                && body.X >= Left + body.Radius && body.X <= Right - body.Radius)
                body.EnteredBowl = true;
            if (!body.EnteredBowl) return;
            if (body.EnteredBowl)
                body.X = Math.Max(Left + body.Radius, Math.Min(Right - body.Radius, body.X));
            if (body.Y > Bottom + body.Radius + 0.01f) return;
            body.Y = Bottom + body.Radius;
            body.Supported = true;
        }

        private static void ResolvePair(Body a, Body b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float radius = a.Radius + b.Radius;
            float squared = dx * dx + dy * dy;
            if (squared > (radius + 0.01f) * (radius + 0.01f)) return;
            float distance = (float)Math.Sqrt(squared);
            float nx = distance > 0.0001f ? dx / distance : 1f;
            float ny = distance > 0.0001f ? dy / distance : 0f;
            if (ny > 0.3f && a.Supported) b.Supported = true;
            if (ny < -0.3f && b.Supported) a.Supported = true;
            float overlap = Math.Max(0f, radius - distance);
            a.X -= nx * overlap * 0.5f;
            a.Y -= ny * overlap * 0.5f;
            b.X += nx * overlap * 0.5f;
            b.Y += ny * overlap * 0.5f;
        }
    }
}
