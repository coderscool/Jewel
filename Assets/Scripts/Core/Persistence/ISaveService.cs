namespace JewelPainter.Core.Persistence
{
    /// Abstraction cho lưu trữ. Domain phụ thuộc interface này,
    /// không bao giờ đụng thẳng PlayerPrefs — nhờ vậy test được không cần Unity.
    public interface ISaveService
    {
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);

        float GetFloat(string key, float defaultValue = 0f);
        void SetFloat(string key, float value);

        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);

        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);

        void DeleteKey(string key);

        void Save();
    }
}
