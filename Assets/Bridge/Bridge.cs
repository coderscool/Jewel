using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

#if BUILD
using com.adjust.sdk;
#endif

public class Bridge : MonoBehaviour
{
    private static Bridge instance;
    private static readonly object lockObject = new object();
    private static bool applicationIsQuitting = false;
    public Queue<Action> ExecuteOnMainThread = new Queue<Action>();
    public Queue<Action> LogEventFirebaseOnMainThread = new Queue<Action>();

    public List<ProductPurchase> listProductPurchase = new List<ProductPurchase>()
    {
        new ProductPurchase() { productID = Product_ID.NO_ADS, purchaseType = ProductPurchaseType.NonConsumable },
        new ProductPurchase() { productID = Product_ID.BONUS_PACK, purchaseType = ProductPurchaseType.NonConsumable },
        new ProductPurchase() { productID = Product_ID.MONEY_500, purchaseType = ProductPurchaseType.Consumable },
        new ProductPurchase() { productID = Product_ID.MONEY_1500, purchaseType = ProductPurchaseType.Consumable },
        new ProductPurchase() { productID = Product_ID.MONEY_4000, purchaseType = ProductPurchaseType.Consumable },
        new ProductPurchase() { productID = Product_ID.MONEY_8000, purchaseType = ProductPurchaseType.Consumable },
    };

