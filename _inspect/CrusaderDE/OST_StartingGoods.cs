using System;
using System.Runtime.CompilerServices;
using Noesis;

namespace CrusaderDE;

public class OST_StartingGoods : UserControl
{
	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static CompletedHandler _003C_003E9__3_0;

		internal void _003C_002Ector_003Eb__3_0(object s, EventArgs e)
		{
			MainViewModel.Instance.OST_Cart_Vis = false;
		}
	}

	public TextBlock refTimeUntilLabel;

	public static Storyboard refCartStory;

	public static Image refCart;

	public OST_StartingGoods()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		InitializeComponent();
		refTimeUntilLabel = (TextBlock)((FrameworkElement)this).FindName("TimeUntilLabel");
		refCart = (Image)((FrameworkElement)this).FindName("Cart");
		refCartStory = (Storyboard)((FrameworkElement)this).TryFindResource((object)"CartIntro");
		Storyboard obj = refCartStory;
		object obj2 = _003C_003Ec._003C_003E9__3_0;
		if (obj2 == null)
		{
			CompletedHandler val = delegate
			{
				MainViewModel.Instance.OST_Cart_Vis = false;
			};
			_003C_003Ec._003C_003E9__3_0 = val;
			obj2 = (object)val;
		}
		((Timeline)obj).Completed += (CompletedHandler)obj2;
		if (FatControler.polish)
		{
			refTimeUntilLabel.FontSize = 14f;
		}
		if (FatControler.hungarian)
		{
			refTimeUntilLabel.FontSize = 16f;
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/OST_StartingGoods.xaml");
	}
}
