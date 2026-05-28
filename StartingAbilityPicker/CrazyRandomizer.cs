using System;

namespace StartingAbilityPicker;

public static class CrazyRandomizer
{
    private static readonly Random GlobalRng = new Random();

    public static void Apply(Plugin plugin)
    {
        // 攻击方向随机
        plugin.allowUpward = GlobalRng.Next(2) == 1;
        plugin.allowLeft = GlobalRng.Next(2) == 1;
        plugin.allowRight = GlobalRng.Next(2) == 1;

        // 进入分类随机模式
        plugin.skillMode = true;

        plugin.skillV = WeightedRandom(Plugin.MaxVertical, GlobalRng);
        plugin.skillH = WeightedRandom(Plugin.MaxHorizontal, GlobalRng);
        plugin.skillS = WeightedRandom(Plugin.MaxSpecial, GlobalRng);
        plugin.skillA = WeightedRandom(Plugin.MaxAttack, GlobalRng);

        // 保底：至少一个垂直和水平技能
        if (plugin.skillV == 0) plugin.skillV = 1;
        if (plugin.skillH == 0) plugin.skillH = 1;

        plugin.itemCount = WeightedRandom(10, GlobalRng);
        plugin.seedInput = GlobalRng.Next(1, int.MaxValue).ToString();
    }

    private static int WeightedRandom(int max, Random rng)
    {
        if (max <= 0) return 0;
        // 预计算权重和
        int totalWeight = 0;
        for (int i = 0; i <= max; i++)
        {
            int w = max - i + 1;
            totalWeight += w * w;
        }

        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        for (int i = 0; i <= max; i++)
        {
            int w = max - i + 1;
            cumulative += w * w;
            if (roll < cumulative)
                return i;
        }
        return 0;
    }
}