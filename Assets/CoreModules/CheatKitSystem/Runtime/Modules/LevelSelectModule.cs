using CoreModules.CheatKit.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace CoreModules.CheatKit.Modules
{
    /// <summary>
    /// Module điều hướng level — THUẦN PORT, không biết game nào.
    ///
    /// UI: InputField (1-based) + Go / Prev / Next / Reload + label info.
    /// Phụ thuộc <see cref="ILevelCheatService"/> để load; <see cref="IFlowCheatService"/> (tuỳ chọn)
    /// chỉ dùng để KHOÁ nút khi đang Loading/Win/Lose — tránh load chen vào cinematic/transition.
    ///
    /// MOBILE: không alloc trong Update (chỉ so int + set bool); không FindObjectOfType; chỉ ghi
    /// InputField khi user KHÔNG focus để không phá thao tác gõ.
    /// </summary>
    public class LevelSelectModule : UITestModuleBase, ICheatBindable, ICheatModuleUi
    {
        #region Serialized
        [Header("Level Navigation")]
        [SerializeField] private InputField _inputField;
        [SerializeField] private Button _btnGo;
        [SerializeField] private Button _btnPrev;
        [SerializeField] private Button _btnNext;
        [SerializeField] private Button _btnReload;
        [SerializeField] private Text _txtLevelInfo;
        #endregion

        #region State
        private ILevelCheatService _level;
        private IFlowCheatService _flow;   // optional
        private int _index;                // level đang chọn (0-based)
        private bool _bound;
        #endregion

        protected override void OnInitialize()
        {
            _moduleName = "Level Select";

            if (_inputField != null)
            {
                _inputField.contentType = InputField.ContentType.IntegerNumber;
                _inputField.onEndEdit.AddListener(OnEndEdit);
            }
            if (_btnGo != null) _btnGo.onClick.AddListener(OnGo);
            if (_btnPrev != null) _btnPrev.onClick.AddListener(OnPrev);
            if (_btnNext != null) _btnNext.onClick.AddListener(OnNext);
            if (_btnReload != null) _btnReload.onClick.AddListener(OnReload);

            RefreshUI();
        }

        /// <summary>No-prefab path: dựng control + gán field nội bộ (Builder gọi trước Initialize).</summary>
        public void BuildDefaultUI(RectTransform section)
        {
            CheatUi.Label(section, "▼ LEVEL SELECT", 26);
            _txtLevelInfo = CheatUi.Label(section, "Level —", 24);

            var nav = CheatUi.Row(section);
            _btnPrev = CheatUi.Button(nav, "◀", new Color(0.30f, 0.50f, 0.80f));
            _inputField = CheatUi.Input(nav, "lv");
            _btnNext = CheatUi.Button(nav, "▶", new Color(0.30f, 0.50f, 0.80f));
            _btnGo = CheatUi.Button(nav, "Go", new Color(0.30f, 0.70f, 0.40f));

            _btnReload = CheatUi.Button(section, "Reload", new Color(0.50f, 0.50f, 0.55f));
        }

        public void Bind(CheatServices services)
        {
            _level = services?.Level;
            _flow = services?.Flow;
            _bound = _level != null;

            if (_level != null && _level.IsReady && _level.CurrentIndex >= 0)
                _index = _level.CurrentIndex;

            RefreshUI();
        }

        protected override void OnCleanup()
        {
            if (_inputField != null) _inputField.onEndEdit.RemoveListener(OnEndEdit);
            if (_btnGo != null) _btnGo.onClick.RemoveListener(OnGo);
            if (_btnPrev != null) _btnPrev.onClick.RemoveListener(OnPrev);
            if (_btnNext != null) _btnNext.onClick.RemoveListener(OnNext);
            if (_btnReload != null) _btnReload.onClick.RemoveListener(OnReload);
        }

        /// <summary>Đồng bộ khi game tự đổi level (vd next-level sau win). Zero-alloc.</summary>
        public override void OnUpdate()
        {
            if (!_bound || !_level.IsReady) return;

            int cur = _level.CurrentIndex;
            if (cur >= 0 && cur != _index)
            {
                _index = cur;
                RefreshUI();
            }
            else
            {
                ApplyInteractable(); // phase có thể đổi mỗi frame
            }
        }

        #region Callbacks
        private void OnEndEdit(string text)
        {
            // Chỉ load khi nhấn Enter (mobile: nút "done" cũng raise submit).
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                OnGo();
        }

        private void OnGo()
        {
            if (!_bound || _inputField == null) return;
            if (int.TryParse(_inputField.text, out int oneBased))
            {
                _index = ClampIndex(oneBased - 1);
                LoadCurrent();
            }
            else
            {
                RefreshUI(); // reset text về giá trị hợp lệ
            }
        }

        private void OnPrev()
        {
            if (!_bound) return;
            _index = ClampIndex(_index - 1);
            LoadCurrent();
        }

        private void OnNext()
        {
            if (!_bound) return;
            _index = ClampIndex(_index + 1);
            LoadCurrent();
        }

        private void OnReload()
        {
            if (!_bound) return;
            _level.ReloadCurrent();
            RefreshUI();
        }
        #endregion

        #region Core
        private void LoadCurrent()
        {
            _level.Load(_index);
            RefreshUI();
        }

        /// <summary>Clamp [0, Count-1]; Count==0 (vô hạn) → chỉ chặn dưới 0.</summary>
        private int ClampIndex(int idx)
        {
            if (idx < 0) return 0;
            int count = _level != null ? _level.Count : 0;
            if (count > 0 && idx > count - 1) return count - 1;
            return idx;
        }

        private void RefreshUI()
        {
            if (_inputField != null && !_inputField.isFocused)
                _inputField.text = (_index + 1).ToString();

            if (_txtLevelInfo != null)
            {
                if (!_bound)
                {
                    _txtLevelInfo.text = "Level — (chưa sẵn sàng)";
                }
                else
                {
                    int count = _level.Count;
                    string name = _level.NameOf(_index);
                    string suffix = string.IsNullOrEmpty(name) ? "" : $" — {name}";
                    _txtLevelInfo.text = count > 0
                        ? $"Level {_index + 1} / {count}{suffix}"
                        : $"Level {_index + 1}{suffix}";
                }
            }

            ApplyInteractable();
        }

        /// <summary>Khoá nút khi đang Loading/Win/Lose (nếu adapter cung cấp phase).</summary>
        private void ApplyInteractable()
        {
            bool locked = _flow != null &&
                          (_flow.Phase == CheatGamePhase.Loading ||
                           _flow.Phase == CheatGamePhase.Win ||
                           _flow.Phase == CheatGamePhase.Lose);
            bool on = _bound && !locked;

            if (_btnGo != null) _btnGo.interactable = on;
            if (_btnPrev != null) _btnPrev.interactable = on;
            if (_btnNext != null) _btnNext.interactable = on;
            if (_btnReload != null) _btnReload.interactable = on;
            if (_inputField != null) _inputField.interactable = on;
        }
        #endregion
    }
}