    public static Bridge Instance
    {
        get
        {
            if (applicationIsQuitting)
            {
                Debug.LogError("[Singleton] Instance '" + typeof(Bridge) + "' already destroyed on application quit." + " Won't create again - returning null.");
                return null;
            }
            // Check if an instance already exists
            if (instance == null)
            {
                // Use lock to ensure only one thread initializes the instance
                lock (lockObject)
                {
                    // Double check to ensure instance is still null
                    if (instance == null)
                    {
                        // Find existing instance in the scene
                        instance = FindObjectOfType<Bridge>();
                        // If instance is still null, create a new GameObject to hold the Singleton
                        if (instance == null)
                        {
                            GameObject singletonObject = new GameObject(typeof(Bridge).Name);
                            instance = singletonObject.AddComponent<Bridge>();
                        }
                        // Ensure that the instance is not destroyed when loading new scenes
                        DontDestroyOnLoad(instance.gameObject);
                    }
                }
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        applicationIsQuitting = true;
    }

    protected void Update()
    {
        while (ExecuteOnMainThread.Count > 0)
        {
            ExecuteOnMainThread.Dequeue().Invoke();
        }
        while (LogEventFirebaseOnMainThread.Count > 0 && IsFirebaseReady())
        {
            LogEventFirebaseOnMainThread.Dequeue().Invoke();
        }
    }

    public void OpenRate()
    {
#if UNITY_ANDROID
        Application.OpenURL("https://play.google.com/store/apps/details?id=com.weegoon.stickmanroll");
#elif UNITY_IOS
        Application.OpenURL("#");
#endif
    }

    public void TermsOfService()
    {
        Application.OpenURL("https://weegoon.vn/terms.html");
    }

    public void PrivacyPolicy()
    {
        Application.OpenURL("https://weegoon.vn/policy");
    }

    #region Check Services Initialization

    public bool IsFirebaseReady()
    {
#if BUILD
        return ServicesManager.Instance.IsFirebaseInitialized();
#else
        return true;
#endif
    }

    public bool IsIAPReady()
    {
#if BUILD
        return IAPManager.Instance.IsInitialized();
#else
        return true;
#endif
    }

    public bool IsAdsReady()
    {
#if BUILD
        return AdsManager.Instance.IsInitialized();
#else
        return true;
#endif
    }

    public bool IsRewardedAdReady()
    {
#if BUILD
        return AdsManager.Instance.IsRewardedAdReady();
#else
        return true;
#endif
    }

    public bool IsInterstitialAdReady()
    {
#if BUILD
        return AdsManager.Instance.IsInterstitialAdReady();
#else
        return true;
#endif
    }

    #endregion Check Services Initialization 

    #region Ads Manager

    public void SetNoAds()
    {
        Debug.LogError("Set No Ads");
#if BUILD
        AdsManager.Instance.SetNoAds();
#endif
    }

    public void SetExpireNoAds1Day()
    {
        Debug.LogError("Set No Ads 1 Day");
#if BUILD
        AdsManager.Instance.SetExpireNoAds1Day();
#endif
    }

    public void ShowAdmobBannerAd()
    {
#if BUILD
        AdsManager.Instance.HideMaxBannerAd();
        AdsManager.Instance.ShowAdmobBannerAd();
#endif
    }

    public void ShowMaxBannerAd()
    {
#if BUILD
        AdsManager.Instance.HideAdmobBannerAd();
        AdsManager.Instance.ShowMaxBannerAd();
#endif
    }

    public void ShowInterstitialAd(bool ignoreAdDuration = false)
    {
        Debug.Log("Show Interstitial Ad");
#if BUILD
        AdsManager.Instance.ShowInterstitialAd(ignoreAdDuration);
#endif
    }

    public void ShowRewardedAd(UnityAction OnCompleted)
    {
        Debug.Log("Show Rewarded Ad");
#if BUILD
        AdsManager.Instance.ShowRewardedAd(OnCompleted);
#else
        OnCompleted.Invoke();
#endif
    }

    #endregion  Ads Manager

    #region Cross Ads

    public void ShowCrossAds(RectTransform container, float scale, float delayShow)
    {
        container.transform.localScale = Vector3.one * scale;
        if (container.gameObject.GetComponent<Image>() != null)
        {
            DestroyImmediate(container.gameObject.GetComponent<Image>());
        }
#if BUILD
        CrossAdsManager.Instance.ShowCrossAds(container, delayShow);
#else
        container.sizeDelta = new Vector2(250f, 290f);
        container.localScale = Vector3.zero;
        Image tmp = container.gameObject.AddComponent<Image>();
        tmp.type = Image.Type.Simple;
        tmp.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        container.transform.DOScale(Vector3.one * scale, 0.35f).SetEase(Ease.OutBounce).SetDelay(delayShow);
        tmp.DOFade(1, 0.35f).SetDelay(delayShow);
#endif
    }

    public void HideCrossAds(RectTransform container)
    {
#if BUILD
        CrossAdsManager.Instance.HideCrossAds();
#else
        container.localScale = Vector3.zero;
#endif
    }

    #endregion Cross Ads

    #region Log Event

    public void SendEventToFirebase(string eventName)
    {
#if BUILD
        LogEventFirebaseOnMainThread.Enqueue(() =>
        {
            Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName);
            Debug.Log(eventName);
        });
#endif
    }

    private void SendEventToAdjust(string eventToken)
    {
#if BUILD
        ExecuteOnMainThread.Enqueue(() =>
        {
            AdjustEvent adjustEvent = new AdjustEvent(eventToken);
            Adjust.trackEvent(adjustEvent);
        });
#endif
    }

    public void SendEventLoadLevel(int lv)
    {
        string level;
        if (lv < 10)
        {
            level = "0" + lv;
        }
        else
        {
            level = "" + lv;
        }
        Debug.Log("Load Level " + level);
        SendEventToFirebase("LevelLoaded_" + level);
    }

    public void SendEventShowHint(int lv)
    {
        string level;
        if (lv < 10)
        {
            level = "0" + lv;
        }
        else
        {
            level = "" + lv;
        }
        Debug.Log("Hint Level " + level);
        SendEventToFirebase("Hint_" + level);
    }
    // Write additional Send Event functions here if needed ...

    #endregion Log Event

    #region IAP Manager

    public void OnIAPInitializedEvent(Action OnInitialized)
    {
#if BUILD
        IAPManager.Instance.OnIAPInitializedEvent += () =>
        {
            OnInitialized.Invoke();
        };
#else
        OnInitialized.Invoke();
#endif
    }

    public void PurchaseProduct(string productID, UnityAction OnSuccessPurchase)
    {
#if BUILD
        IAPManager.Instance.PurchaseProduct(productID, OnSuccessPurchase);
#else
        OnSuccessPurchase.Invoke();
#endif
    }

    public void RestorePurchase(UnityAction OnSuccessRestore)
    {
        Debug.Log("Restore Purchase");
#if BUILD
        IAPManager.Instance.Restore(OnSuccessRestore);
#else
        OnSuccessRestore.Invoke();
#endif
    }

    public bool IsPurchased(string productID)
    {
#if BUILD
        return IAPManager.Instance.IsPurchased(productID);
#else
        return true;
#endif
    }

    public bool IsSubscribed(string subscriptionID)
    {
#if BUILD
        return IAPManager.Instance.IsSubscribed(subscriptionID);
#else
        return true;
#endif
    }

    #endregion IAP Manager

}

#region Product Purchase
public class ProductPurchase
{
    public string productID;
    public ProductPurchaseType purchaseType;
}
public class Product_ID
{
    public const string NO_ADS = "stickmanroll_noads";
    public const string MONEY_500 = "stickmanroll_money_500";
    public const string MONEY_1500 = "stickmanroll_money_1500";
    public const string MONEY_4000 = "stickmanroll_money_4000";
    public const string MONEY_8000 = "stickmanroll_money_8000";

    public const string BONUS_PACK = "stickmanroll_bonuspack";
    public const string OPTIMAL_CHOICE_PACK = "stickmanroll_optimalchoicepack";

    public const string NO_ADS_SPECIAL_PACK = "stickmanroll_removeads_special";
}
public enum ProductPurchaseType
{
    Consumable = 0, // Users can purchase the Product repeatedly. Consumable Products cannot be restored.
    NonConsumable = 1, // Users can only purchased the Product once. Non-Consumable Products can be restored.
    Subscription = 2 // Users can access the Product for a finite period of time. Subscription Products can be restored.
}
#endregion