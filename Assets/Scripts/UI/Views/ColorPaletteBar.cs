using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    /// Thanh chọn màu dưới màn hình. Chỉ hiện những màu ảnh thật sự dùng,
    /// kèm số ô còn lại của mỗi màu.
    ///
    /// Nằm trên Canvas nên không bị zoom và kéo theo bảng.
    ///
    /// Tự ẩn khi thắng màn và hiện lại khi màn mới dựng xong — cùng khuôn với HudView, và
    /// cùng lý do: popup thắng màn phải đứng một mình trên bức tranh vừa hoàn thành. Thanh
    /// màu ở lại thì nó vừa che mất mép dưới bức tranh, vừa mời người chơi bấm vào những
    /// ô màu đã không còn ô nào để tô.
    public class ColorPaletteBar : MonoBehaviour, IPaintOriginProvider
    {
        [SerializeField] private ColorSwatchView _swatchPrefab;
        [SerializeField] private Transform _root;

        [Tooltip("Camera của bảng. Cần để đổi vị trí ô màu trên màn hình sang world " +
                 "cho hiệu ứng ngọc bay.")]
        [SerializeField] private Camera _worldCamera;

        [Tooltip("Scroll Rect của thanh màu. Có gán thì mỗi lần vào màn nó tự cuộn về ô " +
                 "màu ĐẦU TIÊN.\n\n" +
                 "Cần thiết vì vị trí cuộn không tự reset: kéo thanh sang phải ở màn này " +
                 "thì vào màn sau nó vẫn nằm nguyên chỗ đó, dù danh sách màu đã khác hẳn.")]
        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("Object bị ẩn khi thắng màn — kéo cả CANVAS của thanh màu vào đây " +
                 "(Canvas-Palette), không phải object mang script này.\n\n" +
                 "Script này sống trên Content bên trong Scroll Rect. Ẩn chính nó thì " +
                 "Scroll Rect mất phần nội dung mà vẫn còn khung, và cái khung rỗng đó " +
                 "vẫn nằm đè lên mép dưới bức tranh.\n\n" +
                 "Để trống thì ẩn chính object này — vẫn chạy, chỉ là xấu.")]
        [SerializeField] private GameObject _content;

        [Tooltip("CanvasGroup để thanh màu MỜ DẦN đi lúc màn ăn mừng bắt đầu, thay vì tắt " +
                 "phụt. Gán CanvasGroup đặt trên chính object ở ô trên. Để trống thì tắt " +
                 "phụt như cũ.\n\n" +
                 "Chỉ dùng cho đường ra của màn ăn mừng. Mọi chỗ ẩn thanh màu khác vẫn tắt " +
                 "ngay, vì ở đó có màn hình khác phủ lên ngay lập tức.")]
        [SerializeField] private CanvasGroup _celebrationFadeGroup;

        [Tooltip("Thời gian thanh màu mờ đi. Nên NGẮN hơn Sweep Start Delay của " +
                 "WinCelebration, để nó đi hẳn trước khi dải quét chạy tới.")]
        [SerializeField] private float _celebrationFadeDuration = 0.25f;

        [Header("Cuộn tới ô màu vừa chọn")]
        [Tooltip("Thời gian cuộn thanh màu tới ô màu vừa được chọn. Để 0 là nhảy tới " +
                 "ngay không có chuyển động.")]
        [SerializeField] private float _focusScrollDuration = 0.22f;

        [Tooltip("Thời gian KHÉP khe hở sau khi một màu tô xong — mấy ô còn lại trượt vào " +
                 "chỗ trống trong ngần này giây.\n\n" +
                 "Để 0 là đóng phựt như bản cũ. Với Spacing 140 của thanh hiện tại thì cú " +
                 "đóng đó dịch cả thanh đi 140 pixel trong một frame.")]
        [SerializeField] private float _collapseDuration = 0.3f;


        private readonly List<ColorSwatchView> _swatches = new();

        /// Giữ tay nắm của TỪNG coroutine thay vì gọi StopAllCoroutines.
        ///
        /// StopAllCoroutines cắt cả những coroutine mà nó không có ý cắt, và ở đây có
        /// đúng hai cái chạy trên cùng một Scroll Rect: sắp lại thanh, và cuộn tới ô màu.
        /// Gọi chung một nhát thì thêm coroutine thứ ba vào lớp này là lặng lẽ hỏng một
        /// tính năng khác, không có lấy một dòng lỗi.
        private Coroutine _relayout;
        private Coroutine _focusScroll;

        private IPaintService _paintService;
        private ILevelService _levelService;
        private ILevelFlowService _levelFlow;

        /// Lượt mờ đi của màn ăn mừng đang chạy.
        private Tween _celebrationFade;
        private JewelFlyEffect _flyEffect;
        private ISoundService _sound;

        [Tooltip("Màn hướng dẫn dạy ô màu THỨ MẤY trên thanh, đếm từ 0 trong số những ô " +
                 "đang hiện.\n\n" +
                 "0 = ô ngoài cùng bên trái, 1 = ô kế tiếp, và cứ thế.\n\n" +
                 "Ngón tay chỉ vào ô này, và trong lúc hướng dẫn cũng chỉ mình ô này bấm " +
                 "được. MỘT con số cho cả hai việc — tách làm hai thì sớm muộn ngón tay " +
                 "chỉ một ô còn ô bấm được lại là ô khác.\n\n" +
                 "Thanh ít màu hơn số này thì khoá tự gỡ: thà hướng dẫn hơi lỏng còn hơn " +
                 "một màn không ai qua được.")]
        [Min(0)]
        [SerializeField] private int _tutorialSwatchOrder = 1;

        /// Hướng dẫn có đang chạy không. Để trống thì thanh màu chạy bình thường mọi lúc.
        private TutorialState _tutorialState;

        /// Đang xử lý cú chạm vào một ô màu trên thanh. Xem HandleSwatchClicked.
        private bool _selectingFromSwatch;

        /// Màu đã diễn xong màn "tô hết màu" rồi thì thôi.
        ///
        /// Cần chốt lại vì lúc ô cuối của một màu được tô, vài viên ngọc cùng màu vẫn
        /// đang bay — mỗi viên đáp xuống lại thấy RemainingFor = 0 và đòi diễn thêm một
        /// lần nữa. Cùng lý do đã ghi ở ColorCompleteSparkle.
        private readonly HashSet<int> _completed = new();

        /// flyEffect được phép null — để trống thì ô màu tắt ngay lúc tô xong như bản cũ,
        /// nghĩa là tắt trong khi mấy viên cuối còn đang bay.
        public void Init(IPaintService paintService, ILevelService levelService, ILevelFlowService levelFlow,
            JewelFlyEffect flyEffect, ISoundService sound, TutorialState tutorialState)
        {
            _paintService = paintService;
            _levelService = levelService;
            _levelFlow = levelFlow;
            _flyEffect = flyEffect;
            _sound = sound;
            _tutorialState = tutorialState;

            // Nghe để bật lại trục cuộn đúng lúc hướng dẫn kết thúc. Không có nó thì khoá
            // nằm lại cho tới lần dựng bố cục kế tiếp — mà lần đó chỉ tới khi một màu được
            // tô xong, tức là người chơi mất quyền kéo thanh suốt cả màu đầu tiên.
            if (_tutorialState != null) _tutorialState.OnStageChanged += HandleTutorialStageChanged;

            // Nghe lúc ĐÁP chứ không phải lúc bấm: viên ngọc cuối cùng phải nằm vào tranh
            // rồi ô màu mới được thu lại. Nghe OnCellPainted thì ô biến mất trong khi vài
            // viên còn đang bay ra từ chính nó.
            if (_flyEffect != null) _flyEffect.OnJewelLanded += HandleJewelLanded;

            _paintService.OnBoardReady += HandleBoardReady;
            _paintService.OnCellPainted += HandleCellPainted;
            _paintService.OnColorSelected += HandleColorSelected;
            _paintService.OnColorFocusRequested += HandleColorFocusRequested;
            _paintService.OnFreePaintChanged += HandleFreePaintChanged;

            if (_levelFlow != null)
            {
                _levelFlow.OnCelebrationStarted += HandleCelebrationStarted;
                _levelFlow.OnLevelCleared += HandleLevelCleared;
            }
        }

        private void OnDestroy()
        {
            if (_tutorialState != null) _tutorialState.OnStageChanged -= HandleTutorialStageChanged;

            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;

            KillCelebrationFade();

            if (_levelFlow != null)
            {
                _levelFlow.OnCelebrationStarted -= HandleCelebrationStarted;
                _levelFlow.OnLevelCleared -= HandleLevelCleared;
            }

            if (_paintService == null) return;

            _paintService.OnBoardReady -= HandleBoardReady;
            _paintService.OnCellPainted -= HandleCellPainted;
            _paintService.OnColorSelected -= HandleColorSelected;
            _paintService.OnColorFocusRequested -= HandleColorFocusRequested;
            _paintService.OnFreePaintChanged -= HandleFreePaintChanged;
        }

        /// Màn ăn mừng bắt đầu — thanh màu dọn đi ngay, không đợi popup.
        ///
        /// Cùng khuôn và cùng lý do với HudView: từ đây tới lúc popup mở là gần hai giây
        /// dải quét và đóng khung, mà thanh màu thì nằm đè đúng mép dưới bức tranh.
        private void HandleCelebrationStarted()
        {
            var target = _content != null ? _content : gameObject;
            if (!target.activeSelf) return;

            if (_celebrationFadeGroup == null || _celebrationFadeDuration <= 0f)
            {
                SetVisible(false);
                return;
            }

            KillCelebrationFade();

            // Khoá chạm NGAY, không đợi mờ xong: suốt quãng đang tan, thanh màu chỉ còn
            // là hình ảnh.
            _celebrationFadeGroup.interactable = false;
            _celebrationFadeGroup.blocksRaycasts = false;

            // DOVirtual.Float chứ KHÔNG phải CanvasGroup.DOFade — xem chú thích cùng chỗ
            // ở PopupView.
            _celebrationFade = DOVirtual
                .Float(_celebrationFadeGroup.alpha, 0f, _celebrationFadeDuration, value =>
                {
                    if (_celebrationFadeGroup != null) _celebrationFadeGroup.alpha = value;
                })
                .SetUpdate(true)
                .OnComplete(() => SetVisible(false));
        }

        private void HandleLevelCleared() => SetVisible(false);

        /// Trả CanvasGroup về trạng thái hiện đủ. Thiếu nó thì thanh màu của màn sau bật
        /// lên với alpha vẫn đang là 0.
        private void RestoreCelebrationFade()
        {
            KillCelebrationFade();

            if (_celebrationFadeGroup == null) return;

            _celebrationFadeGroup.alpha = 1f;
            _celebrationFadeGroup.interactable = true;
            _celebrationFadeGroup.blocksRaycasts = true;
        }

        private void KillCelebrationFade()
        {
            if (_celebrationFade != null && _celebrationFade.IsActive()) _celebrationFade.Kill();

            _celebrationFade = null;
        }

        /// Ẩn bằng SetActive chứ không đổi alpha: thanh tắt hẳn thì các ô màu cũng không
        /// còn nhận được cú chạm nào, khỏi phải nhớ khoá riêng từng ô.
        ///
        /// Ẩn một object CHA vẫn an toàn dù script nằm bên trong nó: sự kiện C# giữ tham
        /// chiếu tới instance, nên handler vẫn chạy khi GameObject đang tắt — đó chính là
        /// cách thanh màu tự bật lại được ở màn sau.
        private void SetVisible(bool visible)
        {
            // Lượt mờ đang chạy dở mà không giết thì cái OnComplete của nó tắt luôn thanh
            // màu vừa được bật lên cho màn sau.
            if (visible) RestoreCelebrationFade();
            else KillCelebrationFade();

            var target = _content != null ? _content : gameObject;

            if (target.activeSelf != visible) target.SetActive(visible);
        }

        private void HandleBoardReady()
        {
            // Bật lại TRƯỚC mọi thứ khác, không phải ở cuối hàm. RelayoutBar bỏ qua khi
            // isActiveAndEnabled là false, nên còn tắt ở dòng này là màn kế tiếp mở ra với
            // thanh màu nằm nguyên chỗ cuộn của màn trước — và vì nó vẫn dựng đủ ô nên
            // nhìn qua chẳng có gì sai để mà lần.
            //
            // Màn mở ra đã tô kín sẵn thì không bật: đó là lượt XEM LẠI bức tranh đã xong,
            // mọi màu đều hết ô nên thanh sẽ hiện ra như một dải trống nằm đè lên mép dưới
            // tranh. Bấm nút Tô lại là bảng trắng trở lại và thanh hiện ngay ở lượt nạp sau.
            SetVisible(!_paintService.IsComplete);

            _completed.Clear();

            HideAll();

            if (_swatchPrefab == null)
            {
                Debug.LogWarning($"{nameof(ColorPaletteBar)} chưa gán Swatch Prefab — thanh màu sẽ trống.");
                return;
            }

            var data = _levelService.CurrentGrid;
            if (data == null) return;

            // Màu NGỌC chứ không phải màu đất: ô trên thanh phải trông đúng bằng thứ
            // người chơi sắp đặt xuống bảng, nhất là khi hiệu ứng bay xuất phát từ chính
            // ô này.
            var colors = _levelService.CurrentJewelColors;
            var used = _paintService.UsedPaletteIndices;

            // Đếm slot RIÊNG, không dùng chỉ số của vòng lặp.
            //
            // Dùng chỉ số vòng lặp thì mỗi màu bị bỏ qua để lại một slot trống ở giữa,
            // mà GetSwatch tạo ô mới theo slot — nên nó vẫn phải sinh ra những ô đệm
            // cho các slot bị nhảy cóc. Chúng chưa Bind bao giờ nhưng vẫn hiện, và
            // người chơi thấy vài ô màu trắng trơn nằm đầu thanh.
            //
            // Trước khi có tính năng lưu thì lỗi này không lộ, vì lúc vào màn chưa màu
            // nào xong sẵn để mà bị bỏ qua.
            var slot = 0;

            foreach (var paletteIndex in used)
            {
                if (paletteIndex < 0 || paletteIndex >= colors.Count) continue;

                var remaining = _paintService.RemainingFor(paletteIndex);
                if (remaining <= 0) continue;   // màu đã xong sẵn thì không dựng ô nào

                var swatch = GetSwatch(slot++);
                swatch.Bind(paletteIndex, colors[paletteIndex], HandleSwatchClicked);
                swatch.SetRemaining(remaining);
                swatch.SetProgress(_paintService.ProgressFor(paletteIndex));
                swatch.SetSelected(false);
                swatch.gameObject.SetActive(true);
            }

            // Đặt lại tư thế nhô/hạ theo booster. Gần như luôn là false ở đây vì màn mới
            // nạp thì booster đã tắt, nhưng đọc trạng thái thật vẫn hơn là tin vào điều đó.
            SetAllRaised(_paintService.FreePaintActive);

            RelayoutBar();
        }

        /// Booster tô tự do bật nghĩa là MỌI màu đều tô được, nên mọi viên ngọc cùng được
        /// nhấc lên — vẫn đúng ngôn ngữ hình mà ô đang chọn vẫn dùng, chỉ khác là lần này
        /// nó nói "tất cả" thay vì "cái này".
        private void HandleFreePaintChanged(bool active) => SetAllRaised(active);

        /// Gọi lên CẢ những ô đang ẩn. Chúng không hiện nên không tốn gì, mà bỏ qua thì
        /// một ô được bật lại sau đó sẽ nằm sai tư thế.
        private void SetAllRaised(bool raised)
        {
            foreach (var swatch in _swatches)
            {
                if (swatch == null) continue;

                swatch.SetRaised(raised);
            }
        }

        /// Sắp lại thanh: còn nhiều màu thì cuộn về ô đầu, còn ít màu thì căn giữa.
        ///
        /// Phải đợi HẾT MỘT FRAME. Content Size Fitter tính lại bề rộng của thanh ở cuối
        /// frame, và ngay sau đó Scroll Rect kẹp lại vị trí theo bề rộng mới. Đo hay đặt
        /// trong cùng frame với lúc dựng các ô đều là làm xong bị ghi đè.
        private void RelayoutBar()
        {
            if (_scrollRect == null || !isActiveAndEnabled) return;

            // Cắt luôn cú cuộn tới ô màu nếu có: thanh sắp đổi bố cục nên cái đích mà
            // nó đang nhắm tới sắp không còn đúng nữa.
            if (_focusScroll != null) StopCoroutine(_focusScroll);
            if (_relayout != null) StopCoroutine(_relayout);

            _relayout = StartCoroutine(RelayoutBarRoutine());
        }

        private IEnumerator RelayoutBarRoutine()
        {
            yield return null;

            Canvas.ForceUpdateCanvases();

            ApplyBarAlignment(scrollToStart: true);
        }

        /// Đặt thanh về đúng chỗ NGAY LẬP TỨC theo bề rộng hiện tại của nó.
        ///
        /// Tách ra khỏi coroutine để cú khép khe hở gọi được mỗi frame. Chính chỗ này là
        /// nguyên nhân của cú giật: trước đây nó chỉ chạy MỘT lần ở cuối, nên suốt 0.3
        /// giây thanh trượt theo bố cục cũ rồi đến frame chót mới bị đặt lại một phát.
        ///
        /// scrollToStart chỉ đúng khi DỰNG LẠI thanh (vào màn mới): lúc đó vị trí cuộn cũ
        /// không còn nghĩa gì. Trong lúc khép khe hở thì tuyệt đối không được đụng vào —
        /// người chơi có thể đang xem ở giữa thanh, và giật họ về mép trái vì một màu vừa
        /// xong là một cú giật khác hẳn, còn khó chịu hơn cú này.
        private void ApplyBarAlignment(bool scrollToStart)
        {
            if (_scrollRect == null) return;

            var content = _scrollRect.content;
            if (content == null) return;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            var fitsInViewport = content.rect.width <= viewport.rect.width;

            // TẮT cuộn ngang khi các ô đã vừa khung nhìn.
            //
            // Không phải để chặn thao tác, mà vì Scroll Rect luôn kéo content về nằm gọn
            // trong biên — và khi content NHỎ HƠN khung nhìn, phép kẹp đó dí nó vào một
            // mép. Căn giữa xong sẽ bị nó đẩy về lại ngay frame sau. Tắt trục ngang thì
            // phép kẹp không đụng tới trục đó nữa.
            // AND với khoá hướng dẫn: đang hướng dẫn thì trục ngang tắt hẳn, bất kể thanh
            // có dài hơn khung nhìn hay không. Không có vế này thì mỗi lần bố cục được
            // dựng lại là phép kéo mở lại, còn khoá thì lặng lẽ biến mất.
            _scrollRect.horizontal = !fitsInViewport && !IsTutorialRunning;

            if (!fitsInViewport)
            {
                if (!scrollToStart) return;

                // Dừng đà quán tính. Cú kéo dở dang của màn trước còn trớn thì nó đẩy
                // thanh trôi tiếp ngay sau khi ta đặt xong.
                _scrollRect.velocity = Vector2.zero;
                _scrollRect.horizontalNormalizedPosition = 0f;   // 0 là mép TRÁI

                return;
            }

            // Dịch content sao cho MÉP TRÁI của nó trùng mép trái khung nhìn.
            //
            // Đo qua toạ độ thế giới rồi đổi về hệ của viewport, thay vì tính từ
            // anchoredPosition: cách này không phụ thuộc người dựng đặt Anchor và Pivot
            // của content ở đâu.
            var contentLeft = viewport.InverseTransformPoint(
                content.TransformPoint(new Vector3(content.rect.xMin, 0f, 0f))).x;

            var delta = viewport.rect.xMin - contentLeft;

            if (Mathf.Abs(delta) < 0.01f) return;

            _scrollRect.velocity = Vector2.zero;
            content.anchoredPosition += new Vector2(delta, 0f);
        }

        private void HandleCellPainted(Vector2Int cell, int paletteIndex)
        {
            var swatch = FindSwatch(paletteIndex);
            if (swatch == null) return;

            var remaining = _paintService.RemainingFor(paletteIndex);

            // Hết ô mà vẫn cập nhật số và vòng tiến độ như thường: số 0 hiện ra trong lúc
            // mấy viên cuối còn bay là đúng — người chơi đã bấm hết rồi thật.
            //
            // Việc gỡ ô khỏi thanh chuyển sang HandleJewelLanded. Không có hiệu ứng bay
            // thì ở đây làm nốt, vì lúc đó chẳng có cú đáp nào để mà đợi.
            swatch.SetRemaining(remaining);
            swatch.SetProgress(_paintService.ProgressFor(paletteIndex));

            if (remaining <= 0 && _flyEffect == null) CompleteSwatch(paletteIndex);
        }

        /// Viên ngọc vừa nằm vào tranh. Đây mới là lúc hỏi "màu này xong chưa".
        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_paintService.RemainingFor(paletteIndex) > 0) return;

            // Còn viên nào của màu này giữa trời thì chưa xong. JewelFlyEffect gỡ viên
            // khỏi sổ TRƯỚC khi bắn sự kiện này, nên câu hỏi trả lời đúng ngay ở viên
            // cuối cùng.
            if (_flyEffect.HasInFlight(paletteIndex)) return;

            CompleteSwatch(paletteIndex);
        }

        /// Ô màu thu nhỏ dần rồi biến mất, xong thì thanh sắp lại.
        private void CompleteSwatch(int paletteIndex)
        {
            if (!_completed.Add(paletteIndex)) return;

            var swatch = FindSwatch(paletteIndex);
            if (swatch == null) return;

            swatch.PlayComplete(() => StartCoroutine(CollapseRoutine(swatch, paletteIndex)));
        }

        /// Khép dần khe hở của ô vừa xong, rồi mới tắt nó và sắp lại thanh.
        ///
        /// Thu bề rộng về ÂM đúng bằng Spacing, không phải về 0.
        ///
        /// Horizontal Layout Group chừa spacing giữa MỌI cặp con đang bật, nên một ô rộng
        /// 0 vẫn ngốn 140 pixel khoảng hở. Dừng ở 0 thì lúc tắt ô đi, thanh vẫn giật đúng
        /// 140 pixel — chỉ là giật muộn hơn. Đi tới -spacing thì tổng bề rộng lúc đó đã
        /// bằng đúng tổng sau khi ô biến mất, và cú tắt không dịch một pixel nào.
        private IEnumerator CollapseRoutine(ColorSwatchView swatch, int paletteIndex)
        {
            var duration = Mathf.Max(0f, _collapseDuration);
            var from = swatch.LayoutBaseWidth;
            var to = -LayoutSpacing();

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                swatch.SetLayoutWidth(Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration)));

                // Ép layout tính lại NGAY rồi căn lại thanh trong CÙNG frame.
                //
                // Content Size Fitter chỉ chạy ở cuối frame, nên không ép thì phép căn ở
                // dòng dưới đọc bề rộng của frame trước — lệch một nhịp suốt cú khép, và
                // dồn hết vào frame chót thành một cú nảy.
                Canvas.ForceUpdateCanvases();

                ApplyBarAlignment(scrollToStart: false);

                yield return null;
            }

            swatch.gameObject.SetActive(false);

            // Trả bề rộng gốc NGAY sau khi tắt: ô này còn được dùng lại ở màn sau, mà
            // một ô mang bề rộng âm sẽ kéo lệch cả thanh ngay từ lúc dựng.
            swatch.SetLayoutWidth(from);

            // Chốt lại lần cuối. Không gọi RelayoutBar: hàm đó kéo thanh về mép trái, mà
            // đây không phải lúc dựng lại thanh — người chơi vẫn đang chơi dở.
            Canvas.ForceUpdateCanvases();

            ApplyBarAlignment(scrollToStart: false);

            // Bắn ở ĐÂY, sau cả cú loé mừng màu xong lẫn cú khép ô — đây mới là khoảnh
            // khắc "màu đó đã xong hẳn" theo nghĩa người chơi nhìn thấy.
            OnSwatchRemoved?.Invoke(paletteIndex);
        }

        /// Spacing của Horizontal Layout Group đang xếp các ô màu. 0 khi không có.
        private float LayoutSpacing()
        {
            if (_root == null) return 0f;

            var group = _root.GetComponent<HorizontalLayoutGroup>();

            return group != null ? group.spacing : 0f;
        }

        private void HandleColorSelected(int paletteIndex)
        {
            // Nghe OnColorSelected chứ không móc vào cú chạm ô màu: giữ tay vào tranh để
            // bắt màu cũng là một lần chọn ngọc, và nó đi vào đúng cửa này.
            //
            // Sự kiện chỉ bắn khi màu THẬT SỰ đổi, nên chạm lại đúng ô đang chọn không
            // kêu thêm tiếng nào — điều đó đúng, vì có gì đổi đâu.
            if (_sound != null) _sound.Play(SoundKey.ChooseJewel);

            foreach (var swatch in _swatches)
            {
                if (!swatch.gameObject.activeSelf) continue;

                swatch.SetSelected(swatch.PaletteIndex == paletteIndex);
            }
        }

        /// Chạm thẳng vào một ô màu thì KHÔNG cuộn thanh.
        ///
        /// Ô đó đang nằm ngay dưới ngón tay người chơi — nó hiển nhiên đang trong tầm
        /// nhìn, và dịch thanh ngay lúc vừa bấm là cách chắc chắn nhất để lần bấm kế tiếp
        /// trượt sang ô bên cạnh.
        ///
        /// Chặn bằng một cờ dựng lên quanh lời gọi chứ không thêm tham số vào IPaintService:
        /// SelectColor chạy đồng bộ, nên OnColorFocusRequested bắn ra NGAY BÊN TRONG hai
        /// dòng này. Cửa duy nhất còn cần cuộn là cú giữ tay trên tranh để bắt màu — và
        /// nó đi vào từ BoardInput, ngoài phạm vi cái cờ.
        private void HandleSwatchClicked(int paletteIndex)
        {
            // Đang hướng dẫn thì CHỈ ô màu đầu tiên được chọn — đúng ô ngón tay đang chỉ.
            //
            // Im lặng bỏ qua chứ không làm xám mấy ô kia: ô màu xám trên thanh đã có nghĩa
            // sẵn rồi (màu đã tô xong), và mượn lại cái nghĩa đó để nói "chưa tới lượt"
            // là dạy người mới một điều sai ngay ở màn dạy họ đọc thanh màu.
            if (IsTutorialRunning)
            {
                var allowed = TutorialSwatchPaletteIndex;

                // allowed < 0 nghĩa là thanh không có đủ ô cho thứ tự đã đặt. Lúc đó BỎ
                // khoá thay vì chặn hết: khoá một cái không tồn tại là khoá tất cả, và
                // người chơi kẹt lại ở màn hướng dẫn không có đường nào ra.
                if (allowed >= 0 && paletteIndex != allowed) return;
            }


            _selectingFromSwatch = true;

            try
            {
                _paintService.SelectColor(paletteIndex);
            }
            finally
            {
                // finally: người nghe OnColorSelected có thể ném, và một cái cờ kẹt ở true
                // sẽ tắt vĩnh viễn phép cuộn của cú giữ tay.
                _selectingFromSwatch = false;
            }
        }

        private void HandleColorFocusRequested(int paletteIndex)
        {
            if (_selectingFromSwatch) return;

            ScrollToSwatch(paletteIndex);
        }

        /// Cuộn thanh để ô màu lọt vào tầm nhìn — CHỈ khi nó đang nằm ngoài.
        ///
        /// Trên thực tế chỉ còn một đường gọi tới đây: giữ tay trên tranh để bắt màu của
        /// một ô. Đó đúng là lúc màu được chọn có thể nằm tít đầu kia thanh.
        ///
        /// Khi phải dịch thì đưa ô vào CHÍNH GIỮA, trong chừng mực cuộn được: mấy ô đầu
        /// và mấy ô cuối thanh không bao giờ ra được giữa, vì đưa chúng vào giữa nghĩa là
        /// kéo content ra khỏi biên và để lộ một khoảng trống. Phép kẹp 0..1 lo chuyện đó.
        private void ScrollToSwatch(int paletteIndex)
        {
            if (_scrollRect == null || !isActiveAndEnabled) return;

            var swatch = FindSwatch(paletteIndex);
            if (swatch == null || !swatch.gameObject.activeInHierarchy) return;

            if (_focusScroll != null) StopCoroutine(_focusScroll);

            _focusScroll = StartCoroutine(ScrollToSwatchRoutine((RectTransform)swatch.transform));
        }

        private IEnumerator ScrollToSwatchRoutine(RectTransform swatch)
        {
            // Đợi hết một frame vì đúng lý do đã ghi ở RelayoutBarRoutine: bố cục của
            // thanh có thể đang chờ tính lại, và đo trước lúc đó là đo trên số cũ.
            yield return null;

            if (_scrollRect == null || swatch == null) yield break;

            var content = _scrollRect.content;
            if (content == null) yield break;

            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            // Thanh đã vừa khung nhìn thì không có gì để cuộn — mà lúc đó RelayoutBar
            // còn đang giữ nó ở giữa, cuộn vào là phá luôn phép căn giữa đó.
            var scrollable = content.rect.width - viewport.rect.width;
            if (scrollable <= 1f) yield break;

            // Đo hai mép của ô màu trong hệ toạ độ của CONTENT rồi quy về khoảng cách
            // tính từ mép trái content — đó đúng là đơn vị mà horizontalNormalizedPosition
            // dùng, nên không phải đoán Anchor hay Pivot của ai cả.
            var rect = swatch.rect;
            var left = content.InverseTransformPoint(
                swatch.TransformPoint(new Vector3(rect.xMin, 0f, 0f))).x - content.rect.xMin;
            var right = content.InverseTransformPoint(
                swatch.TransformPoint(new Vector3(rect.xMax, 0f, 0f))).x - content.rect.xMin;

            if (right < left)
            {
                var swap = left;
                left = right;
                right = swap;
            }

            var window = viewport.rect.width;
            var from = Mathf.Clamp01(_scrollRect.horizontalNormalizedPosition);

            // Ô đang nằm gọn trong khung thì ĐỨNG YÊN.
            //
            // Đây là điều kiện quan trọng nhất của cả hàm. Cuộn vô điều kiện thì mỗi lần
            // chọn màu cả thanh lại trượt đi dưới ngón tay, kể cả khi ô ấy đang nằm ngay
            // trước mắt — người chơi vừa nhắm trúng thì mọi ô khác đã đổi chỗ.
            var visibleLeft = from * scrollable;

            if (left >= visibleLeft && right <= visibleLeft + window) yield break;

            // Phải dịch thật thì đưa hẳn ô vào GIỮA, đừng dịch vừa đủ cho nó ló ra: ô nằm
            // sát mép khung vẫn khó thấy, và lần chọn kế tiếp lại phải dịch tiếp.
            var windowLeft = (left + right) * 0.5f - window * 0.5f;

            var to = Mathf.Clamp01(windowLeft / scrollable);
            if (Mathf.Abs(to - from) < 0.001f) yield break;

            var duration = Mathf.Max(0f, _focusScrollDuration);

            if (duration <= 0f)
            {
                _scrollRect.velocity = Vector2.zero;
                _scrollRect.horizontalNormalizedPosition = to;
                yield break;
            }

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                // Dập đà MỖI FRAME chứ không chỉ một lần lúc bắt đầu: cú kéo tay dở dang
                // vẫn còn trớn, và Scroll Rect cộng trớn đó vào SAU khi ta đặt vị trí.
                _scrollRect.velocity = Vector2.zero;
                _scrollRect.horizontalNormalizedPosition =
                    Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration));

                yield return null;
            }

            _scrollRect.velocity = Vector2.zero;
            _scrollRect.horizontalNormalizedPosition = to;
            _focusScroll = null;
        }

        /// Ô màu mà hướng dẫn đang nói tới, hoặc null khi thanh không có đủ ô.
        ///
        /// Trả về RectTransform chứ không phải toạ độ: hướng dẫn cần bám theo nó khi bố
        /// cục đổi, mà một điểm chụp sẵn thì không bám được.
        public RectTransform TutorialSwatchRect
        {
            get
            {
                var swatch = VisibleSwatchAt(_tutorialSwatchOrder);

                return swatch != null ? (RectTransform)swatch.transform : null;
            }
        }

        /// Ô màu của một màu đã tô xong vừa BIẾN MẤT hẳn khỏi thanh.
        ///
        /// Muộn hơn hẳn "màu này hết ô chưa tô": giữa hai mốc đó còn cú loé mừng trên ô
        /// màu rồi cú khép bề rộng. Màn hướng dẫn đợi đúng mốc này, vì thứ nó chờ là cả
        /// chuỗi ĐÃ DIỄN XONG chứ không phải một con số vừa về 0.
        ///
        /// Ai đăng ký nhớ gỡ trong OnDestroy của mình.
        public event Action<int> OnSwatchRemoved;

        /// Chỉ số bảng màu của đúng cái ô trên. -1 khi thanh không có đủ ô.
        ///
        /// KHÔNG suy ra từ thứ tự: những màu đã tô xong bị thu lại và tắt đi, nên ô thứ
        /// hai còn thấy được không nhất thiết mang chỉ số 1. Ở màn hướng dẫn thì chưa màu
        /// nào xong nên hai con số trùng nhau, nhưng viết cứng nó sẽ sai lặng lẽ ngay lần
        /// đầu ai đó bật hướng dẫn ở một màn chơi dở.
        public int TutorialSwatchPaletteIndex
        {
            get
            {
                var swatch = VisibleSwatchAt(_tutorialSwatchOrder);

                return swatch != null ? swatch.PaletteIndex : -1;
            }
        }

        /// Ô thứ `order` trong số những ô ĐANG HIỆN, đếm từ 0. null khi không đủ ô.
        ///
        /// Duyệt lại mỗi lần hỏi chứ không cache: chỉ được hỏi lúc đặt ngón tay và ở mỗi
        /// cú bấm ô màu trong lúc hướng dẫn, mà danh sách dài nhất cũng vài chục phần tử.
        /// Cache thì phải nhớ dọn ở mọi lần thanh dựng lại — đắt hơn hẳn thứ nó tiết kiệm.
        private ColorSwatchView VisibleSwatchAt(int order)
        {
            if (order < 0) return null;

            var seen = 0;

            foreach (var swatch in _swatches)
            {
                if (swatch == null) continue;
                if (!swatch.gameObject.activeSelf) continue;

                if (seen == order) return swatch;

                seen++;
            }

            return null;
        }

        /// Hỏi LocksInput, KHÔNG hỏi IsRunning: nhịp cuối vẫn đang chạy nhưng đã thả hết
        /// khoá, và thanh màu phải cuộn lại được ngay từ nhịp đó.
        private bool IsTutorialRunning => _tutorialState != null && _tutorialState.LocksInput;

        /// Dựng lại trạng thái cuộn khi hướng dẫn bật/tắt.
        ///
        /// Gọi ApplyBarAlignment chứ không tự đặt _scrollRect.horizontal: chỉ mình nó biết
        /// thanh có dài hơn khung nhìn hay không, mà đó mới là vế còn lại của phép AND.
        /// Đặt tay ở đây là mở cuộn cho cả những màn chỉ có bốn năm màu.
        ///
        /// scrollToStart: false — không giật thanh về mép trái. Người chơi vừa chọn xong
        /// màu, giật thanh ngay lúc đó là kéo mọi ô khác ra khỏi chỗ mắt họ vừa nhìn thấy.
        private void HandleTutorialStageChanged(TutorialStage stage) => ApplyBarAlignment(false);

        public bool TryGetOriginWorldPosition(int paletteIndex, out Vector3 world)
        {
            world = default;

            if (_worldCamera == null)
            {
                Debug.LogWarning($"{nameof(ColorPaletteBar)} chưa gán World Camera — " +
                                 "hiệu ứng ngọc bay sẽ không có điểm xuất phát.");
                return false;
            }

            var swatch = FindSwatch(paletteIndex);
            if (swatch == null) return false;

            // Đi qua RectTransformUtility thay vì đọc thẳng transform.position: với Canvas
            // Overlay thì hai cách trùng nhau, nhưng Camera hoặc World Space thì khác hẳn.
            // Truyền null cho Overlay là đúng theo tài liệu Unity.
            var canvas = swatch.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var screen = RectTransformUtility.WorldToScreenPoint(uiCamera, swatch.ColorCenterWorldPosition);
            var depth = Mathf.Abs(_worldCamera.transform.position.z);

            world = _worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            return true;
        }

        /// Cố tình KHÔNG đòi ô phải đang hiện.
        ///
        /// Ô màu vừa tô hết bị ẩn ngay trong lượt sự kiện OnCellPainted, mà thanh màu
        /// lại đăng ký sự kiện đó TRƯỚC hiệu ứng ngọc bay. Đòi activeSelf thì viên cuối
        /// cùng của mỗi màu hỏi vị trí xuất phát đúng lúc ô vừa tắt, không tìm ra, và
        /// vĩnh viễn không có hiệu ứng bay.
        ///
        /// Sửa ở đây chứ không sửa thứ tự Init: dựa vào thứ tự đăng ký event là loại
        /// ràng buộc vô hình, ai đổi một dòng ở Bootstrap là hỏng lại mà không hiểu vì sao.
        ///
        /// Ô đã Unbind mang PaletteIndex = -1 nên không bao giờ khớp nhầm.
        private ColorSwatchView FindSwatch(int paletteIndex)
        {
            if (paletteIndex < 0) return null;

            foreach (var swatch in _swatches)
            {
                if (swatch.PaletteIndex == paletteIndex) return swatch;
            }

            return null;
        }

        /// Tạo một lần rồi bật tắt để tái dùng — không Instantiate/Destroy mỗi màn.
        ///
        /// Ô mới sinh ra ở trạng thái TẮT: prefab vốn đang bật, nên ô nào tạo ra mà bên
        /// gọi chưa kịp bật lên sẽ hiện nguyên si nội dung của prefab.
        private ColorSwatchView GetSwatch(int slot)
        {
            while (_swatches.Count <= slot)
            {
                var created = Instantiate(_swatchPrefab, _root);
                created.gameObject.SetActive(false);

                _swatches.Add(created);
            }

            return _swatches[slot];
        }

        /// Unbind trước khi ẩn: ô còn giữ chỉ số màu của màn trước sẽ bị FindSwatch
        /// khớp nhầm, và ngọc của màn mới bay ra từ một chỗ vô nghĩa.
        private void HideAll()
        {
            foreach (var swatch in _swatches)
            {
                swatch.Unbind();
                swatch.gameObject.SetActive(false);
            }
        }
    }
}
