using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JewelPainter.UI.Views
{
    public class ShopPanelView : MonoBehaviour
    {
        [Serializable]
        public class CoinPack
        {
            [Tooltip("Nút mua của gói — btn_money bên trong fr_coin.")]
            [SerializeField] private Button _button;

            [Tooltip("Số xu người chơi nhận được khi mua gói này.")]
            [SerializeField] private int _coins;

            [Tooltip("Product ID trên Google Play / App Store. Để trống cho tới khi làm IAP.")]
            [SerializeField] private string _productId;

            public Button Button => _button;
            public int Coins => _coins;
            public string ProductId => _productId;
        }

        [Header("Gói ưu đãi")]
        [Tooltip("Nút mua của no_ads_pack: bỏ mọi quảng cáo bắt buộc, kèm xu.")]
        [SerializeField] private Button _noAdsPackButton;

        [Tooltip("Nút mua của remove_ads_pack: bỏ quảng cáo toàn màn hình.")]
        [SerializeField] private Button _removeAdsButton;

        [Header("Gói xu")]
        [Tooltip("Mỗi dòng là một ô trong CoinGrid, theo đúng thứ tự trên màn hình.")]
        [SerializeField] private CoinPack[] _coinPacks = Array.Empty<CoinPack>();

        /// Giữ lại đúng các delegate đã đăng ký để gỡ ra được ở OnDestroy — lambda tạo mới
        /// lúc gỡ là một object khác, RemoveListener sẽ không tìm thấy nó.
        private UnityAction[] _coinPackListeners = Array.Empty<UnityAction>();

        private void Awake()
        {
            AddListener(_noAdsPackButton, BuyNoAdsPack);
            AddListener(_removeAdsButton, BuyRemoveAds);

            _coinPackListeners = new UnityAction[_coinPacks.Length];

            for (var i = 0; i < _coinPacks.Length; i++)
            {
                // Chép ra biến riêng: lambda bắt biến vòng lặp thì mọi nút đều nhận
                // chung giá trị cuối của i.
                var index = i;

                _coinPackListeners[i] = () => BuyCoinPack(index);
                AddListener(_coinPacks[i]?.Button, _coinPackListeners[i]);
            }
        }

        private void OnDestroy()
        {
            RemoveListener(_noAdsPackButton, BuyNoAdsPack);
            RemoveListener(_removeAdsButton, BuyRemoveAds);

            for (var i = 0; i < _coinPackListeners.Length && i < _coinPacks.Length; i++)
            {
                RemoveListener(_coinPacks[i]?.Button, _coinPackListeners[i]);
            }
        }

        /// Mua gói bỏ quảng cáo bắt buộc kèm xu.
        public void BuyNoAdsPack()
        {
            Debug.Log($"[{nameof(ShopPanelView)}] Bấm mua No Ads Pack — chưa làm.", this);

            // TODO: gọi service mua hàng; mua xong thì tắt quảng cáo bắt buộc và cộng xu.
        }

        /// Mua gói bỏ quảng cáo toàn màn hình.
        public void BuyRemoveAds()
        {
            Debug.Log($"[{nameof(ShopPanelView)}] Bấm mua Remove Ads — chưa làm.", this);

            // TODO: gọi service mua hàng; mua xong thì tắt quảng cáo toàn màn hình.
        }

        /// Mua gói xu thứ index trong danh sách Coin Packs.
        public void BuyCoinPack(int index)
        {
            if (index < 0 || index >= _coinPacks.Length)
            {
                Debug.LogWarning($"[{nameof(ShopPanelView)}] Không có gói xu số {index}.", this);
                return;
            }

            var pack = _coinPacks[index];

            Debug.Log($"[{nameof(ShopPanelView)}] Bấm mua gói {pack.Coins} xu " +
                      $"(product '{pack.ProductId}') — chưa làm.", this);

            // TODO: gọi service mua hàng với pack.ProductId; mua xong thì cộng pack.Coins vào ví.
        }

        private static void AddListener(Button button, UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void RemoveListener(Button button, UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }
    }
}
