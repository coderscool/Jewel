using CoreModules.CheatKit.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace CoreModules.CheatKit.Examples
{
    /// <summary>
    /// MODULE GENRE — cheat ACTION (Infinite Ammo / Slow-Mo / Refill / Spawn / Kill All). Thêm vào
    /// panel qua <c>CheatPanelBuilder.AddModule&lt;ActionCheatModule&gt;(panel)</c> (OCP). Phụ thuộc
    /// <see cref="IActionCheatService"/> qua Bind (DIP); UI null-safe; <see cref="OnUpdate"/> cập nhật
    /// text chỉ khi đổi (0 alloc khi đứng yên).
    /// </summary>
    public sealed class ActionCheatModule : UITestModuleBase, ICheatBindable, ICheatModuleUi
    {
        private IActionCheatService _action;

        private Toggle _ammoToggle;
        private Toggle _slowMoToggle;
        private Button _btnRefill;
        private Button _btnSpawn;
        private Button _btnKillAll;
        private Text _statText;

        private int _lastAmmo = -1, _lastEnemies = -1;

        protected override void OnInitialize() => _moduleName = "Action Cheats";

        public void BuildDefaultUI(RectTransform section)
        {
            CheatUi.Label(section, "▼ ACTION CHEATS", 26);

            _ammoToggle = CheatUi.Toggle(section, "Infinite Ammo");
            _slowMoToggle = CheatUi.Toggle(section, "Slow Motion");
            _ammoToggle.onValueChanged.AddListener(OnInfiniteAmmo);
            _slowMoToggle.onValueChanged.AddListener(OnSlowMo);

            var row = CheatUi.Row(section);
            _btnRefill = CheatUi.Button(row, "Refill", new Color(0.40f, 0.70f, 0.50f));
            _btnSpawn = CheatUi.Button(row, "Spawn", new Color(0.80f, 0.55f, 0.30f));
            _btnKillAll = CheatUi.Button(row, "Kill All", new Color(0.80f, 0.35f, 0.35f));

            _btnRefill.onClick.AddListener(OnRefill);
            _btnSpawn.onClick.AddListener(OnSpawn);
            _btnKillAll.onClick.AddListener(OnKillAll);

            _statText = CheatUi.Label(section, "Ammo — · Enemies —", 24);
        }

        public void Bind(CheatServices services)
        {
            _action = services?.Get<IActionCheatService>();
            if (_action != null)
            {
                if (_ammoToggle != null) _ammoToggle.SetIsOnWithoutNotify(_action.InfiniteAmmo);
                if (_slowMoToggle != null) _slowMoToggle.SetIsOnWithoutNotify(_action.SlowMotion);
            }
            ApplyInteractable();
            ForceRefreshText();
        }

        protected override void OnCleanup()
        {
            if (_ammoToggle != null) _ammoToggle.onValueChanged.RemoveListener(OnInfiniteAmmo);
            if (_slowMoToggle != null) _slowMoToggle.onValueChanged.RemoveListener(OnSlowMo);
            if (_btnRefill != null) _btnRefill.onClick.RemoveListener(OnRefill);
            if (_btnSpawn != null) _btnSpawn.onClick.RemoveListener(OnSpawn);
            if (_btnKillAll != null) _btnKillAll.onClick.RemoveListener(OnKillAll);
        }

        public override void OnUpdate()
        {
            if (_action == null || _statText == null) return;
            if (_action.Ammo == _lastAmmo && _action.Enemies == _lastEnemies) return;
            ForceRefreshText();
        }

        private void OnInfiniteAmmo(bool on) { if (_action != null) _action.InfiniteAmmo = on; }
        private void OnSlowMo(bool on) { if (_action != null) _action.SlowMotion = on; }
        private void OnRefill() => _action?.RefillAmmo();
        private void OnSpawn() => _action?.SpawnEnemy();
        private void OnKillAll() => _action?.KillAll();

        private void ApplyInteractable()
        {
            bool has = _action != null;
            if (_ammoToggle != null) _ammoToggle.interactable = has;
            if (_slowMoToggle != null) _slowMoToggle.interactable = has;
            if (_btnRefill != null) _btnRefill.interactable = has;
            if (_btnSpawn != null) _btnSpawn.interactable = has;
            if (_btnKillAll != null) _btnKillAll.interactable = has;
        }

        private void ForceRefreshText()
        {
            if (_statText == null) return;
            if (_action == null) { _statText.text = "Ammo — · Enemies —"; return; }
            _lastAmmo = _action.Ammo; _lastEnemies = _action.Enemies;
            _statText.text = $"Ammo {_lastAmmo} · Enemies {_lastEnemies}";
        }
    }
}
