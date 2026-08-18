using System;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_Credits : UserControl
{
	public ScrollViewer RefCreditsViewer;

	public StackPanel RefCreditsStack;

	public Image RefCreditsDELogo;

	public Image RefCreditsSHLogo;

	public Image RefCreditsImage;

	public Storyboard RefFadeIn;

	public Storyboard RefFadeOut;

	public DateTime startTime = DateTime.UtcNow;

	public DateTime imageTime = DateTime.UtcNow;

	public float offset;

	public float scrollLength;

	public const int IMAGE_DURATION = 10;

	public bool fading;

	public int imageID;

	public FRONT_Credits()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected O, but got Unknown
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Expected O, but got Unknown
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Expected O, but got Unknown
		MainViewModel.Instance.FRONTCredits = this;
		InitializeComponent();
		RefCreditsViewer = (ScrollViewer)((FrameworkElement)this).FindName("CreditsViewer");
		RefCreditsStack = (StackPanel)((FrameworkElement)this).FindName("CreditsStack");
		RefCreditsDELogo = (Image)((FrameworkElement)this).FindName("CreditsDELogo");
		RefCreditsSHLogo = (Image)((FrameworkElement)this).FindName("CreditsSHLogo");
		RefCreditsImage = (Image)((FrameworkElement)this).FindName("CreditsImage");
		RefFadeIn = (Storyboard)((FrameworkElement)this).TryFindResource((object)"FadeInImage");
		((Timeline)RefFadeIn).Completed += (CompletedHandler)delegate
		{
			imageTime = DateTime.UtcNow.AddSeconds(10.0);
			fading = false;
		};
		RefFadeOut = (Storyboard)((FrameworkElement)this).TryFindResource((object)"FadeOutImage");
		((Timeline)RefFadeOut).Completed += (CompletedHandler)delegate
		{
			RefCreditsImage.Source = MainViewModel.Instance.GetImage((Enums.eImages)(2 + imageID));
			RefFadeIn.Begin();
		};
	}

	public void init()
	{
		RefCreditsDELogo.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_LOGO);
		RefCreditsSHLogo.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_SH1LOGO);
		startTime = DateTime.UtcNow;
		imageTime = startTime.AddSeconds(10.0);
		imageID = 0;
		fading = false;
		offset = 0f;
		scrollLength = ((FrameworkElement)RefCreditsStack).ActualHeight - 1080f;
		RefCreditsImage.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_CREDITS1);
		SFXManager.instance.playMusic(15);
	}

	public void Update()
	{
		DateTime utcNow = DateTime.UtcNow;
		float num = (float)(utcNow - startTime).TotalMilliseconds;
		if (KeyManager.instance.IsKeyHeldDown((KeyCode)32) || KeyManager.instance.IsKeyHeldDown((KeyCode)274))
		{
			offset += num / 3f;
		}
		else
		{
			offset += num / 20f;
		}
		startTime = utcNow;
		RefCreditsViewer.ScrollToVerticalOffset(offset);
		if (scrollLength > 0f)
		{
			if (offset >= scrollLength - 5f)
			{
				offset = 0f;
			}
		}
		else
		{
			scrollLength = ((FrameworkElement)RefCreditsStack).ActualHeight - 1080f;
		}
		if (!fading && utcNow > imageTime)
		{
			fading = true;
			imageID++;
			if (imageID >= 6)
			{
				imageID = 0;
			}
			RefFadeOut.Begin();
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_Credits.xaml");
	}
}
