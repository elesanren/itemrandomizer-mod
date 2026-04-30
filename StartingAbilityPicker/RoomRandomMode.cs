using System.Reflection;

namespace StartingAbilityPicker;

public static class RoomRandomMode
{
    private static bool isPending = false;

    public static bool IsPending
    {
        get { return isPending; }
    }

    public static void ResetPending()
    {
        isPending = false;
    }

    public static void Toggle()
    {
        isPending = !isPending;
    }

    public static void TryApply()
    {
        if (!isPending) return;

        PlayerData pd = PlayerData.instance;
        if (pd == null)
        {
            Plugin.Log.LogError("RoomRandomMode: PlayerData 不可用");
            isPending = false;
            return;
        }

        try
        {
            bool hasDash = pd.GetBool("hasDash");
            bool hasSuperJump = pd.GetBool("hasSuperJump");
            int currentRegen = pd.GetInt("silkRegenMax");

            if (hasDash && hasSuperJump && currentRegen >= 1)
            {
                Plugin.Log.LogInfo("RoomRandomMode: 能力已存在，跳过");
                isPending = false;
                return;
            }

            if (!hasDash)
            {
                var dashField = typeof(PlayerData).GetField("hasDash", BindingFlags.Instance | BindingFlags.Public);
                if (dashField != null && dashField.FieldType == typeof(bool))
                    dashField.SetValue(pd, true);
            }

            if (!hasSuperJump)
            {
                var superJumpField = typeof(PlayerData).GetField("hasSuperJump", BindingFlags.Instance | BindingFlags.Public);
                if (superJumpField != null && superJumpField.FieldType == typeof(bool))
                    superJumpField.SetValue(pd, true);
            }

            if (currentRegen < 1)
            {
                var silkRegenField = typeof(PlayerData).GetField("silkRegenMax", BindingFlags.Instance | BindingFlags.Public);
                if (silkRegenField != null && silkRegenField.FieldType == typeof(int))
                    silkRegenField.SetValue(pd, 1);
            }

            // 只保留日志，不弹窗
            Plugin.Log.LogInfo("RoomRandomMode: 已给予疾风步(hasDash)、超级跳(hasSuperJump)、丝之心(silkRegenMax=1)");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"RoomRandomMode 出错: {ex}");
        }
        finally
        {
            isPending = false;
        }
    }
}