using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace SilksongItemRandomizer;

public enum TrapPersonality { Friendly, Playful, Sinister }

public enum MoveStyle
{
    SurfacePatrol,
    ZigzagLoop,
    NeedleSwing,
    Spin,
    Pulse,
    Flicker,
    Jitter,
    Bounce
}

public class TrapMover : MonoBehaviour
{
    public MoveStyle style;
    public TrapPersonality personality;
    public float baseSpeed = 1f;

    public float SwingMinAngle { get; set; } = -30f;
    public float SwingMaxAngle { get; set; } = 30f;

    private float currentSpeed;
    private int cycleCount;
    private Vector2 startPos;
    private Vector2 leftEdge, rightEdge;
    private int moveDir = 1;
    private List<Vector2> path;
    private int pathIndex;
    private float pathProgress;
    private float swingTimer;
    private float startAngle;

    // 新增原地运动
    private float spinAngle;
    private int spinDir = 1;
    private Vector3 originalScale;
    private float pulseTimer;
    private SpriteRenderer spriteRenderer;
    private float flickerTimer;
    private bool isVisible = true;
    private Vector3 originalPos;
    private float jitterTimer;
    private float bounceTimer;

    private void Start()
    {
        startPos = transform.position;
        startAngle = transform.eulerAngles.z;
        originalScale = transform.localScale;
        originalPos = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();

        switch (style)
        {
            case MoveStyle.SurfacePatrol:
                (leftEdge, rightEdge) = ScanPlatformEdges(startPos);
                break;
            case MoveStyle.ZigzagLoop:
                if (UnityEngine.Random.value < 0.5f)
                    path = GeneratePresetPath(startPos);
                else
                    path = GenerateZigzagLoop(startPos, 5, 10, 8f, 6f);
                break;
            case MoveStyle.Spin:
                spinDir = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                break;
            case MoveStyle.Pulse:
                pulseTimer = UnityEngine.Random.Range(0f, 2f);
                break;
            case MoveStyle.Flicker:
                flickerTimer = UnityEngine.Random.Range(0f, 3f);
                break;
            case MoveStyle.Jitter:
                jitterTimer = UnityEngine.Random.Range(0f, 1f);
                break;
            case MoveStyle.Bounce:
                bounceTimer = UnityEngine.Random.Range(0f, Mathf.PI * 2);
                break;
        }
        UpdateSpeed();
    }

    private void Update()
    {
        UpdateSpeed();
        switch (style)
        {
            case MoveStyle.SurfacePatrol: DoSurfacePatrol(); break;
            case MoveStyle.ZigzagLoop: DoZigzagLoop(); break;
            case MoveStyle.NeedleSwing: DoNeedleSwing(); break;
            case MoveStyle.Spin: DoSpin(); break;
            case MoveStyle.Pulse: DoPulse(); break;
            case MoveStyle.Flicker: DoFlicker(); break;
            case MoveStyle.Jitter: DoJitter(); break;
            case MoveStyle.Bounce: DoBounce(); break;
        }
    }

    private void UpdateSpeed()
    {
        currentSpeed = personality switch
        {
            TrapPersonality.Friendly => baseSpeed * (0.15f + UnityEngine.Random.value * 0.1f),
            TrapPersonality.Playful => baseSpeed * (0.6f + UnityEngine.Random.value * 0.4f),
            TrapPersonality.Sinister => UpdateSinisterSpeed(),
            _ => currentSpeed
        };
    }

    private float UpdateSinisterSpeed()
    {
        cycleCount++;
        if (cycleCount >= 180)
        {
            cycleCount = 0;
            return baseSpeed * (UnityEngine.Random.value > 0.5f ? 0.75f : 0.15f);
        }
        return currentSpeed;
    }

    private void DoSurfacePatrol()
    {
        Vector2 target = moveDir > 0 ? rightEdge : leftEdge;
        transform.position = Vector2.MoveTowards(transform.position, target, currentSpeed * Time.deltaTime);
        if (Vector2.Distance(transform.position, target) < 0.1f) moveDir *= -1;
    }

