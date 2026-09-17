using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Board;
using DG.Tweening;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.UI.Views
{
    /// Hướng dẫn người chơi mới qua ba nhịp chọn màu và tô.
    public class TutorialOverlayView : MonoBehaviour
    {
        [Tooltip("Object chứa ngón tay và bảng nhắc.")]
        [SerializeField] private GameObject _content;

        [Tooltip("Ảnh bàn tay của nhịp 1.")]
        [SerializeField] private RectTransform _finger;

        [Tooltip("Ảnh bàn tay của nhịp 2.")]
        [SerializeField] private RectTransform _dragFinger;

        [Tooltip("Độ lệch ngón tay khỏi tâm ô ở nhịp 2.")]
        [SerializeField] private Vector2 _dragFingerOffset = new(0f, -60f);

        [Tooltip("Độ lệch ngón tay khỏi tâm ô màu ở nhịp 1, tính bằng pixel canvas.")]
        [SerializeField] private Vector2 _fingerOffset = new(0f, -60f);

        [Tooltip("Camera đang vẽ bàn chơi.")]
        [SerializeField] private Camera _worldCamera;

        [Header("Nhịp 2 — kéo qua các ô gợi ý")]
        [Tooltip("Cạnh của vòng ngón tay đi ở nhịp 2, tính bằng ô.")]
        [Range(1, 4)]
        [SerializeField] private int _loopCells = 2;

        [Tooltip("Thời gian chờ camera bay xong rồi mới hiện ngón tay ở nhịp 2.")]
        [SerializeField] private float _paintStageDelay = 0.85f;

        [Tooltip("Thời gian đi trọn một vòng, tính bằng giây.")]
        [SerializeField] private float _loopSeconds = 2.2f;

        [Header("Nhịp 3 — mời chọn màu tiếp")]
        [Tooltip("Thời gian chờ sau khi ô màu biến mất rồi mới thu camera ở nhịp 3.")]
        [SerializeField] private float _finalStageDelay = 0.6f;

        [Tooltip("Thời gian camera thu về toàn cảnh.")]
        [SerializeField] private float _finalZoomSeconds = 0.5f;

        [Header("Điều kiện hiện")]
        [Tooltip("Màn nào thì hiện hướng dẫn.")]
        [SerializeField] private int _tutorialLevelId = 1;

        [Tooltip("Thời gian chờ sau khi vào màn rồi mới hiện hướng dẫn.")]
        [SerializeField] private float _delaySeconds = 0.6f;

        [Header("Nhịp gõ của ngón tay")]
        [Tooltip("Độ cao ngón tay xuất phát so với ô màu, tính bằng pixel.")]
        [SerializeField] private float _tapTravel = 80f;

        [Tooltip("Thời gian một nhịp gõ: hạ xuống, dừng rồi nhấc lên.")]
        [SerializeField] private float _tapSeconds = 1.1f;

        [Tooltip("Tỉ lệ thời gian hạ xuống trong một nhịp gõ.")]
        [Range(0.1f, 0.8f)]
        [SerializeField] private float _tapDownPortion = 0.35f;

        [Tooltip("Tỉ lệ thời gian dừng ở đáy trong một nhịp gõ.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _tapHoldPortion = 0.18f;

        private ILevelService _levelService;
        private IPaintService _paintService;
        private ColorPaletteBar _paletteBar;
        private TutorialState _tutorialState;
        private IHintService _hintService;
        private BoardView _boardView;
        private BoardCamera _boardCamera;

        private bool _isShowing;
        private bool _isDisabled;

        private int _awaitedColor = NoColor;

        private const int NoColor = -1;

        private bool _pendingShow;

        public void Init(
            ILevelService levelService,
            IPaintService paintService,
            ColorPaletteBar paletteBar,
            TutorialState tutorialState,
            IHintService hintService,
            BoardView boardView,
            BoardCamera boardCamera)
        {
            _levelService = levelService;
            _paintService = paintService;
            _paletteBar = paletteBar;
            _tutorialState = tutorialState;
            _hintService = hintService;
            _boardView = boardView;
            _boardCamera = boardCamera;

            if (_content == null || _content == gameObject)
            {
                _isDisabled = true;

                Debug.LogError($"{nameof(TutorialOverlayView)}: ô Content phải trỏ tới một " +
                               "object CON chứa ngón tay và bảng nhắc. Bỏ trống hoặc trỏ về " +
                               "chính object này thì việc tắt hướng dẫn sẽ huỷ luôn coroutine " +
                               "của chính nó. Hướng dẫn bị tắt.", this);
                return;
            }

            if (_paletteBar != null) _paletteBar.OnSwatchRemoved += HandleSwatchRemoved;

            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnCellPainted += HandleCellPainted;

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_paletteBar != null) _paletteBar.OnSwatchRemoved -= HandleSwatchRemoved;

            if (_paintService != null)
            {
                _paintService.OnBoardReady -= HandleBoardReady;
                _paintService.OnColorSelected -= HandleColorSelected;
                _paintService.OnCellPainted -= HandleCellPainted;
            }
        }

        private void HandleBoardReady()
        {
            Hide();

            _awaitedColor = NoColor;

            if (_isDisabled) return;

            var loadedLevel = LoadedLevelId();
            var untouched = _paintService.IsUntouched;
            var experienced = IsExperienced();

            if (loadedLevel != _tutorialLevelId) return;
            if (!untouched) return;
            if (experienced) return;

            _pendingShow = true;
            TryStartShow();
        }

        /// Bắt đầu hiện hướng dẫn, hoãn lại nếu object đang tắt.
        private void TryStartShow()
        {
            if (!_pendingShow) return;

            if (!gameObject.activeInHierarchy) return;

            _pendingShow = false;
            StartCoroutine(ShowRoutine());
        }

        private void OnEnable() => TryStartShow();

        /// Id màn đang được nạp.
        private int LoadedLevelId()
        {
            var config = _levelService.CurrentConfig;

            return config != null ? config.LevelId : _levelService.CurrentLevel;
        }

        /// Người chơi đã biết tô rồi hay chưa.
        private bool IsExperienced()
        {
            if (_tutorialState != null && _tutorialState.HasPaintedOnce) return true;

            return _levelService != null && _levelService.IsCompleted(_tutorialLevelId);
        }

        /// Ghi nhận ô vừa tô và kết thúc nhịp 2.
        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            _tutorialState?.MarkPainted();

            if (_tutorialState == null || _tutorialState.Stage != TutorialStage.PaintCells) return;

            _awaitedColor = paletteIndex;

            Hide();
        }

        /// Chuyển sang nhịp 3 khi màu đang chờ được tô xong.
        private void HandleSwatchRemoved(int paletteIndex)
        {
            if (_awaitedColor == NoColor || paletteIndex != _awaitedColor) return;

            _awaitedColor = NoColor;

            StartCoroutine(FinalStageRoutine());
        }

        /// Nhịp 3: thu camera về toàn cảnh rồi mời chọn màu tiếp.
        private IEnumerator FinalStageRoutine()
        {
            if (_finalStageDelay > 0f) yield return new WaitForSeconds(_finalStageDelay);

            _boardCamera?.ResetFraming(Mathf.Max(0f, _finalZoomSeconds));

            if (_finalZoomSeconds > 0f) yield return new WaitForSeconds(_finalZoomSeconds);

            var target = _paletteBar != null ? _paletteBar.TutorialSwatchRect : null;

            if (target == null) yield break;

            SetVisible(true);

            _tutorialState?.SetStage(TutorialStage.PickNextColor);

            var finger = UseFinger(forDragStage: false);
            if (finger == null) yield break;

            finger.position = target.position;
            finger.anchoredPosition += _fingerOffset;

            yield return TapRoutine(finger, finger.anchoredPosition);
        }

        /// Kết thúc nhịp chọn màu và chuyển sang nhịp 2.
        private void HandleColorSelected(int paletteIndex)
        {
            if (!_isShowing) return;

            if (_tutorialState != null && _tutorialState.Stage == TutorialStage.PickNextColor)
            {
                Hide();
                return;
            }

            Hide();

            if (_hintService == null || !_hintService.FocusHintWithoutSpending(out var cell))
            {
                return;
            }

            StartCoroutine(PaintStageRoutine(cell));
        }

        /// Nhịp 2: ngón tay hiện lại trên bàn chơi và kéo qua mấy ô đang chờ tô.
        private IEnumerator PaintStageRoutine(Vector2Int startCell)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, _paintStageDelay));

            SetVisible(true);

            var finger = UseFinger(forDragStage: true);

            var path = finger != null ? BuildLoopPath(finger, startCell) : null;

            if (path == null || path.Count < 2)
            {
                Hide();
                yield break;
            }

            _tutorialState?.SetStage(TutorialStage.PaintCells);

            yield return LoopRoutine(finger, path);
        }

        /// Dựng đường vòng khép kín cho ngón tay quanh ô bắt đầu.
        private List<Vector2> BuildLoopPath(RectTransform finger, Vector2Int startCell)
        {
            if (finger == null || _boardView == null) return null;

            var layout = _boardView.Layout;
            if (layout == null) return null;

            var camera = _worldCamera != null ? _worldCamera : Camera.main;
            if (camera == null) return null;

            var parent = finger.parent as RectTransform;
            if (parent == null) return null;

            var canvas = finger.GetComponentInParent<Canvas>();

            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!TryCellToCanvas(layout, camera, parent, uiCamera, startCell.x, startCell.y, out var origin)
                || !TryCellToCanvas(layout, camera, parent, uiCamera, startCell.x + 1, startCell.y, out var right)
                || !TryCellToCanvas(layout, camera, parent, uiCamera, startCell.x, startCell.y + 1, out var down))
            {
                return null;
            }

            var stepRight = (right - origin) * _loopCells;
            var stepDown = (down - origin) * _loopCells;

            var p0 = origin + _dragFingerOffset;

            return new List<Vector2>
            {
                p0,
                p0 + stepDown,
                p0 + stepDown + stepRight,
                p0 + stepRight,
                p0,
            };
        }

        private static bool TryCellToCanvas(BoardLayout layout, Camera camera, RectTransform parent,
            Camera uiCamera, int x, int y, out Vector2 local)
        {
            var world = layout.CellToWorldCenter(x, y);
            var screen = camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screen, uiCamera, out local);
        }

        /// Cho ngón tay đi vòng lặp lại tới khi hướng dẫn tắt.
        private IEnumerator LoopRoutine(RectTransform finger, List<Vector2> path)
        {
            var lap = Mathf.Max(0.1f, _loopSeconds);

            finger.anchoredPosition = path[0];

            var elapsed = 0f;

            while (_isShowing)
            {
                elapsed += Time.unscaledDeltaTime;

                while (elapsed >= lap) elapsed -= lap;

                finger.anchoredPosition = SampleAlong(path, elapsed / lap);

                yield return null;
            }
        }

        /// Điểm trên đường gấp khúc ở tỉ lệ t (0..1), chia đều theo số đoạn.
        private static Vector2 SampleAlong(List<Vector2> path, float t)
        {
            var segments = path.Count - 1;
            if (segments <= 0) return path[0];

            var scaled = t * segments;
            var index = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);

            return Vector2.Lerp(path[index], path[index + 1], scaled - index);
        }

        /// Bật đúng một ngón tay và tắt cái còn lại, rồi trả về cái vừa bật.
        private RectTransform UseFinger(bool forDragStage)
        {
            var drag = _dragFinger != null ? _dragFinger : _finger;
            var active = forDragStage ? drag : _finger;

            if (_finger != null && _finger != active) _finger.gameObject.SetActive(false);
            if (_dragFinger != null && _dragFinger != active) _dragFinger.gameObject.SetActive(false);

            if (active != null && !active.gameObject.activeSelf) active.gameObject.SetActive(true);

            return active;
        }

        private IEnumerator ShowRoutine()
        {
            if (_delaySeconds > 0f) yield return new WaitForSeconds(_delaySeconds);

            var target = _paletteBar != null ? _paletteBar.TutorialSwatchRect : null;

            if (target == null)
            {
                Debug.LogWarning($"{nameof(TutorialOverlayView)}: thanh màu không có đủ ô cho " +
                                 "ô Tutorial Swatch Order bên ColorPaletteBar, nên không biết " +
                                 "chỉ ngón tay vào đâu. Bỏ qua hướng dẫn.", this);
                yield break;
            }

            SetVisible(true);

            _tutorialState?.SetStage(TutorialStage.PickColor);

            var finger = UseFinger(forDragStage: false);

            if (finger == null)
            {
                Debug.LogWarning($"{nameof(TutorialOverlayView)}: ô Finger còn trống nên ngón " +
                                 "tay không gõ. Kéo RectTransform của ảnh bàn tay vào ô đó.", this);
                yield break;
            }

            finger.position = target.position;
            finger.anchoredPosition += _fingerOffset;

            StartCoroutine(TapRoutine(finger, finger.anchoredPosition));
        }

        /// Ngón tay hạ từ trên xuống chạm ô màu, dừng một nhịp, rồi nhấc lên và lặp lại.
        private IEnumerator TapRoutine(RectTransform finger, Vector2 restPosition)
        {
            if (finger == null || _tapSeconds <= 0f || _tapTravel <= 0f) yield break;

            var down = Mathf.Clamp01(_tapDownPortion);
            var hold = Mathf.Clamp01(_tapHoldPortion);
            var upStart = Mathf.Min(0.99f, down + hold);

            while (_isShowing)
            {
                var elapsed = 0f;

                while (elapsed < _tapSeconds && _isShowing)
                {
                    elapsed += Time.unscaledDeltaTime;

                    var t = Mathf.Clamp01(elapsed / _tapSeconds);
                    float lift;

                    if (t < down)
                    {
                        lift = 1f - DOVirtual.EasedValue(0f, 1f, t / down, Ease.OutQuad);
                    }
                    else if (t < upStart)
                    {
                        lift = 0f;
                    }
                    else
                    {
                        lift = DOVirtual.EasedValue(0f, 1f, (t - upStart) / (1f - upStart), Ease.InOutSine);
                    }

                    finger.anchoredPosition = restPosition + Vector2.up * (_tapTravel * lift);
                    yield return null;
                }
            }

            finger.anchoredPosition = restPosition;
        }

        private void Hide()
        {
            StopAllCoroutines();
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            var changed = _isShowing != visible;

            _isShowing = visible;

            if (!visible) _tutorialState?.SetStage(TutorialStage.None);

            if (_content != null && _content.activeSelf != visible) _content.SetActive(visible);

            _ = changed;
        }
    }
}
