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
            [Tooltip("Nút mua của gói.")]
            [SerializeField] private Button _button;

            [Tooltip("Số xu người chơi nhận được khi mua gói này.")]
            [SerializeField] private int _coins;

            [Tooltip("Product ID trên Google Play / App Store.")]
            [SerializeField] private string _productId;

            public Button Button => _button;
            public int Coins => _coins;
            public string ProductId => _productId;
        }

        [Header("Gói ưu đãi")]
        [Tooltip("Nút mua gói bỏ quảng cáo bắt buộc kèm xu.")]
        [SerializeField] private Button _noAdsPackButton;

        [Tooltip("Nút mua gói bỏ quảng cáo toàn màn hình.")]
        [SerializeField] private Button _removeAdsButton;

        [Header("Gói xu")]
        [Tooltip("Danh sách gói xu theo thứ tự trên màn hình.")]
        [SerializeField] private CoinPack[] _coinPacks = Array.Empty<CoinPack>();

        private UnityAction[] _coinPackListeners = Array.Empty<UnityAction>();

        private void Awake()
        {
            AddListener(_noAdsPackButton, BuyNoAdsPack);
            AddListener(_removeAdsButton, BuyRemoveAds);

            _coinPackListeners = new UnityAction[_coinPacks.Length];

            for (var i = 0; i < _coinPacks.Length; i++)
            {
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
        }

        /// Mua gói bỏ quảng cáo toàn màn hình.
        public void BuyRemoveAds()
        {
            Debug.Log($"[{nameof(ShopPanelView)}] Bấm mua Remove Ads — chưa làm.", this);
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
