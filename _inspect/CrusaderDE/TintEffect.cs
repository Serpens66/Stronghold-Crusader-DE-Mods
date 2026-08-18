using System.Runtime.InteropServices;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class TintEffect : ShaderEffect
{
	[StructLayout(LayoutKind.Sequential)]
	public class Constants
	{
		public Color color = Colors.Blue;
	}

	public static NoesisShader Shader;

	public static readonly DependencyProperty ColorProperty = DependencyProperty.Register("Color", typeof(Color), typeof(TintEffect), new PropertyMetadata((object)Colors.Blue, new PropertyChangedCallback(OnColorChanged)));

	public Constants _constants = new Constants();

	public Color Color
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (Color)((DependencyObject)this).GetValue(ColorProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((DependencyObject)this).SetValue(ColorProperty, (object)value);
		}
	}

	public TintEffect()
	{
		if ((Object)(object)Shader == (Object)null)
		{
			Shader = ((ShaderEffect)this).CreateShader();
		}
		((ShaderEffect)this).SetShader(Shader);
		((ShaderEffect)this).SetConstantBuffer<Constants>(_constants);
	}

	public static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		TintEffect obj = (TintEffect)(object)d;
		obj._constants.color = (Color)e.NewValue;
		((ShaderEffect)obj).InvalidateConstantBuffer();
	}
}
