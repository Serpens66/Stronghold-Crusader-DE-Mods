using Noesis;

namespace CrusaderDE;

public class PropEx
{
	public static readonly DependencyProperty Sprite1Property = DependencyProperty.RegisterAttached("Sprite1", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty Sprite2Property = DependencyProperty.RegisterAttached("Sprite2", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty Sprite3Property = DependencyProperty.RegisterAttached("Sprite3", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty Sprite4Property = DependencyProperty.RegisterAttached("Sprite4", typeof(object), typeof(PropEx));

	public static readonly DependencyProperty ImageButtonDisabledOpacityProperty = DependencyProperty.RegisterAttached("ImageButtonDisabledOpacity", typeof(object), typeof(PropEx), new PropertyMetadata("1"));

	public static readonly DependencyProperty BuildingButtonDisabledOpacityProperty = DependencyProperty.RegisterAttached("BuildingButtonDisabledOpacity", typeof(object), typeof(PropEx), new PropertyMetadata("0.5"));

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

	public static readonly DependencyProperty Sprite1MarginProperty = DependencyProperty.RegisterAttached("Sprite1Margin", typeof(object), typeof(PropEx), new PropertyMetadata("10,0,0,0"));

	public static readonly DependencyProperty BorderVisibilityProperty = DependencyProperty.RegisterAttached("BorderVisibility", typeof(Visibility), typeof(PropEx), new PropertyMetadata(Visibility.Hidden));

	public static readonly DependencyProperty ButtonVisibilityProperty = DependencyProperty.RegisterAttached("ButtonVisibility", typeof(Visibility), typeof(PropEx), new PropertyMetadata(Visibility.Visible));

	private static readonly SolidColorBrush defaultBrush = new SolidColorBrush(Color.FromRgb(244, 222, 170));

	public static readonly DependencyProperty ButtonTextColourProperty = DependencyProperty.RegisterAttached("ButtonTextColour", typeof(object), typeof(PropEx), new PropertyMetadata(defaultBrush));

	public static readonly DependencyProperty FrontEndButtonFontSizeProperty = DependencyProperty.RegisterAttached("FrontEndButtonFontSize", typeof(object), typeof(PropEx), new PropertyMetadata("44"));

	public static readonly DependencyProperty FrontEndButtonLineHeightProperty = DependencyProperty.RegisterAttached("FrontEndButtonLineHeight", typeof(object), typeof(PropEx), new PropertyMetadata("30"));

	public static readonly DependencyProperty GlowButtonFontSizeProperty = DependencyProperty.RegisterAttached("GlowButtonFontSize", typeof(object), typeof(PropEx), new PropertyMetadata("16"));

	public static readonly DependencyProperty GlowButtonLFontSizeProperty = DependencyProperty.RegisterAttached("GlowButtonLFontSize", typeof(object), typeof(PropEx), new PropertyMetadata("24"));

	public static readonly DependencyProperty GlowButtonTextHeightProperty = DependencyProperty.RegisterAttached("GlowButtonTextHeight", typeof(object), typeof(PropEx), new PropertyMetadata("24"));

	public static readonly DependencyProperty FlowControlProperty = DependencyProperty.RegisterAttached("ImageFlow", typeof(FlowDirection), typeof(PropEx), new PropertyMetadata(FlowDirection.LeftToRight));

	public static void SetSprite1(UIElement element, object value)
	{
		element.SetValue(Sprite1Property, value);
	}

	public static object GetSprite1(UIElement element)
	{
		return element.GetValue(Sprite1Property);
	}

	public static void SetSprite2(UIElement element, object value)
	{
		element.SetValue(Sprite2Property, value);
	}

	public static object GetSprite2(UIElement element)
	{
		return element.GetValue(Sprite2Property);
	}

	public static void SetSprite3(UIElement element, object value)
	{
		element.SetValue(Sprite3Property, value);
	}

	public static object GetSprite3(UIElement element)
	{
		return element.GetValue(Sprite3Property);
	}

	public static void SetSprite4(UIElement element, object value)
	{
		element.SetValue(Sprite4Property, value);
	}

	public static object GetSprite4(UIElement element)
	{
		return element.GetValue(Sprite4Property);
	}

	public static void SetImageButtonDisabledOpacity(UIElement element, object value)
	{
		element.SetValue(ImageButtonDisabledOpacityProperty, value);
	}

	public static object GetImageButtonDisabledOpacity(UIElement element)
	{
		return (double)element.GetValue(ImageButtonDisabledOpacityProperty);
	}

	public static void SetBuildingButtonDisabledOpacity(UIElement element, object value)
	{
		element.SetValue(BuildingButtonDisabledOpacityProperty, value);
	}

	public static object GetBuildingButtonDisabledOpacity(UIElement element)
	{
		return (double)element.GetValue(BuildingButtonDisabledOpacityProperty);
	}

	public static void SetSvgImage(UIElement element, object value)
	{
		element.SetValue(SvgImageProperty, value);
	}

	public static object GetSvgImage(UIElement element)
	{
		return element.GetValue(SvgImageProperty);
	}

	public static void SetBackImage(UIElement element, object value)
	{
		element.SetValue(BackImageProperty, value);
	}

	public static object GetBackImage(UIElement element)
	{
		return element.GetValue(BackImageProperty);
	}

	public static void SetTextA(UIElement element, object value)
	{
		element.SetValue(TextAProperty, value);
	}

	public static object GetTextA(UIElement element)
	{
		return element.GetValue(TextAProperty);
	}

	public static void SetTextB(UIElement element, object value)
	{
		element.SetValue(TextBProperty, value);
	}

	public static object GetTextB(UIElement element)
	{
		return element.GetValue(TextBProperty);
	}

	public static void SetTextC(UIElement element, object value)
	{
		element.SetValue(TextCProperty, value);
	}

	public static object GetTextC(UIElement element)
	{
		return element.GetValue(TextCProperty);
	}

	public static void SetTextLeft(UIElement element, object value)
	{
		element.SetValue(TextLeftProperty, value);
	}

	public static object GetTextLeft(UIElement element)
	{
		return element.GetValue(TextLeftProperty);
	}

	public static void SetTextLeftHL(UIElement element, object value)
	{
		element.SetValue(TextLeftHLProperty, value);
	}

	public static object GetTextLeftHL(UIElement element)
	{
		return element.GetValue(TextLeftHLProperty);
	}

	public static void SetTextRight(UIElement element, object value)
	{
		element.SetValue(TextRightProperty, value);
	}

	public static object GetTextRight(UIElement element)
	{
		return element.GetValue(TextRightProperty);
	}

	public static void SetTextCentre(UIElement element, object value)
	{
		element.SetValue(TextCentreProperty, value);
	}

	public static object GetTextCentre(UIElement element)
	{
		return element.GetValue(TextCentreProperty);
	}

	public static void SetTextCentreHL(UIElement element, object value)
	{
		element.SetValue(TextCentreHLProperty, value);
	}

	public static object GetTextCentreHL(UIElement element)
	{
		return element.GetValue(TextCentreHLProperty);
	}

	public static void SetSprite1Width(UIElement element, double value)
	{
		element.SetValue(Sprite1WidthProperty, value);
	}

	public static double GetSprite1Width(UIElement element)
	{
		return (double)element.GetValue(Sprite1WidthProperty);
	}

	public static void SetSprite1Height(UIElement element, double value)
	{
		element.SetValue(Sprite1HeightProperty, value);
	}

	public static double GetSprite1Height(UIElement element)
	{
		return (double)element.GetValue(Sprite1HeightProperty);
	}

	public static void SetSprite1Margin(UIElement element, object value)
	{
		element.SetValue(Sprite1MarginProperty, value);
	}

	public static object GetSprite1Margin(UIElement element)
	{
		return element.GetValue(Sprite1MarginProperty);
	}

	public static void SetBorderVisibility(UIElement element, Visibility value)
	{
		element.SetValue(BorderVisibilityProperty, value);
	}

	public static Visibility GetBorderVisibility(UIElement element)
	{
		return (Visibility)element.GetValue(BorderVisibilityProperty);
	}

	public static void SetButtonVisibility(UIElement element, Visibility value)
	{
		element.SetValue(ButtonVisibilityProperty, value);
	}

	public static Visibility GetButtonVisibility(UIElement element)
	{
		return (Visibility)element.GetValue(ButtonVisibilityProperty);
	}

	public static void SetButtonTextColour(UIElement element, object value)
	{
		element.SetValue(ButtonTextColourProperty, value);
	}

	public static object SetButtonTextColour(UIElement element)
	{
		return element.GetValue(ButtonTextColourProperty);
	}

	public static void SetFrontEndButtonFontSize(UIElement element, object value)
	{
		element.SetValue(FrontEndButtonFontSizeProperty, value);
	}

	public static object GetFrontEndButtonFontSize(UIElement element)
	{
		return (double)element.GetValue(FrontEndButtonFontSizeProperty);
	}

	public static void SetFrontEndButtonLineHeight(UIElement element, object value)
	{
		element.SetValue(FrontEndButtonLineHeightProperty, value);
	}

	public static object GetFrontEndButtonLineHeight(UIElement element)
	{
		return (double)element.GetValue(FrontEndButtonLineHeightProperty);
	}

	public static void SetGlowButtonFontSize(UIElement element, object value)
	{
		element.SetValue(GlowButtonFontSizeProperty, value);
	}

	public static object GetGlowuttonFontSize(UIElement element)
	{
		return (double)element.GetValue(GlowButtonFontSizeProperty);
	}

	public static void SetGlowButtonLFontSize(UIElement element, object value)
	{
		element.SetValue(GlowButtonLFontSizeProperty, value);
	}

	public static object GetGlowButtonLFontSize(UIElement element)
	{
		return (double)element.GetValue(GlowButtonLFontSizeProperty);
	}

	public static void SetGlowButtonTextHeight(UIElement element, object value)
	{
		element.SetValue(GlowButtonTextHeightProperty, value);
	}

	public static object GetGlowButtonTextHeight(UIElement element)
	{
		return (double)element.GetValue(GlowButtonTextHeightProperty);
	}

	public static void SetImageFlow(UIElement element, FlowDirection value)
	{
		element.SetValue(FlowControlProperty, value);
	}

	public static FlowDirection GetImageFlow(UIElement element)
	{
		return (FlowDirection)element.GetValue(FlowControlProperty);
	}
}
