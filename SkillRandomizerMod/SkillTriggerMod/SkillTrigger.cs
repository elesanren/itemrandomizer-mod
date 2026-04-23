using UnityEngine;

namespace SkillTriggerMod;

public class SkillTrigger : MonoBehaviour
{
    private bool _triggered = false;
    private string _sceneName;
    private int _index;
    private string _recordKey;

    public void SetInfo(string sceneName, int index, string recordKey)
    {
        _sceneName = sceneName;
        _index = index;
        _recordKey = recordKey;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Hero") && !other.CompareTag("Player")) return;

        _triggered = true;

        if (_sceneName == "Shellwood_10")
            SkillRandomizer.GiveWallJump();
        else
            SkillRandomizer.GiveRandomSkill();

        Plugin._triggeredRecords.Add(_recordKey);
        Plugin.Instance.SaveTriggerRecords();

        Destroy(gameObject);
    }
}