using System;
using Random = System.Random;

namespace StartingAbilityPicker
{
    public static class CrazyRandomizer
    {
        public static void Apply(Plugin plugin)
        {
            var rng = new Random();

            // 攻击方向随机
            plugin.allowUpward = rng.Next(2) == 1;
            plugin.allowLeft = rng.Next(2) == 1;
            plugin.allowRight = rng.Next(2) == 1;

            // 进入分类随机模式
            plugin.skillMode = true;

            int maxV = Plugin.MaxVertical;
            int maxH = Plugin.MaxHorizontal;
            int maxS = Plugin.MaxSpecial;
            int maxA = Plugin.MaxAttack;

            // 二次方衰减权重
            int v = WeightedRandom(maxV, rng);
            int h = WeightedRandom(maxH, rng);
            int s = WeightedRandom(maxS, rng);
            int a = WeightedRandom(maxA, rng);

            // 保底
            if (v == 0) v = 1;
            if (h == 0) h = 1;

            plugin.skillV = v;
            plugin.skillH = h;
            plugin.skillS = s;
            plugin.skillA = a;

            // 物品数量
            plugin.itemCount = WeightedRandom(10, rng);

            // 种子随机
            plugin.seedInput = rng.Next(1, int.MaxValue).ToString();

            // 重置种子世界：不随机，保持原样
        }

        private static int WeightedRandom(int max, Random rng)
        {
            if (max <= 0) return 0;

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
}