using UnityEngine;

public class StoreNotEnoughDialog : IDialog
{
    private const float kTransitionSpeed = 0.3f;

    private StoreData.Item mItemDesc;
    private SUILayout      mLayout;
    private bool           mDone;
    private float          mPriority = 500f;

    private SUILabel  mFallbackTitle;
    private SUIButton mFallbackOkBtn;
    private bool      mUseFallback;

    public bool isDone
    {
        get { return mDone && (mLayout == null || !mLayout.isAnimating); }
    }

    public bool isBlocking { get { return true; } }

    public float priority
    {
        get { return mLayout != null ? mLayout.basePriority : mPriority; }
        set
        {
            mPriority = value;
            if (mLayout != null) mLayout.basePriority = value;
            if (mUseFallback)
            {
                if (mFallbackTitle != null) mFallbackTitle.priority = value;
                if (mFallbackOkBtn != null) mFallbackOkBtn.priority = value + 1f;
            }
        }
    }

    public StoreNotEnoughDialog(StoreData.Item itemDesc)
    {
        mItemDesc = itemDesc;
        if (TryLoadLayout("Layouts/NotEnoughCurrencyDialog"))
        {
            SetupWithLayout();
        }
        else
        {
            SetupFallback();
        }
    }

    private void SetupWithLayout()
    {
        mLayout.AnimateIn(kTransitionSpeed);
        SUILabel sUILabel = mLayout["title"] as SUILabel;
        if (sUILabel != null)
        {
            sUILabel.text = "Not enough currency.";
            sUILabel.visible = true;
        }
        SUIButton sUIButton = mLayout["ok"] as SUIButton;
        if (sUIButton != null)
        {
            sUIButton.text = "OK";
            sUIButton.onButtonPressed = Close;
            sUIButton.visible = true;
        }
        else
        {
            sUIButton = mLayout["back"] as SUIButton;
            if (sUIButton != null)
            {
                sUIButton.onButtonPressed = Close;
                sUIButton.visible = true;
            }
        }
        if (sUIButton == null)
        {
            mLayout.Destroy();
            mLayout = null;
            SetupFallback();
        }
    }

    private void SetupFallback()
    {
        mUseFallback = true;
        float cx = SUIScreen.width / 2f, cy = SUIScreen.height / 2f;

        mFallbackTitle           = new SUILabel("default32");
        mFallbackTitle.text      = "Not enough currency.";
        mFallbackTitle.alignment = TextAlignment.Center;
        mFallbackTitle.anchor    = TextAnchor.MiddleCenter;
        mFallbackTitle.position  = new Vector2(cx, cy - 40f);
        mFallbackTitle.priority  = mPriority;
        mFallbackTitle.visible   = true;

        mFallbackOkBtn = new SUIButton("default32");
        mFallbackOkBtn.text = "OK";
        try
        {
            mFallbackOkBtn.SetFrames(
                "Sprites/Menus/stats_button",
                "Sprites/Menus/stats_button",
                "Sprites/Menus/stats_button");
        }
        catch { }

        mFallbackOkBtn.position        = new Vector2(cx, cy + 80f);
        mFallbackOkBtn.priority        = mPriority + 1f;
        mFallbackOkBtn.visible         = true;
        mFallbackOkBtn.onButtonPressed = Close;
        mFallbackOkBtn.area            = new Rect(cx - 120f, cy + 50f, 240f, 60f);
    }

    public void Destroy()
    {
        if (mUseFallback)
        {
            if (mFallbackTitle != null) mFallbackTitle.Destroy();
            if (mFallbackOkBtn != null) mFallbackOkBtn.Destroy();
        }
        else
        {
            if (mLayout != null) mLayout.Destroy();
            mLayout = null;
        }
    }

    public void Update()
    {
        if (!mUseFallback)
        {
            if (mLayout != null) mLayout.Update();
        }
        else if (mFallbackOkBtn != null) mFallbackOkBtn.Update();
        if (Input.GetKeyUp(KeyCode.Escape)) Close();
    }

    private void Close()
    {
        if (mDone) return;
        mDone = true;

        if (!mUseFallback && mLayout != null)
        {
            SUIButton sUIButton = mLayout["back"] as SUIButton;
            SUIButton sUIButton2 = mLayout["ok"] as SUIButton;
            if (sUIButton != null) sUIButton.onButtonPressed = null;
            if (sUIButton2 != null) sUIButton2.onButtonPressed = null;
            mLayout.AnimateOut(kTransitionSpeed);
        }
        else if (mUseFallback)
        {
            if (mFallbackOkBtn != null) mFallbackOkBtn.onButtonPressed = null;
            if (mFallbackTitle != null) mFallbackTitle.visible = false;
            if (mFallbackOkBtn != null) mFallbackOkBtn.visible = false;
        }

        if (WeakGlobalInstance<DialogHandler>.instance != null)
        {
            WeakGlobalInstance<DialogHandler>.instance.FadeOut();
        }
    }

    private bool TryLoadLayout(string layoutPath)
    {
        SUILayout sUILayout = new SUILayout(layoutPath);
        if (sUILayout != null && (sUILayout.Exists("ok") || sUILayout.Exists("back")))
        {
            mLayout = sUILayout;
            return true;
        }
        if (sUILayout != null)
        {
            sUILayout.Destroy();
        }
        return false;
    }
}
