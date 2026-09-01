using System.Runtime.InteropServices;
using Noesis;

namespace CrusaderDE;

public class TintEffect : ShaderEffect
{
	[StructLayout(LayoutKind.Sequential)]
	private class Constants
	{
		public Color color = Colors.Blue;
	}

	private static NoesisShader Shader;

	public static readonly DependencyProperty ColorProperty = DependencyProperty.Register("Color", typeof(Color), typeof(TintEffect), new PropertyMetadata(Colors.Blue, OnColorChanged));

	private Constants _constants = new Constants();

	public Color Color
	{
		get
		{
			return (Color)GetValue(ColorProperty);
		}
		set
		{
			SetValue(ColorProperty, value);
		}
	}

	public TintEffect()
	{
		if (Shader == null)
		{
			Shader = CreateShader();
		}
		SetShader(Shader);
		SetConstantBuffer(_constants);
	}

	private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		TintEffect obj = (TintEffect)d;
		obj._constants.color = (Color)e.NewValue;
		obj.InvalidateConstantBuffer();
	}
}
