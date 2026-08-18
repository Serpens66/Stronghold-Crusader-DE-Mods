using Noesis;

namespace CrusaderDE;

public class PropEx
{
	public static readonly DependencyProperty Sprite1Property = DependencyProperty.RegisterAttached("Sprite1", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty Sprite2Property = DependencyProperty.RegisterAttached("Sprite2", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty Sprite3Property = DependencyProperty.RegisterAttached("Sprite3", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty Sprite4Property = DependencyProperty.RegisterAttached("Sprite4", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty ImageButtonDisabledOpacityProperty = DependencyProperty.RegisterAttached("ImageButtonDisabledOpacity", typeof(object), typeof(PropEx), new PropertyMetadata((object)"1"));

	public static readonly DependencyProperty BuildingButtonDisabledOpacityProperty = DependencyProperty.RegisterAttached("BuildingButtonDisabledOpacity", typeof(object), typeof(PropEx), new PropertyMetadata((object)"0.5"));

	public static readonly DependencyProperty SvgImageProperty = DependencyProperty.RegisterAttached("SvgImage", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty BackImageProperty = DependencyProperty.RegisterAttached("BackImage", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty TextAProperty = DependencyProperty.RegisterAttached("TextA", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty TextBProperty = DependencyProperty.RegisterAttached("TextB", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty TextCProperty = DependencyProperty.RegisterAttached("TextC", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty TextLeftProperty = DependencyProperty.RegisterAttached("TextLeft", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty TextLeftHLProperty = DependencyProperty.RegisterAttached("TextLeftHL", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty TextRightProperty = DependencyProperty.RegisterAttached("TextRight", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty TextCentreProperty = DependencyProperty.RegisterAttached("TextCentre", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty TextCentreHLProperty = DependencyProperty.RegisterAttached("TextCentreHL", typeof(string), typeof(PropEx));

	public static readonly DependencyProperty Sprite1WidthProperty = DependencyProperty.RegisterAttached("Sprite1Width", typeof(double), typeof(PropEx));

	public static readonly DependencyProperty Sprite1HeightProperty = DependencyProperty.RegisterAttached("Sprite1Height", typeof(double), typeof(PropEx));

	public static readonly DependencyProperty Sprite1MarginProperty = DependencyProperty.RegisterAttached("Sprite1Margin", typeof(object), typeof(PropEx), new PropertyMetadata((object)"10,0,0,0"));

	public static readonly DependencyProperty BorderVisibilityProperty = DependencyProperty.RegisterAttached("BorderVisibility", typeof(Visibility), typeof(PropEx), new PropertyMetadata((object)(Visibility)1));

	public static readonly DependencyProperty ButtonVisibilityProperty = DependencyProperty.RegisterAttached("ButtonVisibility", typeof(Visibility), typeof(PropEx), new PropertyMetadata((object)(Visibility)2));

	public static readonly SolidColorBrush defaultBrush = new SolidColorBrush(Color.FromRgb((byte)244, (byte)222, (byte)170));

	public static readonly DependencyProperty ButtonTextColourProperty = DependencyProperty.RegisterAttached("ButtonTextColour", typeof(object), typeof(PropEx), new PropertyMetadata((object)defaultBrush));

	public static readonly DependencyProperty FrontEndButtonFontSizeProperty = DependencyProperty.RegisterAttached("FrontEndButtonFontSize", typeof(object), typeof(PropEx), new PropertyMetadata((object)"44"));

	public static readonly DependencyProperty FrontEndButtonLineHeightProperty = DependencyProperty.RegisterAttached("FrontEndButtonLineHeight", typeof(object), typeof(PropEx), new PropertyMetadata((object)"30"));

	public static readonly DependencyProperty GlowButtonFontSizeProperty = DependencyProperty.RegisterAttached("GlowButtonFontSize", typeof(object), typeof(PropEx), new PropertyMetadata((object)"16"));

	public static readonly DependencyProperty GlowButtonLFontSizeProperty = DependencyProperty.RegisterAttached("GlowButtonLFontSize", typeof(object), typeof(PropEx), new PropertyMetadata((object)"24"));

	public static readonly DependencyProperty GlowButtonTextHeightProperty = DependencyProperty.RegisterAttached("GlowButtonTextHeight", typeof(object), typeof(PropEx), new PropertyMetadata((object)"24"));

	public static readonly DependencyProperty FlowControlProperty = DependencyProperty.RegisterAttached("ImageFlow", typeof(FlowDirection), typeof(PropEx), new PropertyMetadata((object)(FlowDirection)0));

	public static void SetSprite1(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(Sprite1Property, value);
	}

	public static object GetSprite1(UIElement element)
	{
		return ((DependencyObject)element).GetValue(Sprite1Property);
	}

	public static void SetSprite2(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(Sprite2Property, value);
	}

	public static object GetSprite2(UIElement element)
	{
		return ((DependencyObject)element).GetValue(Sprite2Property);
	}

	public static void SetSprite3(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(Sprite3Property, value);
	}

	public static object GetSprite3(UIElement element)
	{
		return ((DependencyObject)element).GetValue(Sprite3Property);
	}

	public static void SetSprite4(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(Sprite4Property, value);
	}

	public static object GetSprite4(UIElement element)
	{
		return ((DependencyObject)element).GetValue(Sprite4Property);
	}

	public static void SetImageButtonDisabledOpacity(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(ImageButtonDisabledOpacityProperty, value);
	}

	public static object GetImageButtonDisabledOpacity(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(ImageButtonDisabledOpacityProperty);
	}

	public static void SetBuildingButtonDisabledOpacity(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(BuildingButtonDisabledOpacityProperty, value);
	}

	public static object GetBuildingButtonDisabledOpacity(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(BuildingButtonDisabledOpacityProperty);
	}

	public static void SetSvgImage(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(SvgImageProperty, value);
	}

	public static object GetSvgImage(UIElement element)
	{
		return ((DependencyObject)element).GetValue(SvgImageProperty);
	}

	public static void SetBackImage(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(BackImageProperty, value);
	}

	public static object GetBackImage(UIElement element)
	{
		return ((DependencyObject)element).GetValue(BackImageProperty);
	}

	public static void SetTextA(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextAProperty, value);
	}

	public static object GetTextA(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextAProperty);
	}

	public static void SetTextB(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextBProperty, value);
	}

	public static object GetTextB(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextBProperty);
	}

	public static void SetTextC(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextCProperty, value);
	}

	public static object GetTextC(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextCProperty);
	}

	public static void SetTextLeft(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextLeftProperty, value);
	}

	public static object GetTextLeft(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextLeftProperty);
	}

	public static void SetTextLeftHL(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextLeftHLProperty, value);
	}

	public static object GetTextLeftHL(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextLeftHLProperty);
	}

	public static void SetTextRight(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextRightProperty, value);
	}

	public static object GetTextRight(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextRightProperty);
	}

	public static void SetTextCentre(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextCentreProperty, value);
	}

