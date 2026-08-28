using CoreModules.CheatKit.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace CoreModules.CheatKit.Modules
{
    /// <summary>
    /// Module điều khiển luồng &amp; cheat — THUẦN PORT.
    ///
    /// • Flow (<see cref="IFlowCheatService"/>): Force Win / Force Lose / Trigger No-Moves.
    /// • Progress + Currency (<see cref="IProgressCheatService"/>): Unlock All / Set Progress /
    ///   Add Coins / Clear Save.
    /// • Time scale: <see cref="Time.timeScale"/> (generic, không cần port). Khôi phục 1.0 khi cleanup.
    /// • Gameplay flag (<see cref="CheatFlags"/>): Invincible / Infinite Moves / Infinite Undo —
    ///   game tự đọc các cờ tĩnh này.
    ///
    /// Mọi tham chiếu UI &amp; service đều null-safe → gắn được UI tối giản, ẩn phần không dùng.
    /// </summary>
    public class GameControlModule : UITestModuleBase, ICheatBindable, ICheatModuleUi
    {
        #region Serialized
        [Header("Flow")]
        [SerializeField] private Button _btnForceWin;
        [SerializeField] private Button _btnForceLose;
        [SerializeField] private Button _btnNoMoves;

        [Header("Progress / Currency")]
        [SerializeField] private Button _btnUnlockAll;
        [SerializeField] private Button _btnClearSave;
        [SerializeField] private InputField _progressInput;   // 1-based
        [SerializeField] private Button _btnSetProgress;
        [SerializeField] private InputField _coinsInput;
        [SerializeField] private Button _btnAddCoins;
        [SerializeField] private Text _coinsText;

        [Header("Time")]
        [SerializeField] private Slider _timeScaleSlider;
        [SerializeField] private Text _timeScaleText;

        [Header("Cheat Flags (game tự đọc CheatFlags.*)")]
        [SerializeField] private Toggle _invincibilityToggle;
        [SerializeField] private Toggle _infiniteMovesToggle;
        [SerializeField] private Toggle _infiniteUndoToggle;
        #endregion

        #region State
        private IFlowCheatService _flow;
        private IProgressCheatService _progress;
        private bool _restoreTimeScale;
        #endregion

        protected override void OnInitialize()
        {
            _moduleName = "Game Control";

            if (_btnForceWin != null) _btnForceWin.onClick.AddListener(OnForceWin);
            if (_btnForceLose != null) _btnForceLose.onClick.AddListener(OnForceLose);
            if (_btnNoMoves != null) _btnNoMoves.onClick.AddListener(OnNoMoves);

            if (_btnUnlockAll != null) _btnUnlockAll.onClick.AddListener(OnUnlockAll);
            if (_btnClearSave != null) _btnClearSave.onClick.AddListener(OnClearSave);
            if (_btnSetProgress != null) _btnSetProgress.onClick.AddListener(OnSetProgress);
            if (_btnAddCoins != null) _btnAddCoins.onClick.AddListener(OnAddCoins);

            if (_timeScaleSlider != null)
            {
                _timeScaleSlider.onValueChanged.AddListener(OnTimeScaleChanged);
                _timeScaleSlider.SetValueWithoutNotify(Time.timeScale);
                UpdateTimeScaleText(Time.timeScale);
            }

            if (_progressInput != null) _progressInput.contentType = InputField.ContentType.IntegerNumber;
            if (_coinsInput != null) _coinsInput.contentType = InputField.ContentType.IntegerNumber;

            if (_invincibilityToggle != null)
            {
                _invincibilityToggle.SetIsOnWithoutNotify(CheatFlags.IsInvincible);
                _invincibilityToggle.onValueChanged.AddListener(OnInvincibility);
            }
            if (_infiniteMovesToggle != null)
            {
                _infiniteMovesToggle.SetIsOnWithoutNotify(CheatFlags.HasInfiniteMoves);
                _infiniteMovesToggle.onValueChanged.AddListener(OnInfiniteMoves);
            }
            if (_infiniteUndoToggle != null)
            {
                _infiniteUndoToggle.SetIsOnWithoutNotify(CheatFlags.HasInfiniteUndo);
                _infiniteUndoToggle.onValueChanged.AddListener(OnInfiniteUndo);
            }

            RefreshUI();
        }

        /// <summary>No-prefab path: dựng control + gán field nội bộ (Builder gọi trước Initialize).</summary>
        public void BuildDefaultUI(RectTransform section)
        {
            CheatUi.Label(section, "▼ GAME CONTROL", 26);

            var flow = CheatUi.Row(section);
            _btnForceWin = CheatUi.Button(flow, "WIN", new Color(0.90f, 0.75f, 0.20f));
            _btnForceLose = CheatUi.Button(flow, "LOSE", new Color(0.80f, 0.30f, 0.30f));
            _btnNoMoves = CheatUi.Button(flow, "NoMove", new Color(0.70f, 0.40f, 0.70f));

            _timeScaleText = CheatUi.Label(section, "Speed: 1.00x", 24);
            _timeScaleSlider = CheatUi.Slider(section, 0f, 3f, 1f);

            _invincibilityToggle = CheatUi.Toggle(section, "Invincible");
            _infiniteMovesToggle = CheatUi.Toggle(section, "Infinite Moves");
            _infiniteUndoToggle = CheatUi.Toggle(section, "Infinite Undo");

            var prog = CheatUi.Row(section);
            _btnUnlockAll = CheatUi.Button(prog, "UnlockAll", new Color(0.90f, 0.50f, 0.30f));
            _btnClearSave = CheatUi.Button(prog, "ClearSave", new Color(0.60f, 0.40f, 0.40f));

            var setProg = CheatUi.Row(section);
            _progressInput = CheatUi.Input(setProg, "lv");
            _btnSetProgress = CheatUi.Button(setProg, "SetProg", new Color(0.40f, 0.60f, 0.80f));

            _coinsText = CheatUi.Label(section, "Coins: —", 24);
            var coins = CheatUi.Row(section);
            _coinsInput = CheatUi.Input(coins, "amt");
            _btnAddCoins = CheatUi.Button(coins, "AddCoins", new Color(0.40f, 0.70f, 0.50f));
        }

        public void Bind(CheatServices services)
        {
            _flow = services?.Flow;
            _progress = services?.Progress;
            RefreshUI();
        }

        protected override void OnCleanup()
        {
            if (_btnForceWin != null) _btnForceWin.onClick.RemoveListener(OnForceWin);
            if (_btnForceLose != null) _btnForceLose.onClick.RemoveListener(OnForceLose);
            if (_btnNoMoves != null) _btnNoMoves.onClick.RemoveListener(OnNoMoves);
            if (_btnUnlockAll != null) _btnUnlockAll.onClick.RemoveListener(OnUnlockAll);
            if (_btnClearSave != null) _btnClearSave.onClick.RemoveListener(OnClearSave);
            if (_btnSetProgress != null) _btnSetProgress.onClick.RemoveListener(OnSetProgress);
            if (_btnAddCoins != null) _btnAddCoins.onClick.RemoveListener(OnAddCoins);
            if (_timeScaleSlider != null) _timeScaleSlider.onValueChanged.RemoveListener(OnTimeScaleChanged);
            if (_invincibilityToggle != null) _invincibilityToggle.onValueChanged.RemoveListener(OnInvincibility);
            if (_infiniteMovesToggle != null) _infiniteMovesToggle.onValueChanged.RemoveListener(OnInfiniteMoves);
            if (_infiniteUndoToggle != null) _infiniteUndoToggle.onValueChanged.RemoveListener(OnInfiniteUndo);

            // Không để game đứng hình nếu user kéo time scale rồi tắt panel.
            if (_restoreTimeScale) Time.timeScale = 1f;
        }

        /// <summary>Cập nhật hiển thị coins + gate nút theo phase. Zero-alloc trừ khi coins đổi.</summary>
        public override void OnUpdate()
        {
            if (_coinsText != null && _progress != null && _progress.Coins >= 0)
                _coinsText.text = $"Coins: {_progress.Coins}";

            ApplyInteractable();
        }

        #region Flow callbacks
        private void OnForceWin()
        {
            if (_flow == null) { LogWarning("Flow service null — ForceWin bỏ qua."); return; }
            _flow.ForceWin();
        }

        private void OnForceLose()
        {
            if (_flow == null) { LogWarning("Flow service null — ForceLose bỏ qua."); return; }
            _flow.ForceLose();
        }

        private void OnNoMoves()
        {
            if (_flow == null) { LogWarning("Flow service null — TriggerNoMoves bỏ qua."); return; }
            _flow.TriggerNoMoves();
        }
        #endregion

        #region Progress / Currency callbacks
        private void OnUnlockAll()
        {
            if (_progress == null) { LogWarning("Progress service null — UnlockAll bỏ qua."); return; }
            _progress.UnlockAll();
        }

        private void OnClearSave()
        {
            if (_progress == null) { LogWarning("Progress service null — ClearSave bỏ qua."); return; }
            _progress.ClearSave();
        }

        private void OnSetProgress()
        {
            if (_progress == null || _progressInput == null) return;
            if (int.TryParse(_progressInput.text, out int oneBased))
                _progress.SetProgress(Mathf.Max(0, oneBased - 1));
        }

        private void OnAddCoins()
        {
            if (_progress == null || _coinsInput == null) return;
            if (int.TryParse(_coinsInput.text, out int amount))
                _progress.AddCoins(amount);
        }
        #endregion

        #region Time / Flag callbacks
        private void OnTimeScaleChanged(float v)
        {
            Time.timeScale = v;
            _restoreTimeScale = !Mathf.Approximately(v, 1f);
            UpdateTimeScaleText(v);
        }

        private void OnInvincibility(bool on) => CheatFlags.IsInvincible = on;
        private void OnInfiniteMoves(bool on) => CheatFlags.HasInfiniteMoves = on;
        private void OnInfiniteUndo(bool on) => CheatFlags.HasInfiniteUndo = on;
        #endregion

        #region UI
        private void UpdateTimeScaleText(float v)
        {
            if (_timeScaleText != null) _timeScaleText.text = $"Speed: {v:F2}x";
        }

        private void RefreshUI()
        {
            if (_coinsText != null)
                _coinsText.text = _progress != null && _progress.Coins >= 0 ? $"Coins: {_progress.Coins}" : "Coins: —";
            ApplyInteractable();
        }

        private void ApplyInteractable()
        {
            bool hasFlow = _flow != null;
            bool hasProgress = _progress != null;

            if (_btnForceWin != null) _btnForceWin.interactable = hasFlow && _flow.CanForceWin;
            if (_btnForceLose != null) _btnForceLose.interactable = hasFlow;
            if (_btnNoMoves != null) _btnNoMoves.interactable = hasFlow;

            if (_btnUnlockAll != null) _btnUnlockAll.interactable = hasProgress;
            if (_btnClearSave != null) _btnClearSave.interactable = hasProgress;
            if (_btnSetProgress != null) _btnSetProgress.interactable = hasProgress;
            if (_btnAddCoins != null) _btnAddCoins.interactable = hasProgress;
        }
        #endregion
    }
}
