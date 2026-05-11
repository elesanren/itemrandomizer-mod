using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace SilksongItemRandomizer;

public enum TrapPersonality { Friendly, Playful, Sinister }

public enum MoveStyle
{
    SurfacePatrol,
    ZigzagLoop,
    NeedleSwing
}

public class TrapMover : MonoBehaviour
{
    public MoveStyle style;
    public TrapPersonality personality;
    public float baseSpeed = 2f;

    public float SwingMinAngle { get; set; } = -30f;
    public float SwingMaxAngle { get; set; } = 30f;

    private float currentSpeed;
    private int cycleCount;
    private Vector2 startPos;
    private float startAngle;
    private Vector2 leftEdge, rightEdge;
    private int moveDir = 1;
    private List<Vector2> path;
    private int pathIndex;
    private float pathProgress;
    private float swingTimer;

    private void Start()
    {
        startPos = transform.position;
        startAngle = transform.eulerAngles.z;
        switch (style)
        {
            case MoveStyle.SurfacePatrol:
                (leftEdge, rightEdge) = ScanPlatformEdges(startPos);
                break;
            case MoveStyle.ZigzagLoop:
                path = GenerateZigzagLoop(startPos, 5, 10, 8f, 6f);
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
        }
    }

    private void UpdateSpeed()
    {
        currentSpeed = personality switch
        {
            TrapPersonality.Friendly => baseSpeed * (0.3f + UnityEngine.Random.value * 0.2f),
            TrapPersonality.Playful => baseSpeed * (1.2f + UnityEngine.Random.value * 0.8f),
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
            return baseSpeed * (UnityEngine.Random.value > 0.5f ? 1.5f : 0.3f);
        }
        return currentSpeed;
    }

    private void DoSurfacePatrol()
    {
        Vector2 target = moveDir > 0 ? rightEdge : leftEdge;
        Vector2 pos = Vector2.MoveTowards(transform.position, target, currentSpeed * Time.deltaTime);
        transform.position = pos;
        if (Vector2.Distance(transform.position, target) < 0.1f)
            moveDir *= -1;
    }

    private static (Vector2 left, Vector2 right) ScanPlatformEdges(Vector2 origin)
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

    private void DoZigzagLoop()
    {
        if (path is null || path.Count < 2) return;
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
}

public static class TrapMovement
{
    private const float GlobalMoveChance = 0.4f;

    // 所有陷阱都可用的移动方式
    private static readonly List<MoveStyle> AllStyles = new()
    {
        MoveStyle.SurfacePatrol,
        MoveStyle.ZigzagLoop,
        MoveStyle.NeedleSwing
    };

    // 排除岩浆
    private static readonly HashSet<string> ExcludedTraps = new()
    {
        "lava_area"
    };

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