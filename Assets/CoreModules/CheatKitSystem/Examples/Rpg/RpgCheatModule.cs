using CoreModules.CheatKit.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// MODULE GENRE — cheat RPG (God Mode / +XP / Level Up / +Gold). Minh hoạ OCP: thêm vào panel
    /// qua <c>CheatPanelBuilder.AddModule&lt;RpgCheatModule&gt;(panel)</c>, KHÔNG sửa builder/panel.
    /// Phụ thuộc <see cref="IRpgCheatService"/> qua <c>Bind</c> (DIP); UI &amp; service null-safe.
    /// <see cref="OnUpdate"/> chỉ chạm <c>Text.text</c> khi giá trị đổi → 0 alloc khi đứng yên (mobile).
    /// </summary>
    public sealed class RpgCheatModule : UITestModuleBase, ICheatBindable, ICheatModuleUi
    {
        private IRpgCheatService _rpg;

        private Toggle _godToggle;
        private Button _btnAddXp;
        private Button _btnLevelUp;
        private Button _btnAddGold;
        private Text _statText;

        private int _lastLevel = -1, _lastXp = -1, _lastGold = -1;

        protected override void OnInitialize() => _moduleName = "RPG Cheats";

        public void BuildDefaultUI(RectTransform section)
        {
            CheatUi.Label(section, "▼ RPG CHEATS", 26);

            _godToggle = CheatUi.Toggle(section, "God Mode");
            _godToggle.onValueChanged.AddListener(OnGod);

            var row = CheatUi.Row(section);
            _btnAddXp = CheatUi.Button(row, "+100 XP", new Color(0.45f, 0.65f, 0.85f));
            _btnLevelUp = CheatUi.Button(row, "Level Up", new Color(0.55f, 0.45f, 0.80f));

            _btnAddGold = CheatUi.Button(section, "+1000 Gold", new Color(0.85f, 0.70f, 0.25f));

            _btnAddXp.onClick.AddListener(OnAddXp);
            _btnLevelUp.onClick.AddListener(OnLevelUp);
            _btnAddGold.onClick.AddListener(OnAddGold);

            _statText = CheatUi.Label(section, "Lv — · XP — · Gold —", 24);
        }

        public void Bind(CheatServices services)
        {
            _rpg = services?.Get<IRpgCheatService>();
            if (_godToggle != null && _rpg != null)
                _godToggle.SetIsOnWithoutNotify(_rpg.GodMode);
            ApplyInteractable();
            ForceRefreshText();
        }

        protected override void OnCleanup()
        {
            if (_godToggle != null) _godToggle.onValueChanged.RemoveListener(OnGod);
            if (_btnAddXp != null) _btnAddXp.onClick.RemoveListener(OnAddXp);
            if (_btnLevelUp != null) _btnLevelUp.onClick.RemoveListener(OnLevelUp);
            if (_btnAddGold != null) _btnAddGold.onClick.RemoveListener(OnAddGold);
        }

        public override void OnUpdate()
        {
            if (_rpg == null || _statText == null) return;
            if (_rpg.PartyLevel == _lastLevel && _rpg.Xp == _lastXp && _rpg.Gold == _lastGold) return;
            ForceRefreshText();
        }

        private void OnGod(bool on) { if (_rpg != null) _rpg.GodMode = on; }
        private void OnAddXp() => _rpg?.AddXp(100);
        private void OnLevelUp() => _rpg?.LevelUp();
        private void OnAddGold() => _rpg?.AddGold(1000);

        private void ApplyInteractable()
        {
            bool has = _rpg != null;
            if (_godToggle != null) _godToggle.interactable = has;
            if (_btnAddXp != null) _btnAddXp.interactable = has;
            if (_btnLevelUp != null) _btnLevelUp.interactable = has;
            if (_btnAddGold != null) _btnAddGold.interactable = has;
        }

        private void ForceRefreshText()
        {
            if (_statText == null) return;
            if (_rpg == null) { _statText.text = "Lv — · XP — · Gold —"; return; }
            _lastLevel = _rpg.PartyLevel; _lastXp = _rpg.Xp; _lastGold = _rpg.Gold;
            _statText.text = $"Lv {_lastLevel} · XP {_lastXp} · Gold {_lastGold}";
        }
    }
}