    private (Vector2 left, Vector2 right) ScanPlatformEdges(Vector2 origin)
    {
        int mask = LayerMask.GetMask("Terrain");
        float y = origin.y + 0.5f;
        float left = origin.x, right = origin.x;
        for (int i = 0; i < 40; i++)
        {
            if (Physics2D.Raycast(new Vector2(left - 0.5f, y), Vector2.down, 1f, mask))
                left -= 0.5f;
            else break;
        }
        for (int i = 0; i < 40; i++)
        {
            if (Physics2D.Raycast(new Vector2(right + 0.5f, y), Vector2.down, 1f, mask))
                right += 0.5f;
            else break;
        }
        return (new Vector2(left, origin.y), new Vector2(right, origin.y));
    }

    private void DoZigzagLoop()
    {
        if (path == null || path.Count < 2) return;
        pathProgress += currentSpeed * Time.deltaTime;
        while (pathProgress > 1f && pathIndex < path.Count - 1)
        {
            pathProgress -= 1f;
            pathIndex++;
        }
        if (pathIndex >= path.Count - 1)
        {
            pathIndex = 0;
            pathProgress = 0f;
        }
        transform.position = Vector2.Lerp(path[pathIndex], path[pathIndex + 1], pathProgress);
    }

    private void DoNeedleSwing()
    {
        swingTimer += Time.deltaTime * currentSpeed;
        float angle = Mathf.Lerp(SwingMinAngle, SwingMaxAngle, (Mathf.Sin(swingTimer) + 1f) / 2f);
        transform.rotation = Quaternion.Euler(0, 0, startAngle + angle);
    }

    private void DoSpin()
    {
        spinAngle += 90f * currentSpeed * Time.deltaTime * spinDir;
        transform.rotation = Quaternion.Euler(0, 0, spinAngle);
    }

    private void DoPulse()
    {
        pulseTimer += Time.deltaTime * currentSpeed * 2f;
        float scale = 1f + Mathf.Sin(pulseTimer) * 0.2f;
        transform.localScale = originalScale * scale;
    }

    private void DoFlicker()
    {
        flickerTimer += Time.deltaTime * currentSpeed;
        float period = 0.5f;
        bool shouldBeVisible = (Mathf.FloorToInt(flickerTimer / period) % 2) == 0;
        if (shouldBeVisible != isVisible)
        {
            isVisible = shouldBeVisible;
            if (spriteRenderer != null)
                spriteRenderer.enabled = isVisible;
            else
                gameObject.SetActive(isVisible);
        }
    }

    private void DoJitter()
    {
        jitterTimer += Time.deltaTime * 10f;
        float offsetX = (Mathf.PerlinNoise(jitterTimer, 0) - 0.5f) * 0.2f;
        float offsetY = (Mathf.PerlinNoise(0, jitterTimer) - 0.5f) * 0.2f;
        transform.position = originalPos + new Vector3(offsetX, offsetY, 0);
    }

    private void DoBounce()
    {
        bounceTimer += Time.deltaTime * currentSpeed * 2f;
        float offsetY = Mathf.Sin(bounceTimer) * 0.3f;
        transform.position = new Vector3(originalPos.x, originalPos.y + offsetY, originalPos.z);
    }

    // ----- 路径生成（完全保留）-----
    private static List<Vector2> GeneratePresetPath(Vector2 origin)
    {
        float r = 4f;
        var shapes = new List<System.Func<List<Vector2>>>
        {
            () => Square(origin, r),
            () => Hexagon(origin, r),
            () => Hexagram(origin, r),
            () => Triangle(origin, r),
            () => Pentagram(origin, r),
            () => Circle(origin, r, 14),
            () => Figure8(origin, r, 12),
            () => Diamond(origin, r),
            () => Cross(origin, r),
        };
        return shapes[UnityEngine.Random.Range(0, shapes.Count)]();
    }

