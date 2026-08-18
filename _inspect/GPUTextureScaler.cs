using UnityEngine;

public class GPUTextureScaler
{
	public static Texture2D Scaled(Texture2D src, int width, int height, FilterMode mode = (FilterMode)2)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, 0f, (float)width, (float)height);
		_gpu_scale(src, width, height, mode);
		Texture2D val2 = new Texture2D(width, height, (TextureFormat)5, true);
		val2.Reinitialize(width, height);
		val2.ReadPixels(val, 0, 0, true);
		return val2;
	}

	public static void Scale(Texture2D tex, int width, int height, FilterMode mode = (FilterMode)2)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		Rect val = default(Rect);
		((Rect)(ref val))._002Ector(0f, 0f, (float)width, (float)height);
		_gpu_scale(tex, width, height, mode);
		tex.Reinitialize(width, height);
		tex.ReadPixels(val, 0, 0, true);
		tex.Apply(true);
	}

	public static void _gpu_scale(Texture2D src, int width, int height, FilterMode fmode)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		((Texture)src).filterMode = fmode;
		src.Apply(true);
		Graphics.SetRenderTarget(new RenderTexture(width, height, 32));
		GL.LoadPixelMatrix(0f, 1f, 1f, 0f);
		GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
		Graphics.DrawTexture(new Rect(0f, 0f, 1f, 1f), (Texture)(object)src);
	}
}
