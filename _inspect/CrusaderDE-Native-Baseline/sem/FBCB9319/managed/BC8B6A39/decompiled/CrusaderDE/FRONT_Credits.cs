using System;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_Credits : UserControl
{
	private ScrollViewer RefCreditsViewer;

	private StackPanel RefCreditsStack;

	private Image RefCreditsDELogo;

	private Image RefCreditsSHLogo;

	private Image RefCreditsImage;

	private Storyboard RefFadeIn;

	private Storyboard RefFadeOut;

	private DateTime startTime = DateTime.UtcNow;

	private DateTime imageTime = DateTime.UtcNow;

	private float offset;

	private float scrollLength;

	private const int IMAGE_DURATION = 10;

	private bool fading;

	private int imageID;

	public FRONT_Credits()
	{
		MainViewModel.Instance.FRONTCredits = this;
		InitializeComponent();
		RefCreditsViewer = (ScrollViewer)FindName("CreditsViewer");
		RefCreditsStack = (StackPanel)FindName("CreditsStack");
		RefCreditsDELogo = (Image)FindName("CreditsDELogo");
		RefCreditsSHLogo = (Image)FindName("CreditsSHLogo");
		RefCreditsImage = (Image)FindName("CreditsImage");
		RefFadeIn = (Storyboard)TryFindResource("FadeInImage");
		RefFadeIn.Completed += delegate
		{
			imageTime = DateTime.UtcNow.AddSeconds(10.0);
			fading = false;
		};
		RefFadeOut = (Storyboard)TryFindResource("FadeOutImage");
		RefFadeOut.Completed += delegate
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
		scrollLength = RefCreditsStack.ActualHeight - 1080f;
		RefCreditsImage.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_CREDITS1);
		SFXManager.instance.playMusic(15);
	}

	public void Update()
	{
		DateTime utcNow = DateTime.UtcNow;
		float num = (float)(utcNow - startTime).TotalMilliseconds;
		if (KeyManager.instance.IsKeyHeldDown(KeyCode.Space) || KeyManager.instance.IsKeyHeldDown(KeyCode.DownArrow))
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
			scrollLength = RefCreditsStack.ActualHeight - 1080f;
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

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_Credits.xaml");
	}
}