    private static List<Vector2> Square(Vector2 o, float r) => new() { o + new Vector2(-r, r), o + new Vector2(r, r), o + new Vector2(r, -r), o + new Vector2(-r, -r), o + new Vector2(-r, r), o };
    private static List<Vector2> Hexagon(Vector2 o, float r)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= 6; i++) { float angle = Mathf.Deg2Rad * (60f * i - 30f); pts.Add(o + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r)); }
        pts.Add(o); return pts;
    }
    private static List<Vector2> Hexagram(Vector2 o, float r)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < 6; i++) { float angle = Mathf.Deg2Rad * (60f * i - 90f); pts.Add(o + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r)); }
        pts.Add(o); return pts;
    }
    private static List<Vector2> Triangle(Vector2 o, float r)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < 3; i++) { float angle = Mathf.Deg2Rad * (120f * i - 90f); pts.Add(o + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r)); }
        pts.Add(o); return pts;
    }
    private static List<Vector2> Pentagram(Vector2 o, float r)
    {
        var pts = new List<Vector2>();
        float r2 = r * 0.382f;
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Deg2Rad * (72f * i - 90f);
            pts.Add(o + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r));
            pts.Add(o + new Vector2(Mathf.Cos(angle + Mathf.Deg2Rad * 36f) * r2, Mathf.Sin(angle + Mathf.Deg2Rad * 36f) * r2));
        }
        pts.Add(o); return pts;
    }
    private static List<Vector2> Circle(Vector2 o, float r, int segments)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= segments; i++) { float angle = Mathf.Deg2Rad * (360f * i / segments); pts.Add(o + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r)); }
        pts.Add(o); return pts;
    }
    private static List<Vector2> Figure8(Vector2 o, float r, int segments)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= segments / 2; i++) { float angle = Mathf.Deg2Rad * (360f * i / segments); pts.Add(o + new Vector2(r + Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 1.5f)); }
        for (int i = segments / 2; i <= segments; i++) { float angle = Mathf.Deg2Rad * (360f * i / segments); pts.Add(o + new Vector2(-r + Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 1.5f)); }
        pts.Add(o); return pts;
    }
    private static List<Vector2> Diamond(Vector2 o, float r) => new() { o + new Vector2(0, r), o + new Vector2(r, 0), o + new Vector2(0, -r), o + new Vector2(-r, 0), o + new Vector2(0, r), o };
    private static List<Vector2> Cross(Vector2 o, float r)
    {
        float half = r * 0.4f;
        return new List<Vector2>
        {
            o + new Vector2(0, r), o + new Vector2(half, r * 0.6f), o + new Vector2(half, half), o + new Vector2(r * 0.6f, half), o + new Vector2(r, 0),
            o + new Vector2(r * 0.6f, -half), o + new Vector2(half, -half), o + new Vector2(half, -r * 0.6f), o + new Vector2(0, -r),
            o + new Vector2(-half, -r * 0.6f), o + new Vector2(-half, -half), o + new Vector2(-r * 0.6f, -half), o + new Vector2(-r, 0),
            o + new Vector2(-r * 0.6f, half), o + new Vector2(-half, half), o + new Vector2(-half, r * 0.6f), o
        };
    }

    private static List<Vector2> GenerateZigzagLoop(Vector2 start, int minSeg, int maxSeg, float maxW, float maxH)
    {
        var rng = new System.Random();
        int n = rng.Next(minSeg, maxSeg);
        var list = new List<Vector2> { start };
        float x = start.x, y = start.y;
        for (int i = 0; i < n - 1; i++)
        {
            float dx = (rng.Next(2) == 0 ? 1 : -1) * (5f + (float)rng.NextDouble() * 7f);
            float dy = (rng.Next(2) == 0 ? 1 : -1) * (3f + (float)rng.NextDouble() * 5f);
            x += dx;
            y += dy;
            list.Add(new Vector2(x, y));
        }
        list.Add(start);
        return list;
    }
}

public static class TrapMovement
{
    private const float GlobalMoveChance = 0.4f;

    private static readonly List<MoveStyle> AllStyles = new()
    {
        MoveStyle.SurfacePatrol,
        MoveStyle.ZigzagLoop,
        MoveStyle.NeedleSwing,
        MoveStyle.Spin,
        MoveStyle.Pulse,
        MoveStyle.Flicker,
        MoveStyle.Jitter,
        MoveStyle.Bounce
    };

    private static readonly HashSet<string> ExcludedTraps = new() { "lava_area" };

    public static void ApplyTrapMovement(GameObject trapObj, string trapId, Random rng)
    {
        if (ExcludedTraps.Contains(trapId)) return;
        if (rng.NextDouble() > GlobalMoveChance) return;

        MoveStyle style = AllStyles[rng.Next(AllStyles.Count)];
        TrapPersonality personality = (TrapPersonality)rng.Next(0, 3);

        var mover = trapObj.AddComponent<TrapMover>();
        mover.style = style;
        mover.personality = personality;
        mover.baseSpeed = 2f;

        if (style == MoveStyle.NeedleSwing)
        {
            mover.SwingMinAngle = -30f + (float)rng.NextDouble() * 10f - 5f;
            mover.SwingMaxAngle = 30f + (float)rng.NextDouble() * 10f - 5f;
        }
    }
}