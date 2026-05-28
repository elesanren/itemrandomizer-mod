using System.Reflection;

namespace StartingAbilityPicker;

public static class RoomRandomMode
{
    private static bool _isPending;

    public static bool IsPending => _isPending;

    public static void ResetPending() => _isPending = false;

    public static void Toggle() => _isPending = !_isPending;

    public static void TryApply()
    {
        if (!_isPending) return;

        var pd = PlayerData.instance;
        if (pd == null)
        {
            Plugin.Log.LogError("RoomRandomMode: PlayerData 不可用");
            _isPending = false;
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
                _isPending = false;
                return;
            }

            if (!hasDash) pd.SetBool("hasDash", true);
            if (!hasSuperJump) pd.SetBool("hasSuperJump", true);
            if (currentRegen < 1) pd.SetInt("silkRegenMax", 1);

            Plugin.Log.LogInfo("RoomRandomMode: 已给予疾风步(hasDash)、超级跳(hasSuperJump)、丝之心(silkRegenMax=1)");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"RoomRandomMode 出错: {ex}");
        }
        finally
        {
            _isPending = false;
        }
    }
}