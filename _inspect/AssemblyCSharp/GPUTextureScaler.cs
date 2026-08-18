using UnityEngine;

public class GPUTextureScaler
{
	public static Texture2D Scaled(Texture2D src, int width, int height, FilterMode mode = FilterMode.Trilinear)
	{
		Rect source = new Rect(0f, 0f, width, height);
		_gpu_scale(src, width, height, mode);
		Texture2D texture2D = new Texture2D(width, height, TextureFormat.ARGB32, mipChain: true);
		texture2D.Reinitialize(width, height);
		texture2D.ReadPixels(source, 0, 0, recalculateMipMaps: true);
		return texture2D;
	}

	public static void Scale(Texture2D tex, int width, int height, FilterMode mode = FilterMode.Trilinear)
	{
		Rect source = new Rect(0f, 0f, width, height);
		_gpu_scale(tex, width, height, mode);
		tex.Reinitialize(width, height);
		tex.ReadPixels(source, 0, 0, recalculateMipMaps: true);
		tex.Apply(updateMipmaps: true);
	}

	private static void _gpu_scale(Texture2D src, int width, int height, FilterMode fmode)
	{
		src.filterMode = fmode;
		src.Apply(updateMipmaps: true);
		Graphics.SetRenderTarget(new RenderTexture(width, height, 32));
		GL.LoadPixelMatrix(0f, 1f, 1f, 0f);
		GL.Clear(clearDepth: true, clearColor: true, new Color(0f, 0f, 0f, 0f));
		Graphics.DrawTexture(new Rect(0f, 0f, 1f, 1f), src);
	}
}
