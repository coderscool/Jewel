using UnityEngine;

namespace JewelPainter.Core.Persistence
{
    /// Class thuần (không MonoBehaviour) bọc PlayerPrefs.
    /// Muốn đổi sang file JSON hay cloud save thì chỉ thay class này.
    public class PlayerPrefsSaveService : ISaveService
    {
        public int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(key, defaultValue);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public float GetFloat(string key, float defaultValue = 0f) => PlayerPrefs.GetFloat(key, defaultValue);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public bool GetBool(string key, bool defaultValue = false)
            => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;

        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

        public void Save() => PlayerPrefs.Save();
    }
}