	public static object GetTextCentre(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextCentreProperty);
	}

	public static void SetTextCentreHL(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(TextCentreHLProperty, value);
	}

	public static object GetTextCentreHL(UIElement element)
	{
		return ((DependencyObject)element).GetValue(TextCentreHLProperty);
	}

	public static void SetSprite1Width(UIElement element, double value)
	{
		((DependencyObject)element).SetValue(Sprite1WidthProperty, (object)value);
	}

	public static double GetSprite1Width(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(Sprite1WidthProperty);
	}

	public static void SetSprite1Height(UIElement element, double value)
	{
		((DependencyObject)element).SetValue(Sprite1HeightProperty, (object)value);
	}

	public static double GetSprite1Height(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(Sprite1HeightProperty);
	}

	public static void SetSprite1Margin(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(Sprite1MarginProperty, value);
	}

	public static object GetSprite1Margin(UIElement element)
	{
		return ((DependencyObject)element).GetValue(Sprite1MarginProperty);
	}

	public static void SetBorderVisibility(UIElement element, Visibility value)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((DependencyObject)element).SetValue(BorderVisibilityProperty, (object)value);
	}

	public static Visibility GetBorderVisibility(UIElement element)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		return (Visibility)((DependencyObject)element).GetValue(BorderVisibilityProperty);
	}

	public static void SetButtonVisibility(UIElement element, Visibility value)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((DependencyObject)element).SetValue(ButtonVisibilityProperty, (object)value);
	}

	public static Visibility GetButtonVisibility(UIElement element)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		return (Visibility)((DependencyObject)element).GetValue(ButtonVisibilityProperty);
	}

	public static void SetButtonTextColour(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(ButtonTextColourProperty, value);
	}

	public static object SetButtonTextColour(UIElement element)
	{
		return ((DependencyObject)element).GetValue(ButtonTextColourProperty);
	}

	public static void SetFrontEndButtonFontSize(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(FrontEndButtonFontSizeProperty, value);
	}

	public static object GetFrontEndButtonFontSize(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(FrontEndButtonFontSizeProperty);
	}

	public static void SetFrontEndButtonLineHeight(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(FrontEndButtonLineHeightProperty, value);
	}

	public static object GetFrontEndButtonLineHeight(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(FrontEndButtonLineHeightProperty);
	}

	public static void SetGlowButtonFontSize(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(GlowButtonFontSizeProperty, value);
	}

	public static object GetGlowuttonFontSize(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(GlowButtonFontSizeProperty);
	}

	public static void SetGlowButtonLFontSize(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(GlowButtonLFontSizeProperty, value);
	}

	public static object GetGlowuttonLFontSize(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(GlowButtonLFontSizeProperty);
	}

	public static void SetGlowButtonTextHeight(UIElement element, object value)
	{
		((DependencyObject)element).SetValue(GlowButtonTextHeightProperty, value);
	}

	public static object GetGlowButtonTextHeight(UIElement element)
	{
		return (double)((DependencyObject)element).GetValue(GlowButtonTextHeightProperty);
	}

	public static void SetImageFlow(UIElement element, FlowDirection value)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((DependencyObject)element).SetValue(FlowControlProperty, (object)value);
	}

	public static FlowDirection GetImageFlow(UIElement element)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		return (FlowDirection)((DependencyObject)element).GetValue(FlowControlProperty);
	}
}
